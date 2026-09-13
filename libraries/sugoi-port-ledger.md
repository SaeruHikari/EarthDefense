# Sugoi C# 源码对应台账

当前版本恢复 **StagingWorld + Messages + 配套任务机制**。此前普通 World 替代暂存的决定已撤销。本文件覆盖有效源码中的行为；旧注释、空文件或未被调用的声明不被当作已实现功能。

参考代码：D:/Code/ExtremeEngine/engine/modules/engine/runtime。保留的目标差异仍是 C# 职责类、Entity64、统一布局、删除组件锁/自制原子读写锁、Source Generator 和不迁移序列化。

## 三个子系统的实际对应

| 原始来源 | C# 实现 | 对应行为与验证 |
|---|---|---|
| src/ecs/staging.cpp、include/SkrRuntime/ecs/staging.hpp | Data/Staging、World.Staging | 原生组件行、record 归并、transient、Add/Remove/meta、Query 变更、Update/Replace、Apply、sink；StagingChecks 移植全部 16 个源用例 |
| src/ecs/messages.cpp、include/SkrRuntime/ecs/message.hpp | Tasks/Messaging | native ticket ring、OverflowBlock、幂等订阅、GUID/typed/raw、Move/Copy/Destroy、pending、直接/线性化消费；MessagesParityChecks |
| src/ecs/scheduler.cpp、include/SkrRuntime/ecs/scheduler.hpp | Scheduler、DependencyAnalyzer、Submission、Prefetch | 实时 admission、读写依赖、chunk/whole-task fences、实体/消息工作范围、预取、完成清理；TasksChecks/JobParityChecks |
| include/SkrRuntime/ecs/world.hpp 中的任务模板 | JobContracts、AsyncJobContext、CreationJobContracts、Scheduler.Creation、SourceGen | 强类型访问、消息 sender/consumer、Prepare、ref Creation、独立 TaskIndex、名字、批次复制/清理 |
| 原 task scheduler / fiber、counter/event | WorkerExecutor、JobEvent/Counter、WeakJobSignals | 固定线程上挂起/恢复、超过 worker 数量的逻辑批次、共享等待、弱选项、挂起操作强根；WeakSignalChecks/ThreadRaceChecks |
| C++ 模板、类型注册和资源扫描 | Sugoi.SourceGen | 组件/消息静态模块、Entity visitor、GUID resource scan、Query/Message/Creation binder、生命周期诊断；跨程序集 Fixtures/GeneratorChecks |

src/sugoi/staging_world.cpp 是空文件，不再将其误列为有效暂存实现。普通 World.MergeFrom、Instantiate、Defragment、CompactEntityIds 继续作为通用存储能力保留，示例生成流程已经使用真正 StagingWorld。

## StagingWorld 的协议

1. 按实体索引定位本 epoch 的 record，按组件类型分配原生 payload 行；已有实体和 transient 使用独立 lookup。
2. 普通组件重复 Add 覆盖此前暂存值；buffer Add 追加、SetBuffer 替换；Add/Remove 相互抵消。meta 与 Query 结构变更也归并。
3. Apply 按源阶段顺序执行：Query 范围结构变更 → 预留生成身份 → 修补 transient 引用 → 生成 → 已有实体差量/数据 → 收集并销毁实体及从属树 → 清理本 epoch。
4. Query 阶段不看本轮新生成实体，也不冒充普通 component sink 通知。普通移除通知观察移除前 payload，增加通知观察应用后 payload，销毁通知发生在实际删除前。
5. UpdateFrom 保留目标额外组件；ReplaceFrom 删除未被 preserved-types 保留的目标多余普通组件。引用映射与批量源引用按原规则处理，不能以 MergeFrom 代替更新。
6. Transient 使用 generation=FFFFFFFF、index 仍不得为 FFFFFFFF。只 NewEntity 不创建 record；未记录 transient 被引用会报错。Apply/Clear 后 epoch 重置，旧 transient 不能跨 epoch 使用。
7. 多生产者共享记录/行需要粒度受控的 BCL 同步；Apply/Clear 为独占阶段，不允许与生产者或目标 World 任务交错。

初始化容量是 hint，可以增长。payload 行几何由 StagingRowLayout 统一计算：目标块 256 KiB，records-per-block 按源界限钳制；buffer 仍通过同一套 ComponentOps/BufferOps 管理内联与堆所有权。

[完整源用例逐项对应](Sugoi.Data/Staging/README.md)

## Messages 的协议

| 曾有的差异 | 当前处理 |
|---|---|
| 固定容量满后直接丢消息 | native ring 满后分配 overflow blocks；只有分配失败才丢弃有效消息 |
| ConcurrentQueue 完成先后代替票据顺序 | reserve ticket + published ticket，消费连续已发布 ring 前缀 |
| 同一 type/query 重复创建订阅 | 按 GUID/query 幂等返回同一队列 |
| 只有泛型类型键和 Copy/Destroy | 静态 GUID 描述符、typed/raw 投递、Move/Copy/Destroy、move-only 首接收者规则 |
| Prepare 接收消息条数 | 恢复 Query 实体数 |
| 任务固定开启消费校验 | ValidateMessages 默认 false，按配置做 group 校验 |
| 派发前必须显式订阅 | 恢复 admission 时懒注册，第一次仅注册且无消息 work |
| payload 只读 | 可写 native consumer span，支持搬出资源并清空原值 |
| 消息绑定需要手写 | MessageJob / MessageConsumer / MessageSender 自动生成 |
| 每次消费都先 List/ToArray | 连续 ring 可以直接 Visit；wrap/overflow 使用原生线性化与拥有式批次 |

消息物化仍发生在组件依赖等待之前。需要读取某生产者新消息时，必须将 admission gate 显式放在该生产者之后。发送筛选使用完整 Query；消费期可选 group 校验。无订阅者不取得 payload 所有权。

## Job 与生成器的协议

- Build/Prepare 在调用线程同步执行，after gates 在其后。没有“先编排全图，再整体发射”。
- Seq/Seq 冲突以物理 chunk 等待；任何 Random 配对等待整个任务。每块保存独立 writer/readers 历史。
- IAsyncQueryJob/IAsyncMessageJob 在整个 await 期间保持 ECS 依赖、payload 与 World 使用权。执行器强持有挂起操作，线程可执行其他逻辑批次。
- Async For 提交全部逻辑批次；线程预算固定但同时挂起的批次数量不受 workerCount 限制。16 批/2 线程 barrier 已列为回归。
- 提交快照与每批副本分别 Clone/Dispose。Creation 用同一个 ref initializer，在每个 native range 构造后同步回调，不复制/释放调用方初始化器。
- 原独立 task_index、entity_start_index、任务名均有对应。ActivitySource 提供批次追踪。
- 复用 Query 会应用本次 Build meta；显式实体入口按原路径绕过 Query 筛选，保留顺序与重复身份。
- Source Generator 覆盖 Owned、Optional、Shared、Chunk、Buffer、Random、Entity、MessageConsumer/Sender、async context、Creation、组件与消息生命周期以及资源 GUID 访问。
- 预取计划在 admission 构建：默认 Off，Auto 的 128 实体/4 stream/非显式列表/非小 batch 条件，以及 Force 的全任务提升、身份列和两行提示均对应源实现。

## 存储与明确修正

三池、Group/Archetype 分离、GUID 列序、SoA、buffer、PIN、批量生命周期和 SIMD mask 继续沿用 Data 实现。布局只有一个计算归属：ComponentLayout、BufferLayout、ArchetypeLayout/PoolLayout、StagingRowLayout、MessageLayout、MetadataPacking/BlockLayout 各自负责实际消费者使用的布局。

已确认问题采用局部修正，不复制未定义行为：

- 列搬移使用源/目标各自 pool offsets；chunk singleton 先预留单份空间。
- PIN dead 保留普通数据直到最后 PIN 清理；单例拆分复制、最后实体搬移转移所有权。
- 保留 disjoint writer 历史，并将 pure RandomWriter 和 chunk writer 的自身数据竞争串行化。
- 暂存检查完整 Entity64 generation；Update/Replace 提前验证根映射；meta remap 后排序去重。
- 结束目标旧/default 拥有值后再转移 payload；buffer 以真实 heap 所有权判断，不能仅按当前 size 推断内联。
- 一次 Apply 的 Query 阶段先快照匹配组，再变更；阶段之间失效结构/shared 缓存，使后续 sink 观察到正确状态。
- 消息跳过失效/不匹配目标时保留原 payload offset，避免把后一条消息绑定给错误实体。
- Apply/生命周期回调违反 no-throw 或破坏性转移失败会 fault；预检查失败保留重试机会，不宣称事务回滚。

## 必要的 C# 适配

- NativeAOT 使用静态描述符和 Source Generator 替代 RTTR/模板绑定，不依赖运行时反射扫描或动态编译。
- C# async body不能保存 Span/ref struct 跨 await；使用稳定 context 并在同步作用域借用数据。
- 具有可变资源所有权的 async struct 无法保证 this 状态机副本回写给清理者，因此使用 async class + Clone/Dispose；编译器和运行时均拒绝危险组合。
- 弱选项使用 BCL WeakReference，过期时点由 .NET GC 决定；等待已取得的状态与挂起任务有强根。保持有效生产者句柄，取消使用显式 CancellationToken。
- 各种业务与结构所有权需要明确等待边界。C++ 的组件锁和自制原子读写锁没有回流，必要的元数据、暂存、队列与结束同步仍然存在。
- C++ 只在注释中提到的 message coalescing、原 sugoi 没有消费者的 add_child，不虚构成另一套运行行为；消息 accumulation 应由业务消费实现。

这里交付的是源码机制对应的 ECS 库与消费者；尚未执行原 C++ 和 C# 的二进制逐步差分或全平台验证。当前通过的具体测试、AOT 与实测数据见验证记录，不能用“能运行”代替行为覆盖清单。
