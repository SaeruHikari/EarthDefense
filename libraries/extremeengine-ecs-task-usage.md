**ExtremeEngine ECS Task 实际用法与 C# 使用心得**

阅读日期：2026-09-13。依据本机 D:/Code/ExtremeEngine 当前工作区源码，重点是 RenderV2，并对照粒子、WorldPartition、Transform、Animation、Fluxel 和运行时测试。本文区分“源码事实”和“用于 Earthward C# ECS 的建议”。本轮进行静态阅读，没有运行 ExtremeEngine 测试或性能基准；以下性能判断是机制分析，不是实测数据。

**核心判断**

ExtremeEngine 的实际工作流由四种机制共同组成：ECS Task 承担有组件访问契约的批量运算，普通 Fiber Task 承担阶段编排和外部操作，Messages 传递变化与事件，StagingWorld 聚合并提交结构变化。理解它们之间的数据所有权和完成边界，比只掌握 dispatch_task 更重要。

RenderV2 对跨阶段数据流的处理值得参考，但其后端大量任务有意串行，不能直接作为数千架战机并行计算的粒度模板。

**1. 先分清项目里几种名字相近的 Task / Job**

| 种类 | 实际职责 | 例子 |
|---|---|---|
| 查询 ECS Task | 根据组件访问声明匹配实体，按 Chunk / batch 执行 | UpdateRenderInstancesTask、SkrAnimationJob |
| 消息 ECS Task | 从订阅队列取得目标和消息，再绑定目标组件 | StageMeshRendererChangedTask、CommitRenderInstanceChangedTask |
| 普通 Fiber Task | 等阶段、调用外部模块、合并结果、发完成信号 | CoreSourceProjection、StagingApply、FrameGraphSubmission |
| Creation 初始化器 | 同步创建实体后直接初始化组件，并向调用方返回创建结果 | FluxelDynamicMeshSourceWriter |
| 算法库 Job | 在 ECS Task 内运行的具体算法，本身不经过 ECS 调度器 | ozz SamplingJob、LocalToModelJob |
| RenderGraph 工作 | 图记录、资源依赖、GPU 提交 | record fragments、graph->execute |

例如动画系统把 ozz 的采样和骨骼矩阵计算放在一个 ECS Task 的实体循环里；没有为每个关节再提交 ECS Task。[动画任务](D:/Code/ExtremeEngine/engine/modules/render/animation/src/components/anim_component.cpp:19)

Fluxel 的创建初始化器通过引用传入 create_entities，在 run 中写回 created，函数返回后立即取得实体。这个路径不能和异步任务体的复制语义混为一谈。[创建与返回实体](D:/Code/ExtremeEngine/engine/modules/render/fluxel/src/fluxel_scene_dynamic_mesh.cpp:55)、[create_entities 实现](D:/Code/ExtremeEngine/engine/modules/engine/runtime/include/SkrRuntime/ecs/world.hpp:580)

**2. RenderV2 一帧的实际骨架**

下面是主要先后关系，省略可选扩展和同一阶段的子任务；箭头表示依赖，不表示每个箭头都阻塞主线程。

~~~mermaid
flowchart TD
  A["更新 Source World<br/>粒子 Driver / Pipeline"] --> B["消费 Source Changed / Removed<br/>投影进 StagingWorld"]
  B --> C["等待旧访问结束<br/>Apply / 实体重映射 / 汇总变化"]
  C --> D["更新 Render World 后端<br/>Mesh / Primitives / Lights"]
  D --> E["记录 Scene 资源"]
  E --> F["按 Target / View 记录视图"]
  F --> G["等待记录完成<br/>合并 RenderGraph Fragments"]
  G --> H["RenderGraph execute / Present 提交"]
  H --> I["CPU frame counter 完成"]
~~~

主入口连续调用 submit_source_projection、submit_staging_apply、submit_render_world_update，再接 scene records、view records。这是一边发射、一边把完成句柄交给后继的在线组织方式。[阶段调用链](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/renderer.cpp:195)

图提交任务持有 records、targets 和前置 counters 的副本，等待记录完成后合并 fragments，然后执行 RenderGraph，最后递减 frame_counter。[图提交和所有权捕获](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/renderer.cpp:50)

RenderPipeline 扩展接口也接受或返回 counter。例如 project_source 得到 StagingWorld 和 after；默认实现直接返回 after，具体扩展需保证返回的完成信号涵盖自己追加的工作。[扩展接口](D:/Code/ExtremeEngine/engine/modules/render/renderer/include/SkrRenderer/render_v2/render_pipeline.hpp:330)

使用心得：把每个业务阶段设计成返回完成句柄的操作。调用者能继续提交无关工作，只有真正需要读结果或改结构时才等待。

**3. “实时调度”的准确含义：准备立即执行，任务可以挂起等待**

dispatch_task 的顺序是：

1. 在提交调用中执行 build，生成访问签名。
2. 创建或复用 Query，并更新 Query 元条件。
3. 如果有 prepare(entityCount)，在提交调用中立即执行。
4. 复制任务参数并交给调度器。
5. 有 after_events / after_counters 时，普通 Fiber Task 先等待它们，再将 ECS Task 入队。
6. 服务线程在线执行依赖分析和 WorkUnit 生成，然后发射可执行工作。

源码没有“等整帧编排完再统一 launch”的必要步骤。与此同时，dispatch_task 返回也不意味着任务体已经执行。[dispatch_task](D:/Code/ExtremeEngine/engine/modules/engine/runtime/include/SkrRuntime/ecs/world.hpp:495)、[after 门控与入队](D:/Code/ExtremeEngine/engine/modules/engine/runtime/src/ecs/scheduler.cpp:493)、[在线分析与发射](D:/Code/ExtremeEngine/engine/modules/engine/runtime/src/ecs/scheduler.cpp:718)

这里最容易误用的是 prepare：after 并不保护 prepare。如果 prepare 会扩大一个正在被旧任务使用的数组，必须在调用 dispatch_task 之前保证旧使用已结束，或者使用不同的输出缓冲区。同样，不应在旧结构变化尚未完成时，把 prepare 看到的数量当成未来消息数量或最终实体数量。

需要同帧消费生产者产生的消息时，也必须安排明确的生产完成边界。消息是在 WorkUnit 生成阶段取出的，随后才执行组件依赖等待。仅仅给消费者声明读组件，不保证它取消息时生产者已经发送了所有消息。[消息取出阶段](D:/Code/ExtremeEngine/engine/modules/engine/runtime/src/ecs/scheduler.cpp:232)

RenderV2 的 source projection 外层先等待 source update，再提交消息消费者；已有测试专门覆盖“异步 Source 消息进入本次提交帧”。[外层等待](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_source.cpp:156)、[对应测试](D:/Code/ExtremeEngine/engine/modules/render/renderer/tests/render_v2_scene_tests.cpp:385)

**4. 访问声明要尽量窄，而且只能描述自己实际覆盖的数据**

任务的 build 同时承担实体过滤、组件读写依赖和执行时字段绑定。

- read / write：要求拥有对应组件，并绑定当前批次的连续视图。
- optional_read / optional_write：允许组件缺失，任务体必须检查绑定是否存在。
- has / none：描述匹配条件；不要把纯过滤当成组件访问声明。
- Random accessor：跨当前批次访问其他实体，必须声明随机访问。
- subscribe / send：绑定消息消费和发送能力。

顺序访问之间的冲突可以按物理 Chunk 同步；涉及 Random 的依赖使用 WholeTask。相同组件类型位于不同 World 时，依赖键包含 storage，不会仅因类型相同而相互阻塞。[声明 API](D:/Code/ExtremeEngine/engine/modules/engine/runtime/include/SkrRuntime/ecs/world.hpp:78)、[同步粒度](D:/Code/ExtremeEngine/engine/modules/engine/runtime/src/ecs/scheduler.cpp:14)、[World 维度的依赖键](D:/Code/ExtremeEngine/engine/modules/engine/runtime/include/SkrRuntime/ecs/scheduler.hpp:190)

组件依赖推导不等于它能理解任务字段中的任意 C++ 指针。任务写 GPUScene、Map、Vector、RenderGraph 或共享输出指针时，仍要由业务保证独占、分片或前后关系。

RenderV2 的投影任务虽然常常只读 Source 组件，但还会写 Staging、source-to-render 映射、资源跟踪对象。因此 helper 同时设置 no_parallelization，并通过 previous counter 串起不同投影任务。[Source 调度 helper](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_source_tasks.hpp:28)

使用心得：组件内的数据交给 ECS 访问契约；组件外的数据需要明确的业务所有权。不能看到 build 只有 read 就认定任务没有写操作。

**5. Messages 与 StagingWorld 在 RenderV2 中是互补关系**

RenderV2 保留 Source World 与 Render World：

- Source World 保存业务与编辑侧组件。
- Render World 保存渲染需要的组件与实例关联。
- Changed 消息通知“哪个 aspect 发生变化”，任务读取当前 Source 组件，写进 StagingWorld。
- Staging Apply 完成结构及数据变更，再通过 structural sink 汇总 Render 侧 Changed / Retired 通知。
- 后端消费者把变化落实到 GPUScene、实例表等对象。

GameWorld 的 apply_source_staging 也安装 structural sink，将对应变化转换成 Source 消息；样例里的直接组件修改则显式发出 Changed。依赖调度本身不会自动替业务发出所有渲染通知。[GameWorld 提交入口](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/game_world.cpp:451)、[样例修改与通知](D:/Code/ExtremeEngine/engine/samples/render_v2/sample_simulation.cpp:311)、[Render Apply 与变化汇总](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_apply.cpp:283)

因此，Messages 不是一组结构修改指令，StagingWorld 也不是普通 World 的另一种称呼。前者表达通知或事件，后者保存可合并的创建、增删和替换结果。一个阶段可以消费多次变化，把最终状态集中提交。

源码有“同帧 Changed 后 Removed，最终不留下实例”的测试。这证明这里关心的是最终状态与退役过程，不是要求每次渲染状态变化都产生一次完整重建。[最终状态测试](D:/Code/ExtremeEngine/engine/modules/render/renderer/tests/render_v2_scene_tests.cpp:1368)

用于战斗时需要再区分：外观/可见性变化可以合并；伤害、掉落、一次性技能触发等事件，不能因为对象相同就直接覆盖掉前一条。

**6. Changed 与 Removed 必须使用不同的实体存活假设**

Changed 任务需要绑定当前组件，RenderV2 对这类消费者开启 message validation，避免源实体已失效或改变类型后还按旧布局读取。

Removed 则可能发生在实体已经删除之后。Source helper 使用 consume_messages(..., false) 先复制通知记录，再在普通异步任务里等待前置阶段，按消息中的 Entity/业务映射完成清理。它不需要访问已经删除的源组件。[移除通知消费](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_source_tasks.hpp:57)

这条 Removed helper 在等待 previous 之前已经完成消息快照；previous 约束处理顺序，不会延长本次消息收集窗口。需要纳入更晚生产的消息时，应把快照本身放在生产完成之后。

关闭 validation 并不意味着可以安全读取死实体。它只适用于消息自身已经携带足够信息、处理逻辑不再解引用原实体组件的路径。

现有测试明确先发送 MeshRendererRemoved，再删除实体，最后仍消费到通知，且 Query 实体数量已为零。[删除通知存活测试](D:/Code/ExtremeEngine/engine/modules/render/renderer/tests/render_v2_scene_tests.cpp:279)

用于 Earthward：死亡通知应携带伤害来源、位置、工厂归属、奖励所需信息，或在删除前收集这些信息。补机、掉落、自爆、渲染移除不能依赖死实体的组件仍然可查。

**7. Staging 的复用和 Apply 必须有完整边界**

RenderScene 的 source projection 会等待旧 staging_available；Apply 又同时等待本轮投影完成和旧 Render World 的访问结束。需要防止两种冲突：

- 新一帧写入 Staging 时，上一轮还在 Apply / 消费同一份数据。
- Apply 改变 Render World 布局时，旧任务还持有组件视图。

此外，新建实体的 transient identity 进入映射表后，需要在 Apply 的重映射过程中更新；删除 RenderSourceLink 后还要先发退役通知，再在对应帧边界清理 Render 实体。[Staging 复用等待](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_source.cpp:161)、[Apply 等待与延迟删除](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_apply.cpp:220)

这里保留了精细重叠的空间：Source 投影只要拿到可用 Staging，就可以与旧 Render World 后端访问重叠；真正 Apply 时才等待 Render World 访问结束。实际样例还在帧首等待上一 CPU frame，所以不能把这个能力直接描述成该样例已经跨帧充分重叠。

同一轮创建相互引用的对象也是重要用法：Primitive 会先创建暂存的 render data，再把其 transient Entity 写入 render instance；粒子多 stream 重建则让包含新子实体引用的 owner 进入同一次 Staging，以便统一重映射。[Primitive 引用](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_source_primitives.cpp:178)、[粒子 owner 与子实体](D:/Code/ExtremeEngine/engine/modules/render/particle_billboard/src/particle_billboard_renderer.cpp:1344)

使用心得：Staging 可以由任务生产，但 Apply 是明确的结构阶段。对游戏可以把出生、死亡、形态升级等结构变化集中提交；位置、冷却、生命值等常规更新仍直接在既有组件列中批量计算。

**8. Counter / Event 的所有权是业务契约的一部分**

RenderV2 helper 会在提交前 done.add(1)，把 done 放进 on_finish_counters，并保存强引用。因为 TaskOptions 中 after/on_finish 存的是 weak counter，仅把临时 counter 放进去是不够的。

Source helper 的注释直接说明要把强 counter 保留到任务完成；Backend 则将整条 counter vector move-capture 进最后的等待任务。[Source 保活](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_source_tasks.hpp:37)、[Backend 保活](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_backend.cpp:24)

这里还应区分：

- Counter 适合多个工作完成后汇合，必须先登记数量。
- Event 适合某个阶段的一次完成通知；复用前要确保上一轮结束再 clear。
- “任务输入借用的对象”与“任务完成信号”都要活到最后消费者结束。

原始 fib_task 包装最终使用 marl WaitGroup / Event。在 Fiber 环境里等待能让出执行机会；当前 counter_t::wait(bool pin) 并未使用 pin 参数，不能把 true/false 解释为两种不同执行策略。[Fiber 包装器](D:/Code/ExtremeEngine/engine/modules/core/task/include/SkrTask/fib_task.hpp:32)

C# 建议：普通 async 阶段持有输出、Staging 和强完成句柄，await 完整任务后再归还对象池。清理与失败传播用 finally。不要把 C++ 的 wait 机械翻译成工作线程中的 Task.Wait / Result，也不要把“可以 await”理解成可以建立互相等待的依赖环。

**9. Prepare + 连续结果数组 + Index 是最有价值的高吞吐模式**

CollectParticleFrameTask::prepare 先记录 entry_offset，并把输出 Vector 扩至 entity_count 大小。run 通过 entity_start_index 写 entry_offset + base + i，每个批次写自己负责的连续区间。[粒子收集任务](D:/Code/ExtremeEngine/engine/modules/render/particle_billboard/src/particle_billboard_renderer.cpp:1644)

它避免了每实体争用共享 append、循环内扩容，以及为每个结果分配独立对象。当前 CPU / GPU 两次 collect 各自 dispatch 后等待，前者完成后才执行后者的 prepare，因此共享 Vector 扩容没有与前一次写入重叠。[两次 Collect 的实际顺序](D:/Code/ExtremeEngine/engine/modules/render/particle_billboard/src/particle_billboard_renderer.cpp:2485)

entity_start_index 是当前批次在本次源数据序列中的偏移；task_index 是执行批次编号。二者不能混用，task_index 也不是 worker ID 或稳定实体序号。[TaskContext](D:/Code/ExtremeEngine/engine/modules/engine/runtime/include/SkrRuntime/ecs/world.hpp:258)

用于 Earthward：渲染快照、索敌候选、命中结果可以采用连续输出缓冲区。多发导弹、AOE 这类可变结果数量，应选每批 scratch 后合并，或计数后前缀和分配区间，而不是强行假设一机一条结果。

**10. 后端提交应串行多久，要根据真实共享状态决定**

RenderV2 mesh 后端依次调度解析 billboard camera、提交实例变化、更新实例、提交退役实例；然后串接线/点图元任务和灯光任务。相关 helper 均设置 no_parallelization，主链还使用 previous counter。[Mesh helper](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_backend_mesh.cpp:562)、[跨后端主链](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_backend.cpp:24)

这些任务要写共享的实例 ID 分配、GPU 表、TLAS 状态，所以不能仅删除 no_parallelization 就宣称并行优化完成。

View recording 的策略有条件：serialize_views = view_frames || pipeline->requirements().serial_views。存在共享 ViewFrameStore 或 Pipeline 要求串行时，各 Target 通过前驱 counter 串接；否则独立 Target 的 fragment 可以在同一个 scene resource 完成信号后并行录制。每个 RecordViewTask 内部仍明确禁用并行。RenderRuntime 的常规路径会提供 ViewFrameStore，因而走串行分支。“用了 fragment”不等于自动并行，“视图记录一律串行”也不准确。[并行条件](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_record.cpp:369)、[Runtime 提供 ViewFrameStore](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_runtime.cpp:75)、[视图顺序测试](D:/Code/ExtremeEngine/engine/modules/render/renderer/tests/render_v2_view_tests.cpp:110)

使用心得：将大量独立计算与少量共享提交分开。快照生成、变换计算可以并行，实例 ID 分配、主线程 Godot 对象操作可以集中处理。性能优化应优先缩短串行提交的数据量与时长。

**11. 其他模块提供的对照**

| 模块 | 实际用法 | 值得借鉴的部分 |
|---|---|---|
| Transform | 找根节点后递归子树，使用 Random accessor，缓存 Query 和完成 Event | 层级算法要明确跨实体访问；不能伪装成每实体独立运算 |
| Animation | 读骨架，写动画状态与皮肤矩阵，batch 64 | 每个实体内部运行适当大小的完整算法，减少过度拆任务 |
| Particle Billboard | optional 运行时组件；缺失则写 Staging；等完成、Apply 后再 Collect | 补齐结构和使用新结构之间要有明确阶段 |
| WorldPartition | SelectGrid → SelectVolume → ApplySelected → Advance；持久 Query/订阅；batch 256 | 并行收集需求，集中推进含预算和资源状态的阶段 |
| Particle Driver | 普通 Fiber 外层调度 ECS Job，等待后再 prune | 外层管生命周期，内层批量处理组件 |
| Fluxel Creation | 创建初始化器同步写组件并返回 Entity | 创建立即完成，任务型接口可复用初始化绑定 |
| Renderer Service 查找 | 临时 Query、提交后 sync_all 取回结果 | 冷路径可简化同步；高频战斗路径不应照搬全局等待 |

[Transform](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/scene/transform_system.cpp:11)、[Animation](D:/Code/ExtremeEngine/engine/modules/render/animation/src/components/anim_component.cpp:89)、[粒子结构准备](D:/Code/ExtremeEngine/engine/modules/render/particle_billboard/src/particle_billboard_renderer.cpp:2463)、[WorldPartition](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/world_partition.cpp:652)、[Particle Driver](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/particle_effect_driver_runtime.cpp:236)、[服务查询](D:/Code/ExtremeEngine/engine/modules/render/renderer_service/src/renderer_service_scene.cpp:735)

WorldPartition 的资源推进是逐帧状态机：Empty → DataRequested → DataReady → ResourcesRequested → ResourcesReady → Materialized。资源未就绪就保留状态下次推进，并分别控制请求与创建预算，没有给每个 Region 建立一个长期等待 IO 的 ECS Job。[状态推进](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/world_partition.cpp:555)

关于批次参数还需强调：batch 0 表示当前 view 不再切分；UINT32_MAX 也只是不额外切分该 view，不能单独保证不同 Chunk 之间串行。Transform 的 RandomReadWrite 导致 self-conflict，调度器再串起 WorkUnit，这才是此处串行的重要依据。[WorkUnit 串行链](D:/Code/ExtremeEngine/engine/modules/engine/runtime/src/ecs/scheduler.cpp:144)、[批次拆分](D:/Code/ExtremeEngine/engine/modules/engine/runtime/src/ecs/scheduler.cpp:607)

不能把 64、256、512 当成统一最佳值。应根据实体单次计算成本、Chunk 大小、随机访问比例和分配量测量。调度器中存在 Prefetch 选项，并不代表业务开启了它，也不代表任务循环已经自动 SIMD 化。原仓库基准包含顺序多列、随机访问、写密集和 mask 碎片等不同负载，适合作为后续对比维度。[基准任务](D:/Code/ExtremeEngine/engine/modules/engine/runtime/bench/ecs/ecs_world_task_scheduler_bench.cpp:202)

**12. Query、任务副本与输出生命周期的使用纪律**

RenderV2 场景初始化建立 Source 订阅和 Render Queries，帧间复用；场景还会扫描已存在的 Source 实体，避免只订阅未来消息导致旧实体漏投影。销毁时成组释放 Query。[Source Query 初始化](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_source.cpp:211)、[Render Query 生命周期](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/render_scene_backend.cpp:62)

subscribe 是初始化订阅；首次依赖 dispatch 的懒注册路径可能只完成注册而没有当次消息工作。初始化时先订阅，再让业务发消息，时序更明确。[subscribe](D:/Code/ExtremeEngine/engine/modules/engine/runtime/include/SkrRuntime/ecs/world.hpp:473)、[懒注册路径](D:/Code/ExtremeEngine/engine/modules/engine/runtime/src/ecs/scheduler.cpp:232)

另一方面，不是所有任务都缓存 Query：一次性初始扫描、部分粒子准备与 Collect 使用临时 Query，完成后销毁。合理做法是让稳定热路径复用，让临时工作有清晰作用域。

异步 ECS 任务体按值复制，并且每次执行再取自己的任务副本。因此在 run 里改普通字段，不能用来给提交方返回累计结果；任务持有的指针或引用则仍可能指向同一个共享对象。输出应明确放进外部结果缓冲区，计算期间保持有效且互不冲突。[复制与绑定](D:/Code/ExtremeEngine/engine/modules/engine/runtime/include/SkrRuntime/ecs/world.hpp:523)

当前 C# Source Generator 已提供 Span / ReadOnlySpan / Random / Optional / Message 等绑定，其字段中的引用也同样共享，不会因为生成了代码就自动变成深拷贝或线程安全。[C# 绑定契约](D:/MyGame/EarthDefense/libraries/Sugoi.SourceGen/README.md:60)

**13. CPU 任务完成与 GPU 不再使用资源，是两个不同边界**

frame_counter 在 CPU 完成图构建、执行调用和 Present 提交后完成。样例下一轮先等 pending_frame_counter 再修改模拟数据；复用 GPU frame 时还会调用 graph->wait_frame。[样例帧等待](D:/Code/ExtremeEngine/engine/samples/render_v2/render_v2_sample.cpp:1008)、[GPU 帧复用等待](D:/Code/ExtremeEngine/engine/samples/render_v2/render_v2_sample.cpp:186)

CPU 实例 ID 的复用由 scene resource record 阶段协调；GPU 上传 Buffer 的销毁还要等 graph->check_frame(retired.frame_index + RG_MAX_FRAME_IN_FLIGHT)。不能用同一个“已完成”概念覆盖两者。[GPU Buffer 回收](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/render_v2/backend_upload_buffer.cpp:102)、[实例 ID 复用测试](D:/Code/ExtremeEngine/engine/modules/render/renderer/tests/render_v2_scene_tests.cpp:1602)

用于 Godot：ECS 计算结果可在 Job 完成后交给渲染桥接；引擎对象的线程约束与资源生命周期仍由 Godot 集成层处理，不归 ECS 的组件依赖分析自动解决。

**14. 两处不应直接照搬的调用方写法**

以下是静态源码发现，未运行复现；它们说明实际调用代码也需要检查，不能把所有旧写法当成规范。

- MaterializeParticleEffectTask 把 scanned_effect_count 等统计放在任务普通值字段中，run 增加计数，但调用方 dispatch 后读取的是原始 materialize_task。结合按值提交、每批复制的实现，这些计数不会回传，相关诊断可能失效。旁边的 has_structural_changes 使用外部结果对象才具备聚合效果。[字段定义](D:/Code/ExtremeEngine/engine/modules/render/particle_billboard/src/particle_billboard_renderer.cpp:980)、[读取原变量](D:/Code/ExtremeEngine/engine/modules/render/particle_billboard/src/particle_billboard_renderer.cpp:2425)
- Transform 将 LocalTransform 作为 RandomReader 访问，却调用 cancel_dirty()；该 const 方法实际修改 mutable dirty 位。“const 接口”并不意味着真正只读。是否形成运行时竞争还取决于其他任务及外部编排，这里只指出访问语义需要核查。[调用](D:/Code/ExtremeEngine/engine/modules/render/renderer/src/scene/transform_system.cpp:60)、[mutable 修改](D:/Code/ExtremeEngine/engine/modules/render/renderer/include/SkrRenderer/scene/spatial_components.hpp:122)

对 C# 的对应约束很直接：统计结果通过明确的输出载体返回；凡是会修改组件内部状态的辅助方法，都要体现在写访问声明里。

**15. 对 Earthward 下一步的建议**

建议把战斗拆成以下数据流，而不是给每架飞机创建一个异步 Task：

1. 连续组件更新：冷却、移动、巡航状态，用 Seq 读写批量执行。
2. 构建只读空间索引或快照：明确发布完成边界。
3. 并行索敌、弹道与命中收集：读快照，写各批次独占的结果区间。
4. 按目标归并伤害：避免上百架飞机在同一帧随机写同一目标生命值。
5. 处理死亡、自爆、奖励和工厂补机：保护一次性语义，收集结构变化。
6. Staging Apply：统一处理出生、死亡和形态变更，正确重映射新 Entity。
7. 生成紧凑渲染快照：按 batch 写连续内存，在桥接边界交给 Godot。

其中“攻击者收集结果，目标侧归并写入”是面向当前游戏的建议，不是声称 RenderV2 已实现了这套战斗算法。空间分区用于降低候选比较数量；ECS 并行本身不会消除 O(N²) 索敌。

Source Generator 应继续负责访问声明、组件视图绑定、静态注册和生命周期样板；业务阶段继续以普通 C# 方法与 async/await 表达。纯 CPU 热循环使用同步方法和连续 Span，只有阶段等待、资源等待等地方需要异步。避免把“5000 架飞机”变成“5000 个常驻异步状态机”。

适合作为首批用法样例或验收场景的，应是这些来自实际 RenderV2 使用方式的组合：

- 异步生产消息后，当帧准确消费。
- 已销毁 Entity 的 Removed 通知仍能完成资源/归属清理。
- 多次 Changed 与同帧 Removed 得到确定的最终状态。
- Staging Apply 等待生产者及旧视图释放，并完成 Entity 重映射。
- Prepare 预分配输出，多个批次按 Index 独立写入。
- 外部共享对象通过阶段依赖保护，强完成句柄保持到最终消费者。
- Query 复用、临时 Query 清理、空任务完成、场景退出时工作收束。
- 分别统计计算、等待、结构提交、输出分配与渲染桥接耗时。

本次阅读最值得保留的工程原则是：让每个任务准确声明“读什么、写什么”，让每个阶段准确说明“何时输入稳定、何时输出可读、何时对象可以释放”。这三项明确之后，才有条件安全缩小等待范围并增加并行度。
