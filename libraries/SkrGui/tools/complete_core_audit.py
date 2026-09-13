from pathlib import Path
import json

p=Path('.report/core-semantics-audit.md');s=p.read_text(encoding='utf-8')
s=s.replace('Key 的字符串构造和 setter 拥有独立 UTF8 byte 副本；equal/hash 按字节；GetString 返回只读引用，GetStringMutable 对应原非 const 引用重载。','Key 采用最小 OwnedUtf8String 值适配；构造、setter、可变引用的赋值均复制输入 UTF8 bytes；equal/hash 按字节；GetString 返回只读引用，GetStringMutable 对应原非 const 引用重载。')
s=s.replace('| 源事实 | 原 C# 偏差 | 已落盘的对应修正 |','| 源事实 | 原 C# 偏差 | 已落盘的对应修正 |')
needle='`GuiAssert.Require` 增加'
idx=s.index(needle)
s=s[:idx]+'''另补 NexusSlot 默认哨兵：使用 index+1 的内部 ulong 编码，让 CLR default(T)/数组零初始化也与原 Invalid 一致；公开索引、比较顺序和 8-byte 布局不变。新增针对默认值、数组、首末有效值和大小的验证。

'''+s[idx:]
s=s.replace('已通知 render agent 用 ref readonly 对应。','render agent 已用 ref readonly 对应；最新整套回归通过。')
s=s.replace('已通知 render agent 转值类型并逐原引用位置回写，不改变排序算法。','render agent 已转为 struct 并逐原引用位置回写；原算法不变，最新整套回归通过。')
s=s.replace('## 本轮新增 17 个边界用例','## 本轮新增 18 个边界用例')
s=s.replace('- KeySourceContractTests：1 个，原字节等值、拥有副本及活引用。','- KeySourceContractTests：1 个，原字节等值、拥有副本、活引用及可变引用赋值隔离。\n- NexusSlotDefaultContractTests：1 个，default/数组无效哨兵、排序和原大小。')
s=s.replace('**以上 17 项已在正式 test 项目的独立 StructureAudit 配置全部通过，结果为 `.report/core-semantics-boundary-results.json`。**','**上述 18 项及当前全部已注册 610 项均在正式 test 项目的独立 StructureAudit 配置通过：610 passed / 0 failed，结果为 `.report/core-semantics-full-results.json`。**')
s=s.replace('最终全套结果另见 `.report/core-semantics-full-results.json`（若该文件尚未落盘，应以正在运行状态而非通过处理）。','最新全套结果为 610/610 通过，其中源案例与新增宿主/语言边界案例分开计数。')
p.write_text(s,encoding='utf-8')

p=Path('migration/core-semantics-audit.json');doc=json.loads(p.read_text())
for row in doc['files']:
    source=row['source']
    if source.endswith(('framework/nexus/nexus.hpp','framework/nexus/nexus_visual.hpp','framework/state.hpp','visual/visual_node.hpp','visual/visual_slot.hpp')):
        row['targets'].append('libraries/SkrGui.Core/Framework/BorrowedReference.cs')
    if source.endswith('framework/key.hpp'):
        row['targets'].append('libraries/SkrGui.Core/Framework/OwnedUtf8String.cs')
    row['targets']=list(dict.fromkeys(row['targets']))
doc['audit_report']='.report/core-semantics-audit.md'
doc['latest_full_test_run']={'configuration':'StructureAudit','passed':610,'failed':0,'report':'.report/core-semantics-full-results.json','note':'All currently registered source and added boundary cases; this number is not the count of original C++ source cases.'}
doc['additional_audit_cases']=18
p.write_text(json.dumps(doc,indent=2)+'\n')
