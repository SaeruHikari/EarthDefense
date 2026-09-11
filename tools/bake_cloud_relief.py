"""Bake clear coverage and a genuinely varying cloud-top height field.

The PNG is LINEAR data: R=coverage, GB=signed tangent height derivatives,
A=normalized radial height. This script intentionally leaves older cloud assets
unchanged. Requires only NumPy and Pillow; no runtime image generation.
"""
from pathlib import Path
import hashlib
import json
import math
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'assets' / 'earth' / 'clouds'
SEED = 261213
DERIVATIVE_RANGE = 16.0
W, H = 4096, 2048
RNG = np.random.default_rng(SEED)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0., 1.)
    return t * t * (3. - 2. * t)


def noise(w, h, nx, ny):
    lattice = RNG.random((ny + 1, nx), dtype=np.float32)
    xx = (np.arange(w, dtype=np.float32) + .5) / w * nx
    yy = (np.arange(h, dtype=np.float32) + .5) / h * ny
    ix, iy = np.floor(xx).astype(int), np.floor(yy).astype(int)
    tx, ty = xx - ix, yy - iy
    tx = tx * tx * tx * (tx * (tx * 6. - 15.) + 10.)
    ty = ty * ty * ty * (ty * (ty * 6. - 15.) + 10.)
    tx, ty = tx[None, :], ty[:, None]
    a = lattice[iy[:, None], ix[None, :] % nx]
    b = lattice[iy[:, None], (ix[None, :] + 1) % nx]
    c = lattice[(iy[:, None] + 1).clip(0, ny), ix[None, :] % nx]
    d = lattice[(iy[:, None] + 1).clip(0, ny), (ix[None, :] + 1) % nx]
    return ((a * (1-tx) + b * tx) * (1-ty) + (c * (1-tx) + d * tx) * ty).astype(np.float32)


def rounded_billows(w, h, populations=None):
    """Overlapping Gaussian cloud turrets with finite, smoothly tapered edges.

    Additive soft unions give rounded summits without sharp Voronoi ridges.
    Two staggered populations form broad cloud towers and smaller cauliflower
    detail. Their layout is periodic in longitude, deterministic, and offline.
    """
    result = np.zeros((h, w), dtype=np.float32)
    for nx, ny, weight in (populations or [(35, 18, .72), (83, 42, .28)]):
        field = np.zeros_like(result)
        cellx, celly = w / nx, h / ny
        for cy in range(ny):
            for cx in range(nx):
                x = (cx + RNG.uniform(.12, .88)) * cellx
                y = (cy + RNG.uniform(.12, .88)) * celly
                sx = cellx * RNG.uniform(.32, .62)
                sy = celly * RNG.uniform(.32, .64)
                strength = RNG.uniform(.75, 1.50)
                xs = np.arange(math.floor(x - 3*sx), math.ceil(x + 3*sx) + 1)
                ys = np.arange(max(0, math.floor(y - 3*sy)), min(h, math.ceil(y + 3*sy) + 1))
                dx = (xs - x)[None, :] / sx
                dy = (ys - y)[:, None] / sy
                r2 = dx * dx + dy * dy
                puff = (np.exp(-.5*r2) * (1-smoothstep(6.25, 9., r2)) * strength).astype(np.float32)
                field[np.ix_(ys, xs % w)] += puff
        # Soft union preserves rounded summits without saturating into plateaux.
        field = 1. - np.exp(-field * .80)
        result += field * weight
    return result


def resize_float(data, size):
    return np.asarray(Image.fromarray(data).resize(size, Image.Resampling.BICUBIC), dtype=np.float32)


def polar_height_contract(coverage, height):
    distance = np.minimum((np.arange(H) + .5) / H, 1 - (np.arange(H) + .5) / H)
    for row in np.flatnonzero(distance < .005):
        amount = float(smoothstep(.0015, .005, distance[row]))
        coverage[row] = coverage[row].mean() + (coverage[row] - coverage[row].mean()) * amount
        height[row] = height[row].mean() + (height[row] - height[row].mean()) * amount


def import_config():
    source = 'res://assets/earth/clouds/cloud_relief.png'
    digest = hashlib.md5(source.encode()).hexdigest()
    cache = 'res://.godot/imported/cloud_relief.png-' + digest + '.bptc.ctex'
    return f'''[remap]

importer="texture"
type="CompressedTexture2D"
path.bptc="{cache}"
metadata={{"imported_formats": ["s3tc_bptc"], "vram_texture": true}}

[deps]

source_file="{source}"
dest_files=["{cache}"]

[params]

compress/mode=2
compress/high_quality=true
compress/lossy_quality=0.95
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=2
compress/channel_pack=0
mipmaps/generate=true
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=false
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=0
'''


def main():
    global RNG
    RNG = np.random.default_rng(SEED)
    source = Image.open(ROOT / 'assets' / 'earth' / '8k_earth_clouds.jpg').convert('L')
    raw = np.asarray(source.resize((W, H), Image.Resampling.LANCZOS), dtype=np.float32) / 255.
    # No blur on coverage: retain the actual cloud system outlines and holes.
    coverage = smoothstep(.14, .76, raw)
    billows = rounded_billows(W // 2, H // 2)
    macro = noise(W//2, H//2, 23, 12)
    fine = .68 * noise(W//2, H//2, 190, 95) + .32 * noise(W//2, H//2, 370, 185)
    structure = .15 + .65 * billows + .17 * macro + .08 * (fine-.5)
    structure = resize_float(structure, (W, H))
    # Real rounded 10-40px turret lobes, independent of the coverage silhouette.
    medium = rounded_billows(W//2, H//2, [(90, 45, .66), (165, 83, .34)])
    medium = resize_float(medium, (W, H))
    profile = .60 + (structure-.67)*2.70 + (medium-.66)*1.00
    # Smooth soft bounds preserve broad valleys and rounded summits; no clamp plateau.
    structure = .50 + .44 * np.tanh((profile-.50)/.44)
    # Cloud amount controls the outer footprint, while independent cloud turrets
    # retain real height variation even in completely saturated source interiors.
    padded = np.pad(np.round(coverage*255).astype(np.uint8), ((0,0),(40,40)), mode="wrap")
    smoothed = np.asarray(Image.fromarray(padded).filter(ImageFilter.GaussianBlur(8)), dtype=np.float32)[:,40:-40] / 255.
    envelope = smoothstep(.04, .68, smoothed) ** .50
    height = np.clip(envelope * structure, 0., 1.)
    polar_height_contract(coverage, height)
    # Exact spherical tangent derivatives of normalized A. With radius R and
    # displacement range D, normal = normalize(radial - gradient*(D/R)*16).
    # These channels already include eastward latitude metric compensation.
    latitude_sine = np.sin((np.arange(H, dtype=np.float32) + .5) / H * np.pi)[:, None]
    east = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * (W / (4*np.pi)) / np.maximum(latitude_sine, .035)
    south = np.gradient(height, axis=0) * (H / np.pi)
    pole_fade = smoothstep(.008, .035, latitude_sine)
    gradients = np.stack([east, south], axis=-1) / DERIVATIVE_RANGE
    clipped_fraction = float(np.mean(np.abs(gradients) > .99))
    gradients = np.clip(gradients, -.99, .99) * pole_fade[:, :, None]
    data = np.round(np.clip(np.stack([coverage, gradients[:,:,0]*.5+.5, gradients[:,:,1]*.5+.5, height], axis=-1), 0, 1)*255).astype(np.uint8)
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / 'cloud_relief.png'
    Image.fromarray(data).save(path, optimize=True)
    if not (OUT / 'cloud_relief.png.import').exists():
        (OUT / 'cloud_relief.png.import').write_text(import_config(), encoding='utf-8')
    dense = height[coverage>.9]
    stats = {'seed':SEED, 'dimensions':[W,H], 'gradient_decode':'GB*2-1; normal_strength = height_range/base_radius*16; latitude metric already baked', 'normal_gain_base_4_08_range_0_20':.20/4.08*16, 'coverage_mean':float(coverage.mean()), 'height_min':float(height.min()), 'height_max':float(height.max()), 'dense_height_percentiles':np.percentile(dense,[1,10,50,90,99]).tolist(), 'dense_height_std':float(dense.std()), 'clipped_gradient_fraction':clipped_fraction, 'file_bytes':path.stat().st_size, 'sha256':hashlib.sha256(path.read_bytes()).hexdigest(), 'longitude_seam_mean_delta':np.abs(data[:,0].astype(float)-data[:,-1].astype(float)).mean(axis=0).tolist(), 'neighbor_mean_delta':np.abs(data[:,1:].astype(float)-data[:,:-1].astype(float)).mean(axis=(0,1)).tolist()}
    (OUT / 'CLOUD_RELIEF_STATS.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
    # Offline QA only: shaded tangent-space cloud tops over a dark Earth tone.
    gx,gy=gradients[:,:,0]*(.20/4.08*16),gradients[:,:,1]*(.20/4.08*16)
    light=np.clip((-.55*gx+.38*gy+.74)/np.sqrt(1+gx*gx+gy*gy),0,1)
    shade=np.clip(.20+.82*light,0,1)
    clouds=shade[:,:,None]*np.array([.94,.97,1.],dtype=np.float32)
    preview=np.array([.028,.070,.115],dtype=np.float32)+(clouds-np.array([.028,.070,.115],dtype=np.float32))*coverage[:,:,None]
    Image.fromarray(np.uint8(np.clip(preview,0,1)*255)).resize((2048,1024),Image.Resampling.LANCZOS).save(ROOT/'artifacts'/'cloud-relief-bake-preview.jpg',quality=92)
    print(json.dumps(stats,indent=2))


if __name__ == '__main__':
    main()


