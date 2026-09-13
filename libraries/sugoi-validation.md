# Staging / Messages / Jobs 验证记录

日期：2026-09-13。此记录取代此前普通生成 World 方案的验证记录。所有结果来自当前源码重新构建，旧 tests/Ecs* 产物没有参与测试。

## 可复现入口与日志

~~~powershell
.\ValidateEcs.ps1 -Stress -Benchmarks -NativeAot -GameBuild
~~~

- [完整构建、测试、压力和模拟日志](../artifacts/sugoi/validation-staging-final.log)
- [包含最后阶段可见性回归的 NativeAOT 发布及执行日志](../artifacts/sugoi/aot-staging-final.log)
- 原生程序：artifacts/sugoi/aot-win-x64/SugoiChecks.exe

artifacts 是生成输出目录，不作为源文件管理。测试入口在 tests/SugoiChecks/Program.cs，所有新用例已接入运行器，不是仅编译而未执行。

## 结果

| 检查 | 当前结果 |
|---|---|
| Sugoi.sln 的 7 个项目 Release 构建 | 0 warning / 0 error |
| Data / Tasks / 原及新增运行检查 | JIT 全部通过 |
| Source Generator 基础检查 | 32 项通过 |
| Source Generator 高级检查 | 43 项通过 |
| 跨程序集组件、消息、Query/Message/Creation 绑定 | JIT 与 NativeAOT 通过 |
| win-x64 NativeAOT 发布 | 成功，无 AOT/trim 警告 |
| 最新原生 exe 执行全部运行检查 | 全部通过，dynamic-code=False |
| Earthward.csproj Debug 构建 | 0 warning / 0 error |

环境为 Windows x64、AMD Ryzen 9 9950X3D（16 核 / 32 逻辑线程）、SDK 10.0.100、.NET 8.0.22。测试的执行器使用 4 workers；专门的挂起测试使用 1 或 2 workers。

JIT 实际 mask 模式为 AVX512，NativeAOT 默认可移植 x64 基线为 SSE2。本机对支持的 scalar/SSE2/AVX2/AVX512 路径做一致性检查；没有在 ARM64 设备上执行。Data/Tasks 的依赖仍仅为 BCL 和 Data，Roslyn 只运行在编译期。

## 本轮新增的行为证据

**StagingChecks** 移植原 ecs_staging_tests.cpp 的全部 16 个用例，涵盖不连续实体、4096 并发生产、transient 普通/buffer/meta 引用、Update/Replace、批引用映射、保留例外、递归从属销毁、容量增长和三类 sink 时点。还增加增删抵消、Query 在生成之前执行、Move-only 与 heap buffer 所有权、暂存复用、失败边界和资源扫描。

**MessagesParityChecks** 核对 native ring/overflow/wrap、慢复制时的票据发布、GUID/typed 幂等注册、pending、Move-only 首接收者、稳定副本广播、raw Copy、可写消费和 Move-out、Prepare 的实体数、lazy admission、Validate 默认值、无效前缀的 payload 偏移，以及 async 消息任务与 payload 清理。

**JobParityChecks** 验证 Job 体内 await 时其他 World 能运行、后继冲突任务等待真实完成、World/Staging.Apply 使用权保持、独立 TaskIndex、提交及每批拥有对象的 Clone/Dispose、异常释放、async class 的最终状态清理、复用 Query meta、显式列表绕过 Query 筛选、完整预取条件与 Activity 名称。

另有两个重要边界已在 JIT 和 NativeAOT 中执行：

- **16 个异步 ECS 批次在 2 个 worker 上同时抵达 barrier**，证明限制的是线程预算而不是逻辑挂起数量。
- **Creation 回调等待另一 World 的任务结束**，证明回调不持有调度器元数据锁；ref 初始化器的最终状态正确返回。
- **预热 shared 缓存后，在同一次 Apply 的 Query 阶段移除 owner 组件，后续 sink 立即观察到变化**，验证阶段内缓存失效。

**WeakSignalChecks** 强制 compact GC，核对四类弱选项过期时跳过、等待者保持 owner、执行器持有挂起操作强根，以及只有弱外部句柄时等待仍能被唤醒。

**GeneratedAdvancedChecks** 实际运行全部自动访问 wrapper、消息注册/发送/消费、async、JobClone/JobDispose、同步 Creation/reserved/ref 状态、资源 GUID 的嵌套/inline array/buffer/chunk/custom 扫描。生成器反例包含不安全 owning async struct 的编译期拒绝。

已有 World、Transfer、Packing、SIMD、buffer、并发提交、MPSC、关闭和异常测试继续通过。

## 固定实体内核

Movement → Combat 两阶段，batch 512，预热 16 次、测量 120 次。固定实体数量不变，最终每个实体的位置和生命值均校验。以下是完整脚本那次运行的墙钟延迟：

| 模式 | 实体数 | median | p95 | 托管字节 / step |
|---|---:|---:|---:|---:|
| JIT | 5,000 | 0.109 ms | 0.184 ms | 15,080 |
| JIT | 10,000 | 0.133 ms | 0.207 ms | 15,089 |
| JIT | 50,000 | 0.373 ms | 0.503 ms | 41,205 |
| NativeAOT | 5,000 | 0.065 ms | 0.109 ms | 14,363 |
| NativeAOT | 10,000 | 0.070 ms | 0.113 ms | 14,357 |
| NativeAOT | 50,000 | 0.094 ms | 0.145 ms | 38,415 |

对应区间 GC 计数为 0/0/0，但托管分配不为零，不能推导长时间运行不会 GC。JIT 的短预热、系统负载和编译策略影响比较，不宣称普遍的 AOT 加速倍数。

## 使用真实 StagingWorld 的防御模拟

生成器现在向 StagingWorld 写入组件行和 transient 引用，Apply 后由 BirthTicket sink 找到正式身份，再绑定外部工厂、取消失效工厂的未发布出生。没有用普通 World/MergeFrom 充当暂存替身。

每档初始总战机数为下表数字，测量 120 步，包含运动、目标快照、有限候选攻击、伤害归并、死亡与奖励去重、暂存生成/Apply 和补机：

| 初始战机数 | median / step | p95 / step | 进程 CPU / step | 托管字节 / step |
|---:|---:|---:|---:|---:|
| 5,000 | 0.713 ms | 4.172 ms | 1.562 ms | 377,968 |
| 10,000 | 1.026 ms | 7.211 ms | 3.385 ms | 634,423 |
| 50,000 | 3.454 ms | 34.946 ms | 12.500 ms | 3,059,736 |

模拟期间会死亡和补充，表中是初始人口，不能当成始终同时存活的数量。5k 档共发布 17,063 架，其中补充 12,063；取消 4 个失效工厂的未发布出生；应用 92,197 次唯一命中；奖励 98,810，等于 9,881 次敌军唯一死亡乘以 10。

50k 场景的生成/Apply/结算峰值仍明显，p95 并未达到稳定高帧率目标；本轮以恢复源机制与行为验证为重点，没有据此宣称真实游戏 50k 战机流畅或零分配。新的生成协议与实体分配顺序也不同于旧示例，不把两次模拟数字当作严格性能 A/B。

## 验收边界

本轮完成两套 ECS 运行库及配套 Source Generator 的机制迁移和独立消费者验证。没有运行原 C++ 与 C# 的逐指令二进制差分，也没有跨平台穷尽测试；必要的 C# async/弱引用适配和源缺陷修正均在台账中明确列出。

EarthDefense 原游戏构建继续通过，游戏的现有战斗与渲染尚未迁入新 ECS。上面的毫秒数是独立 ECS 场景，不能当作 Godot FPS。
