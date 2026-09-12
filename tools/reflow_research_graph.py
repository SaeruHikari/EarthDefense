"""Deterministic presentation-only research reflow; never edits costs, effects or requirements.
Usage: python tools/reflow_research_graph.py --write (or --check)
"""
from pathlib import Path
import argparse, collections, csv, itertools, json, math

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / 'data/domain'
BRANCHES = 'KMLIDC'
FIRST_RADIUS, RING_SPACING, SPOKE_DEGREES = 220.0, 84.0, 6.0
# D_N4 is an existing direct medium technology whose established presentation
# lane is deliberately kept on the third ring.  Its new repair child follows
# that node on the next ring; retaining the anchor prevents a medium node from
# colliding with the five compact first-ring spokes.
LAYOUT_DEPTH_OVERRIDES = {'D_N4': 3}

def read(name):
    with (DATA / name).open(encoding='utf-8-sig', newline='') as stream:
        reader = csv.DictReader(stream)
        return reader.fieldnames, list(reader)

def generate():
    fields, nodes = read('deep_technology_nodes.csv')
    index = {node['id']: node for node in nodes}
    requires = collections.defaultdict(list)
    for row in read('deep_technology_nodes_requires.csv')[1]:
        requires[row['parent_id']].append(row['value'])
    depths, resolving = {}, set()
    def depth(key):
        if key in depths: return depths[key]
        if key in LAYOUT_DEPTH_OVERRIDES:
            depths[key] = LAYOUT_DEPTH_OVERRIDES[key]
            return depths[key]
        if key in resolving: raise ValueError('Cyclic technology: ' + key)
        resolving.add(key)
        depths[key] = 1 + max((depth(parent) for parent in requires[key]), default=0)
        resolving.remove(key)
        return depths[key]
    for key in index: depth(key)
    slots, positions = {}, {}
    for level in range(1, max(depths.values()) + 1):
        for branch_index, branch in enumerate(BRANCHES):
            group = [node for node in nodes if node['branch'] == branch and depths[node['id']] == level]
            if not group: continue
            remaining = []
            occupied = set()
            for node in group:
                pair = node.get('legacy_pair_id') or node['id']
                original = int(pair.split('_S')[1]) if '_S' in pair else 0
                if node['size'] == 'small' and 1 <= original <= 20:
                    spoke = ((original - 1) % 5 - 2) * 2
                    slots[node['id']] = spoke
                    assert spoke not in occupied, (branch, level, spoke)
                    occupied.add(spoke)
                else: remaining.append(node)
            remaining.sort(key=lambda node: node['id'])
            free = [spoke for spoke in range(-4, 5) if spoke not in occupied]
            radius = FIRST_RADIUS + (level - 1) * RING_SPACING
            def point(spoke):
                angle = math.radians(branch_index * 60 + spoke * SPOKE_DEGREES)
                return (math.sin(angle) * radius, -math.cos(angle) * radius)
            def score(assignment):
                total = 0.0
                for node, spoke in zip(remaining, assignment):
                    key = node['id']; parents = requires[key]
                    # Every split small-tech pair is on precisely the same ray, including missile extensions.
                    if node.get('is_pair_second') == 'true' and len(parents) == 1 and index[parents[0]]['branch'] == branch and spoke != slots[parents[0]]:
                        return math.inf
                    p = point(spoke)
                    deepest = max((depths[parent] for parent in parents), default=0)
                    for parent in parents:
                        q = positions[parent]
                        weight = 25 if len(parents) == 1 else 12 if depths[parent] == deepest else 1
                        total += weight * ((p[0]-q[0])**2 + (p[1]-q[1])**2)
                    # Stable center preference only breaks equivalent placements.
                    total += abs(spoke) * .001
                return total
            choices = itertools.permutations(free, len(remaining))
            best = min(choices, key=lambda assignment: (score(assignment), assignment))
            if not math.isfinite(score(best)): raise ValueError('Cannot retain paired ray: ' + branch + ':' + str(level))
            for node, spoke in zip(remaining, best): slots[node['id']] = spoke
            for node in group: positions[node['id']] = point(slots[node['id']])
    minimum, nearest = math.inf, None
    radii = {'small':12,'medium':20,'large':32}
    for i, a in enumerate(nodes):
        for b in nodes[i+1:]:
            gap = math.dist(positions[a['id']],positions[b['id']])-radii[a['size']]-radii[b['size']]
            if gap < minimum: minimum, nearest = gap, (a['id'],b['id'])
    if minimum < 8: raise ValueError('Overlapping research nodes: ' + str((minimum,nearest)))
    single, multi, pairs, secondary = 0, 0, 0, 0
    for node in nodes:
        key = node['id']; parents = requires[key]
        if parents:
            assert depths[key] == max(depths[parent] for parent in parents) + 1
            if len(parents)==1: single += 1
            else: multi += 1
            secondary += sum(index[parent]['branch'] != node['branch'] or depths[key]-depths[parent]>1 for parent in parents)
        if node.get('is_pair_second') == 'true':
            assert len(parents)==1 and slots[key]==slots[parents[0]]
            assert abs(math.dist(positions[key],positions[parents[0]])-RING_SPACING)<.0001
            pairs += 1
        node['layout_depth'] = str(depths[key])
        node['layout_row'] = str(depths[key])
        node['layout_lane'] = str(slots[key])
    report = {'nodes':len(nodes),'edges':sum(map(len,requires.values())),'rings':max(depths.values()),'single_parent_next_ring':single,'multi_parent_deepest_plus_one':multi,'same_ray_pairs':pairs,'secondary_edges':secondary,'minimum_node_gap':minimum,'nearest_nodes':nearest,'first_radius':FIRST_RADIUS,'ring_spacing':RING_SPACING,'max_radius':max(math.hypot(*p) for p in positions.values())}
    return fields,nodes,positions,report

def write_csv(path, fields, rows):
    with path.open('w',encoding='utf-8',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=fields,lineterminator='\n',extrasaction='ignore');writer.writeheader();writer.writerows(rows)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--write',action='store_true');parser.add_argument('--check',action='store_true');args=parser.parse_args()
    fields,nodes,positions,report=generate()
    if args.write:
        backup=ROOT/'artifacts/research-adjacent127-before';backup.mkdir(exist_ok=True)
        for name in ('deep_technology_nodes.csv','deep_technology_nodes_draw_position.csv'):
            destination=backup/name
            if not destination.exists():destination.write_bytes((DATA/name).read_bytes())
        write_csv(DATA/'deep_technology_nodes.csv',fields,nodes)
        rows=[]
        for node in nodes:
            for axis,value in enumerate(positions[node['id']]):rows.append({'parent_id':node['id'],'position':axis,'type':'number','value':format(value,'.9f')})
        write_csv(DATA/'deep_technology_nodes_draw_position.csv',['parent_id','position','type','value'],rows)
    if args.check:
        stored=collections.defaultdict(lambda:[0.,0.])
        for row in read('deep_technology_nodes_draw_position.csv')[1]:stored[row['parent_id']][int(row['position'])]=float(row['value'])
        assert all(math.dist(stored[key],point)<.00001 for key,point in positions.items()), 'CSV geometry differs from deterministic DAG layout'
    print(json.dumps(report,ensure_ascii=False,indent=2))
    (ROOT/'artifacts/research-adjacent127-audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
if __name__=='__main__':main()
