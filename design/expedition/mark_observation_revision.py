from pathlib import Path
note='> **最新需求修订（2026-09-10）：** 观测、反扑、航行、地球持续防御、存档库优先级和首版资产范围，现以 [v0.2 理解报告](observation-expedition-understanding.md) 为准。本文件保留此前设计内容；与最新报告冲突的条目不作为本轮实现约定。\n\n'
for name in ('proposal.md','world-and-combat.md','progression-balance.md','interface-design.md'):
    p=Path('design/expedition')/name
    s=p.read_text(encoding='utf-8-sig')
    if '**最新需求修订（2026-09-10）：**' not in s:
        first,rest=s.split('\n',1)
        s=first+'\n\n'+note+rest.lstrip('\n')
        p.write_text(s,encoding='utf-8')
p=Path('design/expedition/observation-travel-review.md')
s=p.read_text(encoding='utf-8-sig')
first,rest=s.split('\n',1)
s=first+'\n\n> **用户回复更新：** 已明确“只揭露被观测星球，其他星球各自观测”。下文初审中的这一歧义已解决；其余时间和结算细节仍按建议解释标注。最终综合口径见 [理解报告](observation-expedition-understanding.md)。\n\n'+rest.lstrip('\n')
p.write_text(s,encoding='utf-8')
print('Earlier design documents marked with the latest requirements; confirmed per-planet answer recorded.')
