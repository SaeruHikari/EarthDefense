"""Author original tileable PBR surface maps for the fleet and industrial assets.

No external image inputs. RGB ORM follows glTF: occlusion, roughness, metallic.
Geometry and UVs are stored in the editable assets/managed packed scenes.
The former one-time GDScript migration baker is retired.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[1] / 'assets' / 'pbr'
OUT.mkdir(parents=True, exist_ok=True)
N = 256
profiles = [
    ('ceramic_coating', .34, .06, 0),
    ('painted_alloy', .43, .28, 0),
    ('brushed_steel', .28, .94, 0),
    ('dark_titanium', .38, .87, 0),
    ('optical_glass', .14, .12, 0),
    ('copper_gold', .25, 1., 0),
    ('mineral', .81, .0, 0),
    ('polymer', .61, .02, 0),
    ('flight_emitter', .28, .15, 6.5),
    ('hangar_guide', .35, .1, 1.1),
    ('lit_glass', .19, .12, .24),
    ('hostile_red_emitter', .28, .15, 6.5),
    ('missile_amber_emitter', .28, .15, 6.5),
    ('enamel', .27, .05, 0),
    ('machined_steel', .23, .95, 0),
    ('structural_coating', .52, .25, 0),
]
maps = {name: np.zeros((N*4, N*4, 3), dtype=np.uint8) for name in ['albedo', 'orm', 'normal', 'emission']}
y, x = np.mgrid[:N, :N].astype(np.float32) / N
rng = np.random.default_rng(210019)
for i, (name, rough, metal, energy) in enumerate(profiles):
    grain = rng.normal(0, .24, (N,N)).astype(np.float32)
    grain = (grain + np.roll(grain,1,axis=0) + np.roll(grain,1,axis=1)) / 3
    brushed = np.sin(y*2*np.pi*87)*.22 + np.sin(y*2*np.pi*61)*.12
    brushed *= float(metal > .7)
    detail = grain + brushed
    if 'glass' in name or energy:
        detail *= .06
    # Restrained material texture; edge definition is modeled in geometry.
    albedo = np.clip(.97 + detail*.038, .88, 1)
    roughness = np.clip(rough + detail*.09, .09, .95)
    occlusion = np.clip(.99 - np.abs(grain)*.025, .95, 1)
    height = detail * (.032 if metal > .7 else .022)
    dy = (np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*.5
    dx = (np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*.5
    normal = np.stack([-dx,-dy,np.ones_like(dx)],axis=-1)
    normal /= np.linalg.norm(normal,axis=-1,keepdims=True)
    ox, oy = (i%4)*N, (i//4)*N
    tile = (slice(oy,oy+N), slice(ox,ox+N))
    maps['albedo'][tile] = np.repeat((albedo*255).astype(np.uint8)[...,None],3,axis=2)
    maps['orm'][tile] = (np.stack([occlusion,roughness,np.full_like(x,metal)],axis=-1)*255).astype(np.uint8)
    maps['normal'][tile] = ((normal*.5+.5)*255).astype(np.uint8)
    emission_srgb = np.array({8:[.518,1.,.878],9:[.396,.902,.749],10:[.216,.510,.588],11:[1.,.475,.412],12:[.949,.718,.451]}.get(i,[1.,1.,1.]),dtype=np.float32)
    emission_linear = np.where(emission_srgb<=.04045,emission_srgb/12.92,((emission_srgb+.055)/1.055)**2.4)*(energy/8)
    encoded_emission = np.where(emission_linear<=.0031308,emission_linear*12.92,1.055*emission_linear**(1/2.4)-.055)
    maps['emission'][tile] = np.round(255*encoded_emission).astype(np.uint8)
for name, pixels in maps.items():
    Image.fromarray(pixels).save(OUT/f'surface_{name}.png')
(OUT/'profiles.json').write_text(json.dumps({'tile_size':N,'grid':[4,4],'profiles':[dict(id=i,name=p[0],roughness=p[1],metallic=p[2],emission=p[3]) for i,p in enumerate(profiles)]},indent=2),encoding='utf-8')
print('PBR_ATLAS_CREATED',OUT)
