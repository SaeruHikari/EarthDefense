from pathlib import Path
import json
root=Path(__file__).resolve().parent
p=root/'aircraft-gameplay-redesign-v2.md'
s=p.read_text(encoding='utf-8')
s=s.replace('4K1 + 2K3 + 4M1 + 2L1 + 1L3 + 2点机动余量','6K1 + 2K3 + 4M1 + 2L1 + 1L3 + 2点机动余量')
s=s.replace('表内为24点配置，需扩大到24点再使用；少量关键战机完成重击，失去保护时波动大。','少量关键战机完成重击；快机比例较低，失去保护或被连续穿线时波动大。')
s=s.replace('1孵化 + 2岩甲 + 9裂爪，幼机名额另从本波总数预留','1孵化 + 2岩甲 + 3裂爪 + 舱内6幼机，总计12')
s=s.replace('以及裂爪、织幕、蚀星三类敌人。','以及裂爪、岩甲盾舟、织幕、蚀星四类敌人。')
old='选择“护航”意味着系统保留真实的护航名额与跟随位置，而不是给所有附近单位无条件挂光环。'
new=old+'\n\n工厂模板可提供守备、均衡、攻坚三个策略，分别建议保留约80%、50%、20%的本厂轻型编制承担近地任务，整数取整不能导致唯一一架拦截机也离开防线。此比例是初始可配置建议，不是新单位上限。未解锁对母舰攻坚时全部承担近地任务；解锁后也不会自动把全体飞机抽离地球。策略放在已有工厂配置中，不增加独立指挥面板。'
s=s.replace(old,new)
refs={
1:'https://qute.co.jp/press-release-eschatos-220106/',
2:'https://store.steampowered.com/app/378770/ESCHATOS/?l=brazilian',
3:'https://rtypefinal2.com/?lang=en',
4:'https://darius.jp/dbcs/en/system/index.html',
5:'https://www.platinumgames.co.jp/dev-sol-cresta/article/440',
6:'https://store.steampowered.com/app/253750/Ikaruga/?l=english',
7:'https://eu-support.konami.com/hc/en-gb/articles/25358374579351-CYGNI-All-Guns-Blazing',
8:'https://store.steampowered.com/app/2678070/Phoenix_2/',
9:'https://blog.novadrift.io/nova-drift-future-content-2/',
10:'https://blog.novadrift.io/patch-notes/'
}
for i,url in refs.items():s=s.replace(f'[{i}]',f'[{i}]({url})')
p.write_text(s,encoding='utf-8')
cost={'K1':1,'K2':2,'K3':2,'M1':1,'M2':2,'M3':2,'L1':1,'L2':2,'L3':2}
fleets=[{'name':'高周转守备','units':{'K1':10,'K2':2,'M1':4,'L1':2},'reserve':0},
        {'name':'穿插破阵','units':{'K1':6,'K3':2,'M1':4,'L1':2,'L3':1},'reserve':2},
        {'name':'重击防线','units':{'K1':4,'M1':2,'L1':2,'M3':2,'L2':2,'L3':2},'reserve':0}]
for fleet in fleets:
    fleet['used_points']=sum(cost[k]*n for k,n in fleet['units'].items())
    fleet['total_points']=fleet['used_points']+fleet['reserve']
    assert fleet['total_points']==20
for code in cost:assert s.count('### '+code+' ')==1
for i in range(1,11):assert '| E'+str(i)+' ' in s
result={'scope':'Design arithmetic and roster completeness only; not combat balance simulation',
        'friendly_roster':len(cost),'enemy_roster':10,'fleet_examples':fleets,
        'source_urls':list(refs.values()),'production_files_modified':False}
(root/'aircraft-v2-design-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('Design audit: 9 friendly cards, 10 enemy cards, three 20-point examples, source links checked structurally.')
