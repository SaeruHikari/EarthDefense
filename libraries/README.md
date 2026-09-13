# Sugoi C# ECS

本目录包含两套 .NET 8 运行库与一个独立编译期 Source Generator。当前方案保留 **World + StagingWorld**，恢复 ExtremeEngine 的暂存差量、Messages 和配套任务机制。普通 World 的 MergeFrom 仍是通用存储操作，已不再承担 StagingWorld 的替代职责。

| 项目 | 职责 |
|---|---|
| [Sugoi.Data](Sugoi.Data/README.md) | Entity64、原生 SoA、统一布局、组件生命周期、World/Query、buffer、实例化/导入/压实、资源引用扫描，以及真正的 StagingWorld |
| [Sugoi.Tasks](Sugoi.Tasks/README.md) | 即时调度与依赖解析、固定工作线程、可挂起的 ECS Job、弱事件/计数器、任务复制清理、Creation、原生消息环形队列和溢出块 |
| [Sugoi.SourceGen](Sugoi.SourceGen/README.md) | 静态组件/消息注册、Entity 与资源访问器、生命周期绑定、Query/Message/Creation 的强类型绑定与诊断 |

Data 只依赖 BCL，Tasks 只额外依赖 Data；Roslyn 不进入运行时依赖图。保留 Entity64、统一布局和移除组件读写锁的决定。没有 CommandBuffer，也没有序列化实现。

## 运行和验收

打开仓库根目录的 Sugoi.sln，或运行：

~~~powershell
# Release 构建、全部正确性/生成器检查、5,000 战机模拟
.\ValidateEcs.ps1

# 增加 5k/10k/50k 内核与模拟、真实 NativeAOT 发布执行、Godot 构建
.\ValidateEcs.ps1 -Stress -Benchmarks -NativeAot -GameBuild
~~~

脚本串行构建，任何一步失败即返回错误。NativeAOT 默认使用本机验证的 win-x64 / .NET 8.0.22，输出在 artifacts/sugoi/aot-win-x64；参数允许更换目标与框架版本。运行库没有 Godot 依赖，测试和样例也不加载 Godot。

## World 与 StagingWorld

World 仍然是真实存储，Create/Add/Remove/Destroy 立即执行。StagingWorld 是独立的暂存机制：按组件类型保存原生 payload 行，按实体归并结构差量，不是普通 World 的包装，也不是按时间回放命令的列表。

~~~csharp
using var runtime = new EcsRuntime();
var position = Position.Register(runtime.Types);
using var world = runtime.CreateWorld();
using var staging = new StagingWorld(runtime);

var existing = world.Create(new EntityType(position));
staging.Add(existing, new Position { X = 100 }); // 暂存已有实体更新
var pending = staging.NewEntity();              // 本轮 transient Entity64
staging.Add(pending, new Position { X = 20 });   // 暂存新实体的数据

// 等所有生产暂存数据、访问目标 World 的任务结束。
staging.Apply(world);                          // 按原协议应用差量
~~~

暂存支持普通组件覆盖、buffer 追加/替换、Add/Remove 抵消、meta、按 Query 的结构变更、递归从属销毁、UpdateFrom/ReplaceFrom、preserved types、实体引用映射及结构通知 sink。只调用 NewEntity 而没有产生记录的身份不会自动生成实体。Transient 身份仅在当前 staging epoch 内有效。

[完整暂存契约和源用例对应](Sugoi.Data/Staging/README.md)

## 即时任务与异步 Job

Dispatch 在调用时就开始派发流程：同步 Build/Prepare，随后等待显式前置条件，再在线解析组件依赖并执行就绪工作。无需先编排完整任务图或另按一次“执行”。

- 顺序读写依赖按物理 chunk；涉及 Random 的冲突等待整个任务。
- IAsyncQueryJob 与 IAsyncMessageJob 的执行体可以 await。等待期间保持依赖和 World/payload 的使用权，线程可运行其他工作。
- 固定的是工作线程数量，不是可挂起的逻辑批次数量。
- IJobCloneable<T> 与 IDisposable 管理提交快照及每批任务体。Creation 按 ref 使用同一个初始化器，不复制或释放调用方的对象。
- JobContext.Index 是实体起始位置，TaskIndex 是独立批执行序号。
- after/on-finish 的 Event/Counter 选项使用弱句柄，失效目标按原协议跳过；正在等待的目标与挂起操作由执行器保持存活。
- 可用 ActivitySource 名称 Sugoi.Tasks 监听任务名称、chunk 和批次执行范围。

异步 Job 的跨度借用必须局限于同步作用域，不能跨 await 保存 Span/ChunkView。具有可变资源所有权的异步任务使用 class 加显式 Clone/Dispose，避免 C# async struct 状态机复制 this 导致清理看不到最终状态；生成器和运行时均拒绝不安全的用法。

## Messages

每个 (消息 GUID, Query) 对应一个独立的原生 MPSC 队列。重复订阅返回原队列。主环形队列满后进入溢出块；按票据发布连续完成的消息，不能让慢复制的前一条被后一条越过。

静态 MessageRegistry 与生成模块提供 GUID/类型描述符、typed/raw 投递和 Copy/Move/Destroy。单接收者支持 move-only 消息；多接收者按原拷贝/首接收者规则处理。消费 payload 可写，允许显式搬出资源并清空原值。

消息任务的第一次懒派发在 admission 注册队列，当次不执行消息工作。消息取出发生在组件依赖等待之前；要消费某生产者刚发出的消息，需要显式 gate 到该生产者完成。ValidateMessages 默认 false，开启后再检验消费目标分组。

## 生成器

普通 Span、Entity、Optional、Shared、Chunk、Buffer、Random、消息 Consumer/Sender 和同步 Creation 都有对应生成绑定。静态注册与 visitor 在独立组件程序集消费并通过 NativeAOT 检查。复杂逻辑可手写同一套公开接口，不需要另一条私有执行路径。

[完整生成参数、消息注册和生命周期示例](Sugoi.SourceGen/README.md)

## 对照资料

- [源码与实现对应台账](sugoi-port-ledger.md)
- [当前构建、运行、AOT 和性能验证记录](sugoi-validation.md)
- [使用真正 StagingWorld 的防御模拟](../samples/Sugoi.DefenseSimulation/README.md)

此前“World 替代 StagingWorld、队列满就丢弃”的版本记录已经被本版本取代。当前验证针对 ECS 运行库及独立业务消费者；EarthDefense 游戏自身的战斗和渲染尚未迁入这些库。
