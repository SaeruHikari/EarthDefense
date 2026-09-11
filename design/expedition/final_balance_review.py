from pathlib import Path
p=Path('design/expedition/progression-balance.md');s=p.read_text(encoding='utf-8')
s=s.replace('根设计案负责','总体设计案负责')
a='工业网络由r-1到r的专用工程价格为 `(矿10000,能8000,研6000,A600)×3.4^r`，不再乘`1.16^r`。r1为34k矿／27.2k能／20.4k研／2040A。以600A为例，r5价格272,612.544A，产能782.70786A/s，纯产能等待约348.3秒，入门600/2=300秒，形成温和增长而非越来越长的指数墙。'
b='工业网络由r-1到r的专用工程价格为 `(矿10000,能8000,研6000,A600)×3.4^r`，最终各资源向上取整，不再乘`1.16^r`。r1为34k矿／27.2k能／20.4k研／2040A。计算购买等待必须使用升级前产能：r0的2A/s单靠生产攒r1需1020秒；r4的237.1842A/s攒r5报价272613A约需1149.37秒。这是没有已有库存、没有战斗战利品时的17～19分钟挂机预算；正常远征由一轮战利品加制造期产出提供主要投入，不能让UI误用升级后的产能显示只等约348秒。普通低阶节点仍提供短周期升级。\n\n专用工业代际工程是明确例外：直接从超构精炼接出r1～r6成长主干，r1在首次占领后开放，以后要求已有r-1且n≥r。它不要求先买完工业分支前十个节点，也不受目录中第11个位置默认era_tier=5限制。其作用、价格与门槛由专用工程定义覆盖，避免第一次胜利后看得到工业升级却要到第五星才能购买。'
assert a in s;s=s.replace(a,b,1)
p.write_text(s,encoding='utf-8')
p=Path('design/expedition/design-review.md');s=p.read_text(encoding='utf-8')
s=s.replace('审查范围：proposal.md、world-and-combat.md、save-audit.md；progression-balance.md 正在编写，数值分项与旧科技映射将在其落盘后补审。','审查范围：proposal.md、world-and-combat.md、save-audit.md；数值细案完成后，追加了 progression-balance.md 的节点计数、旧 ID 映射、预算和关键前置口径检查。')
a='截至此复核，progression-balance.md 尚未落盘；启动总额算术通过，细分节点/前置/配方成本和旧科技精确补偿仍以其后交付的数值细案为准，不能把本报告当作该表已逐项验证。若后续细案与已锁定的 3 核 Mk III、8 核 Mk IV、首链 1850 合金或 28 参数继承冲突，需要继续更正。'
b='数值细案完成后已追加离线核验：[document-checks.json](document-checks.json) 确认阶段一24节点／72级、旧56个ID恰好映射一次且与真实目录集合相同、阶段二72节点／标称504级、全部文档本地链接存在。首链含首次出航、两座预装工厂与MkII武装，预算为168k／113k／70k／1850A；3核／8核永久许可、6格工厂占地、方舟舰体与飞机代际命名、120秒恢复和局部计时口径已统一。静态映射计数不代表迁移效果已经由代码实测；正式前置DAG、每级效果向量及完整战斗平衡仍属于后续实现验收。\n\n### R7 / 已解决：工业升级等待不能使用升级后的产能\n\nr1工程从r0的2A/s攒2040A，纯生产等待是1020秒；r5从r4产能购买约需1149.37秒。已把原文以升级后产能计算的348秒解释改正，并明确实际投资由战利品与在途产出支撑。另将工业r1～r6工程列为从精炼直接接出的主干，r1首占后可购，不被工业表第11个位置的默认era5错误锁到后期。'
assert a in s;s=s.replace(a,b,1)
p.write_text(s,encoding='utf-8')
print('Final balance and review corrected; all results remain design-only.')
