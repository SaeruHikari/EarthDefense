# Sugoi.Data

这是独立于任务库的完整存储层，只依赖 .NET BCL。运行时组件列位于 native SoA chunks 中；World/Group/Query 控制对象、定位表和查询缓存使用托管数据结构。调用方负责确定性释放 `EcsRuntime` / `World`，不依靠 GC 猜测 native 所有权。

## 对象与布局职责

| 对象 | 责任 |
|---|---|
| `EcsRuntime` | 注册表、共享三池与其下 World 的寿命 |
| `TypeRegistry` / `ComponentDescriptor` | 稳定 GUID、运行时 ComponentType、生命周期操作、兼容回调更新 |
| `ComponentLayout` / `BufferLayout` | 类型尺寸、对齐、stride、buffer 内联几何 |
| `ArchetypeLayout` / `PoolLayout` | 三种 pool 的完整容量与列偏移，只计算一次并缓存 |
| `MetadataPacking` / `BlockLayout` | Group 的组件/meta 连续 metadata 与 block header/payload 边界 |
| `World` | 定位表、逻辑分组、结构立即操作、迁移、导入与 Query 生命周期 |
| 内部 `Group` / `Archetype` / `Chunk` | 逻辑签名、物理列定义、实际内存与 full/free 分区 |
| `Query` / `ChunkView` | 匹配计划、连续范围枚举、只读或可写的借用视图 |

目标 ABI：Entity 为 8 字节；buffer header 为 24 字节（pointer/count/capacity，各 8 字节）；列 change version 为 4 字节。原生块有 64 字节 header、64 字节对齐；池总尺寸为 64 KiB、512 KiB、1 MiB。列 alignment 最大 64，表示列起点对齐；普通元素 stride 仍为类型 Size，不因更高的列 alignment 膨胀每个元素。

按 GUID 原始字节的稳定顺序排列物理列，保留参考源 Windows 有符号 char 的比较语义。tag 参与 Group 签名而不占组件列，chunk 组件每块只有一份。所有源、目标搬移地址各自通过自己的 `PoolLayout` 取得。

## Entity64 与身份

Entity 的低 32 位是 index，高 32 位是 generation。`default(Entity)` 为 Null，非空 generation 从 1 开始；0 与全 1 generation 保留；`0xFFFFFFFE` 回收后进入 1。释放与源 World 导入都会推进 generation。比较、hash、所有 visitor 与引用表使用完整 64 位身份。

身份是 World 局部的，没有隐藏 World ID。把另一 World 的原始 Entity 传来，即使数字相同也不表示同一实体。`EntityMapping` 输出按源 index 排序。导入前必须明确 source-local 与外部关系的归属。

## 生命周期

- `Create` / `CreateReserved` 批量分配连续范围并构造列；`ReserveEntities` 只保留身份，不创建实体。重复预留身份输入会在修改前拒绝。
- `Add` / `Remove` / `ChangeType` / `ApplyTypeDelta` 立即修改。相同物理布局的 tag/meta 变化可直接重挂整块。普通迁移分别执行保留列 Move、移除列 Destroy、新增列 Construct。
- Move 结束源值寿命，不再对已 Move 的源值执行 Destroy。trivial 列批量清零/复制/搬移；持有资源的 unmanaged 表示仍必须遵守其 Copy/Move/Destroy 钩子。
- PIN 实体被 Destroy 时保留所有组件、移除 meta 并加 Dead tag；默认 Query 不再匹配。清理代码移除最后一个 PIN 后才释放身份和其余数据。Dead 实体不能作为 Instantiate 模板。普通复制不复制 PIN；WithDelta 显式加入则重新构造。
- `BufferColumn<T>` 表示某一个实体的可变长度 buffer。支持内联→堆、增长、收缩、复制、搬移、清理及元素引用 remap；内联区域不会让每个实体额外分配数组对象。
- 生命周期钩子不允许抛异常或重入结构修改。破坏性导入中回调出错会使 World fault，阻止继续使用；这不是事务回滚机制。

## 查询与借用

```csharp
using var query = world.CreateQuery(new QueryDescription()
    .WithOwned(positionType)
    .Without(disabledTag));
foreach (var view in query)
{
    var positions = view.WriteOwned<Position>();
    // 一个 view 是一段物理连续范围；按 Span 处理，而非每实体字典查询。
}
```

`WithAll` 支持 meta/shared 继承；`WithOwned` 明确要求当前实体拥有物理列。`ReadShared<T>` 与 `ReadChunk<T>` 返回单值引用，普通 owned 返回 Span，buffer 使用专用访问器。可选列通过 `TryReadOwned/TryWriteOwned` 区分存在性。枚举器使用 foreach pattern，无 yield 和逐实体 boxing。

Query 支持 all/none/owned/shared/meta、disabled/dead、enabled mask、changed since、custom predicate、phase/alias writer 排除。启用 mask 的连续命中范围由 scalar/SSE2/AVX2/AVX512/AdvSimd 内核扫描；仅在当前平台支持时选用对应指令。

字符串入口 `world.CreateQuery(text)` / `TryCreateQuery` 支持真实 DSL：`[in/inout/out/atomic/has]`、`<seq/par/unseq>`、`$shared`、`?optional`、`!excluded`、注册类型名与 alias。`Query.Terms` 保留读写及访问顺序元数据。Tasks 的实际访问声明仍由 Job Build 提供，解析文字本身不自动证明隐藏访问安全。

借用 Span/ref 只能在其同步使用阶段内使用。保存旧 QueryRange 后发生结构修改，再取得 View 会报错；已经取得的 Span 是原生借用，调用方必须自行结束使用，不能跨结构修改或 await。生命周期使用租约只保证地址/定义稳定，不代替组件读写的任务依赖声明。

## 复制、导入与维护

| API | 语义 |
|---|---|
| `Instantiate` | 同 World 的单模板复制保留原引用，包括原始失效值；不复制 PIN |
| `InstantiateSet` | 每份副本单独映射集合内关系；有效的同 World 外部关系保留 |
| 跨 World `Instantiate` / `InstantiateSet` | 同 runtime；不在复制映射内的关系变 Null |
| `MergeFrom` | 导入全部已创建源实体，含 disabled/dead；预留全部目标身份后修补引用并接管块 |
| `RedirectReferences` | 只改组件、buffer、chunk 中的 Entity；不改身份和 meta 分组 |
| `CompactEntityIds` | 压紧 index、推进 generation、修补内存内引用/meta，返回新旧映射 |
| `Defragment` | 三种 pool 间以各自布局压实；保留 chunk singleton 的语义边界 |
| `ValidateMeta` / `DestroyOwned` | 清理失效 meta / 递归销毁从属实体，仍遵守 PIN 清理 |
| `TrimEmptyGroups` / `Reset` | 回收空签名 metadata / 清理数据并保留旧身份的失效历史 |

`CompactEntityIds` 后调用方持有的身份、Query meta 条件以及游戏索引需要使用返回 mapping 重绑。库不能识别业务对象中未声明的引用。

Group metadata 使用固定 1,024 个 1 KiB 槽位，超出报错；`TrimEmptyGroups` 可回收历史空签名，`Reset` 自动回收。签名本身的 metadata 必须适合单槽。内存池保留已归还块以复用；`GetDiagnostics` 给出当前实体、Group、Archetype、Chunk、容量与 native 占用，池自身也提供归还/修剪统计。

## 并发边界

普通组件访问不加 C_x_lock/C_s_lock，也没有自制原子读写锁。必要的 runtime/query/池元数据同步、World 使用期计数与 mask 位原子合并仍存在。结构操作要求本 World 无活动使用者，冲突会立即拒绝，业务需要先等待相关工作完成。永久消息订阅只保持 Query 定义，不把 World 永久锁在不可修改状态。

注册新类型可追加；兼容更新既有生命周期回调需要使用同一 runtime 的 Worlds 已静止。没有运行时 Assembly 扫描、Reflection.Emit、编译表达式树或序列化器。运行库 AOT 验证见根目录验证记录。
