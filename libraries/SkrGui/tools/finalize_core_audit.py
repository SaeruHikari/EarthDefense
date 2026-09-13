from pathlib import Path
p=Path('.report/core-semantics-audit.md');s=p.read_text(encoding='utf-8')
start=s.index('## 必须在最终汇总中单独说明的生命周期边界')
s=s[:start]+'''## 生命周期与语言边界

源 Visual/Nexus/BuildScope/Batch 持有物采用 RC，C# 普通对象引用没有自动对应“最后一次 RC 释放时立刻析构”的时刻。root 明确采用：普通对象由共享引用/GC 保持活性，拥有原生资源的对象提供 Dispose/finalizer，不引入改变所有公开 API 的 RC<T>。State 的原虚拟析构已由 NexusComponent.OnDestroy 调用 Dispose。VisualTempText 原默认析构隐式释放 RC<TextParagraph>，由 text agent 补对应原生资源生命周期清理；不能在 NexusVisual.OnDestroy 中无条件销毁仍由 VisualOwner 或其他外部强引用持有的 Visual。

源非拥有指针已在指定结构范围恢复为内部 BorrowedReference<T>（WeakReference）：Nexus.ParentNode、NexusVisual._ancestorVisualNexus、State.AttachedNexus、VisualNode._parent/_owner、VisualSlot.AttachedNode。所有公开 getter/参数仍使用原对象类型，不把弱引用泄漏到业务 API。BuildScope.owner 之前已是 WeakReference。活树的向下拥有关系继续使用强引用。

正常结束路径逐源核对未改变：Nexus.DestroyInternal 清 ParentNode/CurrentWidget/Scope；NexusComponent 清 State.AttachedNexus；NexusVisual 清 ancestor/visual；VisualOwner.RemoveRoot 递归 DetachOwnerSubtree，先清 _owner 再调用 OnDetachOwner。外部仍持有子节点也不能保活失去拥有者的 ancestor/owner。没有把 GC 析构时间说成 C++ 末 RC 同步析构时间。

## 本轮新增 17 个边界用例

- WidgetDslSourceContractTests：2 个，方法发现与异常/非 void 约束。
- KeySourceContractTests：1 个，原字节等值、拥有副本及活引用。
- StructureBorrowContractTests：4 个，布局/命中失败输出、活 readonly 值借用和集合借用。
- FilesFontSourceContractTests：4 个，标量/表/名称/face 失败哨兵与目录只读借用。
- FilesFontUtf8ContractTests：1 个，无法被 UTF16 安全代替的原 byte 家族名及目录拥有副本。
- RawBorrowGcContractTests：5 个，child 不拥有 Visual parent/owner、slot 不拥有 node、State 不拥有 Nexus、Nexus child 不拥有 ancestor/BuildOwner，以及活树继续保留 child/state。

**以上 17 项已在正式 test 项目的独立 StructureAudit 配置全部通过，结果为 `.report/core-semantics-boundary-results.json`。** 该配置避免与其他 agent 的 Debug DLL 同时写入；没有为测试另造实现或 stub。先前的 Debug 定向运行曾因共享 Core.dll 编译锁失败，现已用独立配置完成实际验证。

此前原结构 142 个、shape 50 个、VisualTempText 12 个均通过；50 个 shape 在 sampler struct/ref 修正后也复测通过。最终全套结果另见 `.report/core-semantics-full-results.json`（若该文件尚未落盘，应以正在运行状态而非通过处理）。补充语言边界检查不计入原始 558 个 source cases。
'''
p.write_text(s,encoding='utf-8')
