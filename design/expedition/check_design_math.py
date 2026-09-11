from pathlib import Path
import itertools,json,math
worlds=[('moon',1.0,.90,.95),('mercury',.90,1.15,1.10),('venus',1.10,.90,1.05),('mars',1.15,1.0,1.10),('europa',1.05,1.05,1.10),('titan',.85,1.10,1.0)]
rows=[]
for n in range(6):
    rows.append({'completed_before_entry':n,'enemy_hp_multiplier':3.2**n,'enemy_damage_multiplier':2.4**n,'material_reward_multiplier':3.5**n,'industrial_output_target_multiplier':3.3**n,'tier_upgrade_cost_multiplier':3.4**n,'fleet_dps_target_multiplier':3.1**n,'fleet_ehp_target_multiplier':2.35**n,'boss_reference_hp':12_000_000*3.2**n,'fleet_reference_dps':150_000*3.1**n,'boss_ideal_ttk_seconds':80*(3.2/3.1)**n})
orders=[]
for order in itertools.permutations(worlds):
    ttk=[80*w[1]*(3.2/3.1)**n for n,w in enumerate(order)]
    rewards=sum(1800*w[3]*3.5**n for n,w in enumerate(order))
    orders.append({'order':[w[0] for w in order],'ideal_ttk_seconds':ttk,'combat_alloy_total':rewards})
backup=Path('saves/backups/20260910-003354-827-pre-expedition-design/profile-01/capture-01/earthward_checkpoint.json')
save=json.loads(backup.read_text(encoding='utf-8-sig'))['game']
startup={'minerals':168000,'energy':113000,'science':70000,'alloy':1850}
remaining={k:save[k]-startup[k] for k in ['minerals','energy','science']}
assert all(x>=0 for x in remaining.values())
assert 2000-startup['alloy']>=0
out={'status':'design_math_only_not_playtest','scope':'Enumerates 720 order permutations under the same-rank-investment assumption; excludes skill downtime, player action, factory occupancy and tactical failures. Does not prove every route playable.','rank_rows':rows,'world_intrinsic_proposal':[{ 'id':i,'hp':h,'damage':d,'reward':r} for i,h,d,r in worlds],'orders_checked':len(orders),'all_ttk_finite':all(math.isfinite(x) for o in orders for x in o['ideal_ttk_seconds']),'ideal_ttk_bounds':[min(x for o in orders for x in o['ideal_ttk_seconds']),max(x for o in orders for x in o['ideal_ttk_seconds'])],'total_alloy_bounds':[min(o['combat_alloy_total'] for o in orders),max(o['combat_alloy_total'] for o in orders)],'startup_cost':startup,'player_balance_after_fixed_chain_before_new_income':remaining,'alloy_after_fixed_grant_and_chain':150,'protocol_pairs':{'unordered_pairs_for_three_types':math.comb(6,2)*3,'directed_main_aux_for_three_types':6*5*3}}
Path('design/expedition/balance-scenarios.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'orders':out['orders_checked'],'ideal_ttk_bounds':out['ideal_ttk_bounds'],'startup_remaining':remaining,'protocol_pairs':out['protocol_pairs']},ensure_ascii=False))
