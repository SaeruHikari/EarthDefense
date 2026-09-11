from pathlib import Path
p=Path('design/expedition/world-and-combat.md');s=p.read_text(encoding='utf-8')
s=s.replace('初始工厂占一格；大型支持模块可占两格。','舰载工厂默认占 2×3＝6 格；精炼线占 1×2＝2 格；基础护盾模块占 2×2＝4 格。机型改装优先沿用原占地，若某模块需要扩建，先预览并检查周围空格，成功后才扣费。')
p.write_text(s,encoding='utf-8')
p=Path('design/expedition/proposal.md');s=p.read_text(encoding='utf-8')
s=s.replace('工厂、能源、维修、护盾和增幅模块会影响甲板布局。','舰载工厂默认占 6 格，精炼占 2 格，护盾模块占 4 格；工厂、能源、维修、护盾和增幅模块会影响甲板布局。')
a='第一阶段仍保留小 Boss 核心'
s=s.replace(a,'大树的每个等级都要有可预览的实际收益。成本预览、将军危险预警、基本调度和存档等基础操作直接提供；不能把“看清 UI”变成十级付费科技。情报类研究的多级成长应改善弱点窗口、先手部署或侦察收益；无法定义有意义增益的节点改为单级。约 500 级是内容容量预算，不是必须用重复升级凑齐的指标。\n\n'+a,1)
s=s.replace('- [已完成的交战 CPU 调查]','- [设计一致性审查](design-review.md)：首战恢复、阶段时钟、保存语义与首占事务边界。\n- [已完成的交战 CPU 调查]',1)
p.write_text(s,encoding='utf-8')
p=Path('design/expedition/check_design_math.py');s=p.read_text(encoding='utf-8')
a="worlds=[('moon',1.0,1.0,1.0),('mercury',.90,1.15,1.0),('venus',1.10,.90,1.0),('mars',1.15,1.05,1.05),('europa',1.0,1.0,1.02),('titan',.85,1.10,.98)]"
b="worlds=[('moon',1.0,.90,.95),('mercury',.90,1.15,1.10),('venus',1.10,.90,1.05),('mars',1.15,1.0,1.10),('europa',1.05,1.05,1.10),('titan',.85,1.10,1.0)]"
assert a in s;s=s.replace(a,b,1);p.write_text(s,encoding='utf-8')
print('Footprints, core information access, and scenario inputs aligned.')
