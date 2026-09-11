"""Generate and audit a DESIGN-ONLY technology catalog. No game files are changed."""
from pathlib import Path
import json
import math

ROOT = Path(__file__).resolve().parent
FAMILIES = {
    'K': ('动能', [('弹芯加工','动能伤害','%',8),('枪机循环','动能射速','%',4),('电磁加速','动能弹速','%',8),('轻甲破片','对轻甲伤害','%',6),('枪口延伸','动能武器射程','%',4)]),
    'M': ('导弹', [('战斗部装药','导弹伤害','%',8),('挂架装填','导弹射速','%',4),('爆破整形','导弹爆炸半径','%',4),('导引舵机','导弹转向速度','%',8),('助推推进','导弹速度','%',8)]),
    'L': ('光束', [('晶体增益','激光伤害','%',8),('脉冲循环','激光射速','%',4),('光学准直','激光武器射程','%',4),('镜组转轴','激光瞄准转速','%',8),('共振校准','对能量层伤害','%',6)]),
    'I': ('工业科研', [('矿物分离','矿物设施产量','%',8),('能源转化','能源设施产量','%',8),('科研方法','科研设施产量','%',10),('机库扩建','工厂编制点','点',1),('装配节拍','工厂生产速度','%',5)]),
    'D': ('防御生存', [('机体加固','战机最大耐久','%',8),('行星蓄能','地球护盾上限','%',8),('维修单元','维修速度','%',8),('殉爆装药','战机自爆原始伤害','%',8),('殉爆整形','自爆半径','%',4)]),
    'C': ('航程指挥', [('巡逻区划','球面巡航半径','%',8),('远端拓展','球面巡航远端范围','%',8),('巡航推进','巡航速度','%',6),('矢量机动','战术转向速度','%',6),('返航推进','返航速度','%',8)]),
}
NAMES = {
 'K': [('N1','扫荡机编制','解锁 K2 扫荡机'),('N2','猎隼机编制','解锁 K3 猎隼机'),('N3','远距点射','动能武器射程 +25%'),('A1','近防识别','允许动能机用最多25%火力拦截可拦截鱼雷和孢囊'),('A2','脉冲压制','动能正伤命中轻甲延迟其开火准备，每秒最多0.2秒'),('A3','失盾追猎','能量层首次被击破后的3秒，动能对该目标船体伤害 +40%'),('G1','超导枪列','持续开火2秒后，动能伤害 +100% 持续3秒；触发后冷却8秒'),('G2','行星近防网','可用于拦弹的火力预算上限由25%升至50%；被威胁设施可请求邻厂近防支援')],
 'M': [('N1','制导武装','解锁 M1 破甲导弹机'),('N2','轰击机编制','解锁 M2 震荡轰击机'),('N3','鱼雷机编制','解锁 M3 破城鱼雷机'),('A1','近炸引信','导弹进入目标近炸距离即可结算一次主爆，减少掠过失误'),('A2','甲壳震裂','有效导弹命中重甲形成2秒裂口，动能对重甲系数由0.20临时变为0.35'),('A3','饱和锁定','同一重甲目标承受第3次有效主弹后，该次伤害 +60%；每目标冷却3秒'),('G1','破城制导','导弹对大型重甲目标的主命中伤害 +100%'),('G2','轨道火力链','三座不同导弹工厂在2秒内命中同一大型目标时，第三次主命中伤害 +200%；每目标冷却8秒')],
 'L': [('N1','聚能激光','解锁 L1 辉光激光机'),('N2','长矛机编制','解锁 L2 灼盾长矛机'),('N3','光脉冲机编制','解锁 L3 光脉冲压制机'),('A1','光盾卸载','首次击破目标能量层时造成0.4秒失衡，Boss半效，每条生命一次'),('A2','频谱封锁','正伤激光命中后，目标能量层恢复被延后2秒'),('A3','冷启动镜组','切换到新的能量层目标时首发伤害 +60%，冷却3秒'),('G1','相位解构','激光对能量层伤害 +100%'),('G2','光蚀场','首次破盾位置产生2秒局部场，场内敌人暂停能量层恢复；每目标每条生命只触发一次')],
 'I': [('N1','精密核心接口','单颗资源核心对所在设施的产量增益由5%变为7%'),('N2','科研设备','研究所产量 +25%'),('N3','自动排产','工厂生产速度 +20%'),('A1','遗迹催化','新资源设施首次投产后的20秒产量 ×2，单设施只触发一次'),('A2','科研回响','实际支付科研后10秒返还该笔科研的15%，返还不再次触发'),('A3','战利精炼','击杀的矿物奖励 +40%'),('G1','行星计算阵列','科研设施产量倍率 ×2'),('G2','并行产线','每座工厂可同时推进两个缺额编制的生产，仍共享总编制上限和出舱安全间隔')],
 'D': [('N1','复合机体','战机最大耐久 +20%'),('N2','快速维修','战机维修速度 +30%'),('N3','护盾整流','地球护盾恢复速度 +25%'),('A1','反应护层','每架新机首次实际受击伤害 -60%，维修不刷新'),('A2','纳米急救','首次跌至30%耐久时恢复15%最大耐久，每条生命一次'),('A3','牺牲转换','自爆造成的正伤害有10%转换为地球护盾；全军每秒最多恢复护盾上限的2%'),('G1','行星屏障','地球护盾上限 ×2'),('G2','零界护盾','地球护盾耗尽后若仍存活，重建25%最大护盾；重建护盾2秒免伤，冷却30秒，不免疫船体伤害')],
 'C': [('N1','攻坚测距','对大型目标的武器射程 +20%'),('N2','远域巡航','解锁真实空间的战略巡航及接敌减速模式'),('N3','轮替接敌','允许每厂保留一个已就绪编制接替返修或回航机的警戒位置，不额外生成战机'),('A1','应急加力','从巡逻转入拦截时移动速度 +50% 持续3秒，冷却10秒'),('A2','深空补给','太空最大行动中心半径提升到50，适配44半径母舰'),('A3','外环补给','太空最大行动中心半径提升到84，适配76半径母舰'),('G1','母舰攻坚','母舰成为可攻击目标；太空最大行动中心半径提升到12'),('G2','星环航程','太空最大行动中心半径提升到128，适配116半径母舰')],
}

def node(id, name, size, branch, effects, science, alien=0, requires=None, wave=0, stage=0):
    return dict(id=id, name=name, size=size, branch=branch, effects=effects,
                cost=dict(science=science, alien_points=alien), requires=requires or [],
                unlock=dict(completed_wave=wave, defense_stage=stage), ranks=1)

nodes=[]
for b,(branch,stats) in FAMILIES.items():
    for tier in range(4):
        for j,(name,attr,unit,base) in enumerate(stats):
            ix=tier*5+j+1
            req=[f'{b}_S{ix-5:02d}'] if tier else ([] if b not in ('M','L') else [f'{b}_N1'])
            if unit=='点': value=[1,1,2,2][tier]
            else: value=base*[1,1.5,2,3][tier]
            effect=f'{attr} {value:+g}{unit}'
            n=node(f'{b}_S{ix:02d}',f'{name} {"ⅠⅡⅢⅣ"[tier]}','small',b,[effect],[8,8,10,10,12][j]*4**tier,requires=req,wave=10 if tier==1 else 0,stage=max(0,tier-1))
            n['effect_definition']=dict(attribute=attr,operation='add_percentage' if unit=='%' else 'add',value=value)
            nodes.append(n)

for b, entries in NAMES.items():
    for code,name,description in entries:
        if code.startswith('N'):
            ix=int(code[1]); cost=[80,260,1100][ix-1]
            req=[f'{b}_S01',f'{b}_S02'] if ix==1 else [f'{b}_N{ix-1}',f'{b}_S{6 if ix==2 else 11:02d}']
            wave=0 if ix==1 else 10; stage=1 if ix==3 else 0
            if b=='M' and ix==1: req=['K_S01']; cost=60
            if b=='L' and ix==1: req=['M_N1','I_S03']; cost=220; wave=10
            if b=='K' and ix==1: wave=8
            if b=='K' and ix==2: wave=22
            if b=='M' and ix==2: wave=18
            if b=='L' and ix==2: wave=24
            nodes.append(node(f'{b}_{code}',name,'medium',b,[description],cost,requires=req,wave=wave,stage=stage))
        elif code.startswith('A'):
            ix=int(code[1]); req=[f'{b}_S01'] if ix==1 else [f'{b}_A{ix-1}',f'{b}_S{6 if ix==2 else 11:02d}']
            nodes.append(node(f'{b}_{code}',name,'alien_medium',b,[description],[18,160,800][ix-1],[1,3,5][ix-1],req,stage=ix-1))
            nodes[-1]['unlock']['first_medium_boss_defeated']=True
            if ix==2 and b!='C':
                nodes[-1]['unlock']['defense_stage']=0
                nodes[-1]['unlock']['completed_wave']=15
            if ix==3 and b!='C': nodes[-1]['unlock']['defense_stage']=1
        else:
            ix=int(code[1]); first_cost=8 if b=='L' else 4; last_cost=16 if b=='C' else 12
            req=[f'{b}_N1',f'{b}_A1',f'{b}_S06'] if ix==1 else [f'{b}_G1',f'{b}_A3',f'{b}_S16']
            effects=description.split('；')
            nodes.append(node(f'{b}_{code}',name,'large',b,effects,(420 if b=='C' else (600 if b=='L' else 300)) if ix==1 else 4800,first_cost if ix==1 else last_cost,req,wave=10 if ix==1 else 0,stage=(3 if b=='C' else 2) if ix==2 else 0))

# Preserve the armor and support teaching sequence before mother-ship assault.
next(n for n in nodes if n['id']=='C_G1')['unlock']['completed_wave']=24
lookup={n['id']:n for n in nodes}
assert len(nodes)==len(lookup)==168
assert sum(n['size']=='small' for n in nodes)==120
assert sum(n['size']=='medium' for n in nodes)==18
assert sum(n['size']=='alien_medium' for n in nodes)==18
assert sum(n['size']=='large' for n in nodes)==12
assert all(1<=len(n['effects'])<=2 for n in nodes)
assert all(len(n['effects'])==1 and n['cost']['alien_points']==0 and set(n['cost'])=={'science','alien_points'} for n in nodes if n['size']=='small')
visiting=set(); visited=set()
def visit(id):
    assert id in lookup, id
    assert id not in visiting, f'cycle: {id}'
    if id in visited: return
    visiting.add(id)
    for r in lookup[id]['requires']: visit(r)
    visiting.remove(id); visited.add(id)
for id in lookup: visit(id)
def ancestors(id):
    seen={id}
    for r in lookup[id]['requires']: seen|=ancestors(r)
    return seen
def ap_chain(id): return sum(lookup[k]['cost']['alien_points'] for k in ancestors(id))
assert ap_chain('C_G1')<=6
assert ap_chain('M_N1')==ap_chain('L_N1')==0
early=['K_A1','I_A1','D_A1','C_A1']
assert all(lookup[k]['cost']['alien_points']==1 and lookup[k]['unlock']['defense_stage']==0 for k in early)

matrix=[[1.6,.2,0],[.65,1.65,.2],[.65,.65,1.8]]
efficiency=[1,.85,.75]
work=[.45,.35,.2]
single=[]
for row,base in zip(matrix,efficiency):
    single.append(None if any(d==0 and w>0 for d,w in zip(row,work)) else sum(w/(d*base) for d,w in zip(row,work)))
mixed=sum(work[i]/(matrix[i][i]*efficiency[i]) for i in range(3))
ap_rewards=[(3,3),(5,3),(10,4),(15,4),(20,5),(25,5),(30,6),(35,6),(40,7)]
acc=0; ledger=[]
for w,r in ap_rewards: acc+=r; ledger.append(dict(wave=w,reward=r,cumulative=acc))
snapshots=[]
for w in [1,5,10,20,30,40,60,80,100]:
    snapshots.append(dict(wave=w,count_full_fronts=math.ceil((10+2*(w-1))*2),hp_multiplier=round(1.035**(w-1),4),damage_multiplier=round(1.018**(w-1),4)))
audit=dict(status='DESIGN_ONLY_NOT_GAME_TESTED',node_count=168,small=120,medium=36,large=12,
    references_exist=True,acyclic=True,max_effects_per_node=2,small_only_science=True,
    first_assault_required_alien_points=ap_chain('C_G1'),first_assault_completed_wave=24,missile_required_alien_points=ap_chain('M_N1'),laser_required_alien_points=ap_chain('L_N1'),
    first_ten_wave_science_budget=dict(income_lower_bound=612.5,missile_science=60,laser_science=220,small_nodes_count=22,small_mean_science=10,early_alien_medium_count=3,early_alien_medium_science=18,remaining=612.5-60-220-22*10-3*18),
    alien_total_once=sum(n['cost']['alien_points'] for n in nodes),early_alien_choices=early,alien_income=ledger,wave_samples=snapshots,
    armor_work_model=dict(assumptions='Equal fully burdened investment. No travel, shields regenerating, splash or firepower waste. Armor work shares, not enemy counts. Comparative arithmetic only.',work_shares=work,normalized_damage_per_investment=efficiency,matrix=matrix,
        pure_kinetic_work=None,pure_missile_work=single[1],pure_laser_work=single[2],specialist_mix_work=mixed,
        pure_missile_cost_relative_to_mix=single[1]/mixed,pure_laser_cost_relative_to_mix=single[2]/mixed))
spec=dict(status='PROPOSAL_NOT_RUNTIME',principles=['小科技一个属性且任何已开放基础机型均有有效收益','中科技一个明确机制或机型','大科技最多两个核心效果','科技不重复解锁Perk','基础反制武器无外星点前置'],nodes=nodes)
(ROOT/'technology-catalog-proposal.json').write_text(json.dumps(spec,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'balance-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
out=['# 科技树逐节点目录（设计提案）','','此表为策划数据，不会被当前游戏加载。共 168 个真实节点，每节点仅购买自己的一项或两项效果。科研与外星点以外的科技费用均为 0。','']
for b,(branch,_) in FAMILIES.items():
    out += [f'## {branch}','','| ID | 节点 | 类型 | 科研 | 外星点 | 效果 | 直接前置 | 开放条件 |','|---|---|---|---:|---:|---|---|---|']
    for n in [x for x in nodes if x['branch']==b]:
        u=n['unlock']; gates=[]
        if u['completed_wave']: gates.append(f'完成第{u["completed_wave"]}波')
        if u['defense_stage']: gates.append(f'进入第{u["defense_stage"]}次外推')
        if u.get('first_medium_boss_defeated'): gates.append('击败过中Boss')
        out.append(f'| {n["id"]} | {n["name"]} | {n["size"]} | {n["cost"]["science"]} | {n["cost"]["alien_points"]} | {"；".join(n["effects"])} | {", ".join(n["requires"]) or "起点"} | {"；".join(gates) or "无额外门槛"} |')
    out.append('')
(ROOT/'technology-catalog-proposal.md').write_text('\n'.join(out),encoding='utf-8')
print(json.dumps(audit,ensure_ascii=False,indent=2))
