"""Bake deterministic linear cloud data; runtime performs no weather simulation.

Requires NumPy and Pillow. Run from any directory. Every generated image is a
linear numeric texture, not an sRGB image; do not use source_color in shaders.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'assets' / 'earth' / 'clouds'
SEED = 260909
rng = np.random.default_rng(SEED)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def grid(w, h):
    u = np.broadcast_to((np.arange(w, dtype=np.float32) + 0.5)[None, :] / w, (h, w))
    v = np.broadcast_to((np.arange(h, dtype=np.float32) + 0.5)[:, None] / h, (h, w))
    return u, v


def sample(field, u, v):
    """Bilinear lookup: longitude wraps, latitude clamps."""
    h, w = field.shape
    xx = np.mod(u, 1.0) * w - 0.5
    yy = np.clip(v * h - 0.5, 0.0, h - 1.0)
    xi = np.floor(xx).astype(np.int32)
    yi = np.floor(yy).astype(np.int32)
    tx, ty = xx - xi, yy - yi
    a = field[yi, xi % w]
    b = field[yi, (xi + 1) % w]
    c = field[np.minimum(yi + 1, h - 1), xi % w]
    d = field[np.minimum(yi + 1, h - 1), (xi + 1) % w]
    return ((a * (1.0 - tx) + b * tx) * (1.0 - ty) + (c * (1.0 - tx) + d * tx) * ty).astype(np.float32)


def value_noise(w, h, cells_x, cells_y):
    """Smooth periodic longitude noise from an offline random lattice."""
    lattice = rng.random((cells_y + 1, cells_x), dtype=np.float32)
    u, v = grid(w, h)
    x = u * cells_x
    y = v * cells_y
    xi = np.floor(x).astype(np.int32)
    yi = np.floor(y).astype(np.int32)
    tx, ty = x - xi, y - yi
    tx = tx * tx * tx * (tx * (tx * 6.0 - 15.0) + 10.0)
    ty = ty * ty * ty * (ty * (ty * 6.0 - 15.0) + 10.0)
    a = lattice[yi, xi % cells_x]
    b = lattice[yi, (xi + 1) % cells_x]
    c = lattice[np.minimum(yi + 1, cells_y), xi % cells_x]
    d = lattice[np.minimum(yi + 1, cells_y), (xi + 1) % cells_x]
    return ((a * (1.0 - tx) + b * tx) * (1.0 - ty) + (c * (1.0 - tx) + d * tx) * ty).astype(np.float32)


def blur_periodic(field, radius):
    """Pillow Gaussian with longitude padding; padding never modifies seam."""
    pad = int(math.ceil(radius * 4)) + 2
    encoded = np.round(np.clip(field, 0, 1) * 255).astype(np.uint8)
    padded = np.pad(encoded, ((0, 0), (pad, pad)), mode='wrap')
    blurred = np.asarray(Image.fromarray(padded).filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255.0
    return blurred[:, pad:-pad]


def pack_cloud(coverage, thickness, gradient_gain):
    h, w = coverage.shape
    # Cloud top grows slowly with optical depth; prefiltered gradients avoid
    # embossed hard edges and are stored instead of per-fragment derivatives.
    height = blur_periodic(np.power(thickness, 0.75), max(1.3, w / 1700.0))
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    dy = np.gradient(height, axis=0)
    lat = (0.5 - (np.arange(h, dtype=np.float32) + 0.5) / h) * np.pi
    pole_fade = smoothstep(0.04, 0.20, np.cos(lat))[:, None]
    # UV pixels correspond to equal angular increments because width=2*height.
    # Suppress the tangent field at the singular poles instead of amplifying it.
    dx = np.clip(dx * gradient_gain, -0.95, 0.95) * pole_fade
    dy = np.clip(dy * gradient_gain, -0.95, 0.95) * pole_fade
    packed = np.stack([coverage, dx * 0.5 + 0.5, dy * 0.5 + 0.5, thickness], axis=-1)
    return np.round(np.clip(packed, 0, 1) * 255).astype(np.uint8)



def polar_contract(data):
    """Collapse only a 0.5% cap; exact longitude convergence inside v=0.0015."""
    h = data.shape[0]
    distance = np.minimum((np.arange(h) + 0.5) / h, 1.0 - (np.arange(h) + 0.5) / h)
    rows = np.flatnonzero(distance < 0.005)
    for row in rows:
        amount = float(smoothstep(0.0015, 0.005, distance[row]))
        values = data[row].astype(np.float32)
        neutral = np.array([values[:, 0].mean(), 128.0, 128.0, values[:, 3].mean()], dtype=np.float32)
        data[row] = np.round(neutral + (values - neutral) * amount).astype(np.uint8)
    return data


def main_clouds():
    source = Image.open(ROOT / 'assets' / 'earth' / '8k_earth_clouds.jpg').convert('L')
    original = np.asarray(source.resize((4096, 2048), Image.Resampling.LANCZOS), dtype=np.float32) / 255.0
    coverage = smoothstep(0.115, 0.84, original)
    # Broad interiors retain optical depth; thin edges stay translucent.
    interior = blur_periodic(coverage, 4.0)
    thickness = np.clip(np.power(coverage, 0.80) * 0.65 + interior * 0.35, 0, 1)
    return polar_contract(pack_cloud(coverage, thickness, 20.0))


def cirrus_clouds():
    global rng
    rng = np.random.default_rng(SEED + 11)
    w, h = 2048, 1024
    coverage = np.zeros((h, w), dtype=np.float32)
    # Each plume has a finite footprint, independent orientation, and short
    # bent ribs. No globally anisotropic texture can form parallel latitude bands.
    for _ in range(260):
        cx = float(rng.uniform(0, w))
        cy = float(rng.uniform(0.075 * h, 0.925 * h))
        sx = float(rng.uniform(12.0, 39.0))
        sy = float(rng.uniform(5.0, 18.0))
        angle = float(rng.uniform(-np.pi, np.pi))
        extent = int(math.ceil(2.9 * max(sx, sy)))
        xs = np.arange(int(cx) - extent, int(cx) + extent + 1)
        ys = np.arange(max(0, int(cy) - extent), min(h, int(cy) + extent + 1))
        dx, dy = np.meshgrid(xs - cx, ys - cy)
        ca, sa = math.cos(angle), math.sin(angle)
        x = (dx * ca + dy * sa) / sx
        y = (-dx * sa + dy * ca) / sy
        phase = float(rng.uniform(-np.pi, np.pi))
        curvature = float(rng.uniform(-1.15, 1.15))
        curl = curvature * (0.38 * x * x + 0.45 * np.sin(x * 1.8 + phase))
        cross = y - curl
        envelope = np.exp(-0.5 * (x / 1.10) ** 2 - 0.5 * (cross / 0.85) ** 2)
        local_h, local_w = x.shape
        # Local broken fibers: randomized rib positions and curvature, with
        # irregular breakup at scales shorter than the whole cloud plume.
        breakup = value_noise(local_w, local_h, 15, 13)
        wisps = np.zeros_like(x)
        for rib in range(int(rng.integers(5, 10))):
            offset = float(rng.uniform(-1.25, 1.25))
            rib_curve = cross - offset - float(rng.uniform(-0.26, 0.26)) * x
            rib_curve += (breakup - 0.5) * 0.38 + np.sin(x * 5.0 + phase + offset * 3.0) * 0.06
            center = float(rng.uniform(-0.55, 0.55))
            length = float(rng.uniform(0.45, 1.15))
            width = float(rng.uniform(0.12, 0.29))
            rib_density = np.exp(-0.5 * (rib_curve / width) ** 2 - 0.5 * ((x - center) / length) ** 2)
            wisps = np.maximum(wisps, rib_density * float(rng.uniform(0.45, 1.0)))
        plume = (wisps * 0.65 + envelope * 0.24) * envelope
        plume *= smoothstep(0.10, 0.77, breakup) * 0.80 + 0.20
        plume *= float(rng.uniform(0.55, 1.0))
        ix = np.ix_(ys, xs % w)
        coverage[ix] = np.maximum(coverage[ix], plume.astype(np.float32))
    # A faint isotropic broken veil connects nearby wisps without long streaks.
    u, v = grid(w, h)
    warp_u = u + (value_noise(w, h, 34, 26) - 0.5) * 0.047
    warp_v = v + (value_noise(w, h, 41, 29) - 0.5) * 0.033
    veil_noise = value_noise(w, h, 93, 61) * 0.45 + value_noise(w, h, 181, 117) * 0.35 + value_noise(w, h, 350, 207) * 0.20
    veil = smoothstep(0.52, 0.86, sample(veil_noise, warp_u, warp_v))
    veil *= smoothstep(0.40, 0.79, value_noise(w, h, 29, 23)) * 0.12
    coverage = np.maximum(coverage, veil)
    coverage *= smoothstep(0.045, 0.20, np.sin(v * np.pi))
    # Preserve the intended sparse layer density after breaking the long bands.
    for _ in range(4):
        coverage = np.clip(coverage * (0.049 / max(float(coverage.mean()), 0.001)), 0.0, 0.88)
    thickness = np.clip(np.power(coverage, 0.86) * 0.63, 0, 1)
    return polar_contract(pack_cloud(coverage, thickness, 8.0))


def wind_field():
    global rng
    rng = np.random.default_rng(SEED)
    # Preserve the original wind asset byte-for-byte while allowing cloud bakes
    # to evolve independently. This is its original deterministic PCG64 state.
    rng.bit_generator.state = {"bit_generator": "PCG64", "state": {"state": 65414650454525648993984009745360550991, "inc": 282633332573489794459843110211945684991}, "has_uint32": 0, "uinteger": 2986992715}
    w, h = 1024, 512
    u, v = grid(w, h)
    latitude = (0.5 - v) * np.pi
    longitude = u * 2 * np.pi
    east = np.broadcast_to(-0.42 * np.cos(latitude * 4.3), (h, w)).copy()
    south = 0.055 * np.sin(longitude * 3.0 + latitude * 5.0)
    east += (value_noise(w, h, 12, 9) - 0.5) * 0.21
    south += (value_noise(w, h, 17, 11) - 0.5) * 0.22
    for _ in range(14):
        cx = float(rng.random())
        cy = float(rng.uniform(0.17, 0.83))
        dx = (np.mod(u - cx + 0.5, 1.0) - 0.5) * max(math.sin(cy * math.pi), 0.25)
        dy = (v - cy) * 0.5
        radius = float(rng.uniform(0.028, 0.065))
        falloff = np.exp(-0.5 * (dx * dx + dy * dy) / (radius * radius))
        strength = float(rng.uniform(-0.28, 0.28)) / radius
        east += -dy * falloff * strength
        south += dx * falloff * strength
    polar_fade = smoothstep(0.025, 0.24, np.cos(latitude)) * np.power(np.cos(latitude), 0.40)
    east = np.clip(east * polar_fade, -0.85, 0.85)
    south = np.clip(south * polar_fade, -0.65, 0.65)
    phase = value_noise(w, h, 15, 10) * 0.75 + value_noise(w, h, 31, 20) * 0.25
    erosion = value_noise(w, h, 85, 48) * 0.50 + value_noise(w, h, 170, 96) * 0.30 + value_noise(w, h, 340, 192) * 0.20
    # Pole values collapse to a single state to avoid seams around singularities.
    phase = phase * polar_fade + 0.5 * (1.0 - polar_fade)
    erosion = erosion * polar_fade + 0.5 * (1.0 - polar_fade)
    return np.round(np.stack([east * 0.5 + 0.5, south * 0.5 + 0.5, phase, erosion], axis=-1) * 255).astype(np.uint8)


def import_config(name, compressed):
    source = 'res://assets/earth/clouds/' + name
    digest = hashlib.md5(source.encode()).hexdigest()
    suffix = '.s3tc.ctex' if compressed else '.ctex'
    cache = 'res://.godot/imported/' + name + '-' + digest + suffix
    remap = ('path.s3tc=' if compressed else 'path=') + json.dumps(cache)
    metadata = '{"imported_formats": ["s3tc"], "vram_texture": true}' if compressed else '{"vram_texture": false}'
    return f'''[remap]

importer="texture"
type="CompressedTexture2D"
{remap}
metadata={metadata}

[deps]

source_file="{source}"
dest_files=["{cache}"]

[params]

compress/mode={2 if compressed else 0}
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


def write_asset(name, data, compressed, stats):
    path = OUT / name
    Image.fromarray(data).save(path, optimize=True)
    (OUT / (name + '.import')).write_text(import_config(name, compressed), encoding='utf-8')
    values = data.astype(np.float32) / 255.0
    channels = {}
    for index, label in enumerate('RGBA'):
        channel = values[:, :, index]
        channels[label] = {'min': float(channel.min()), 'max': float(channel.max()), 'mean': float(channel.mean()), 'p95': float(np.percentile(channel, 95))}
    seam = np.abs(values[:, 0] - values[:, -1]).mean(axis=0)
    adjacency = np.abs(values[:, 1:] - values[:, :-1]).mean(axis=(0, 1))
    stats[name] = {'size': [data.shape[1], data.shape[0]], 'bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'channels': channels, 'longitude_seam_mean_abs_delta': seam.tolist(), 'horizontal_neighbor_mean_abs_delta': adjacency.tolist()}
    print(name, json.dumps(stats[name]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--only', choices=['all', 'clouds', 'main', 'cirrus', 'wind'], default='all')
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    stats_path = OUT / 'BAKE_STATS.json'
    stats = json.loads(stats_path.read_text(encoding='utf-8')) if stats_path.exists() else {}
    stats.update({'seed': SEED, 'cirrus_seed': SEED + 11, 'data_color_space': 'linear', 'normal_gradient_axes': ['+u east', '+v south'], 'gradient_formula': 'encoded = 0.5 + 0.5 * clamp(dHeight/dPixel * gain, -0.95, 0.95) * polar_fade', 'main_gradient_gain': 20.0, 'cirrus_gradient_gain': 8.0, 'polar_cap': {'fully_converged_v': 0.0015, 'blend_end_v': 0.005}})
    if args.only in ('all', 'clouds', 'main'):
        write_asset('main_clouds.png', main_clouds(), True, stats)
    if args.only in ('all', 'clouds', 'cirrus'):
        write_asset('cirrus_clouds.png', cirrus_clouds(), True, stats)
    if args.only in ('all', 'wind'):
        write_asset('wind_field.png', wind_field(), False, stats)
    stats_path.write_text(json.dumps(stats, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
