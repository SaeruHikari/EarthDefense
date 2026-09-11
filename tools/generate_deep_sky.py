"""Bake an original procedural celestial panorama; no third-party image assets.

Run once when authoring the sky. The game only loads the resulting panorama and
does a single direction lookup per pixel; noise octaves and star halos are baked.
"""
from pathlib import Path
import numpy as np
from PIL import Image

DESTINATION = Path(__file__).resolve().parents[1] / "assets" / "space"
WIDTH, HEIGHT = 4096, 2048
SEED = 108273
rng = np.random.default_rng(SEED)


def noise(columns: int, rows: int, seed: int) -> np.ndarray:
    generator = np.random.default_rng(seed)
    lattice = generator.random((rows + 1, columns + 1), dtype=np.float32)
    lattice[:, -1] = lattice[:, 0]
    y = np.linspace(0, rows, HEIGHT, endpoint=False, dtype=np.float32)[:, None]
    x = np.linspace(0, columns, WIDTH, endpoint=False, dtype=np.float32)[None, :]
    iy, ix = y.astype(np.int32), x.astype(np.int32)
    fy, fx = y - iy, x - ix
    fy, fx = fy * fy * (3.0 - 2.0 * fy), fx * fx * (3.0 - 2.0 * fx)
    upper = lattice[iy, ix] * (1.0 - fx) + lattice[iy, ix + 1] * fx
    lower = lattice[iy + 1, ix] * (1.0 - fx) + lattice[iy + 1, ix + 1] * fx
    return (upper * (1.0 - fy) + lower * fy).astype(np.float32)


def paint_star(image: np.ndarray, u: float, v: float, radius: float,
               luminance: float, color: np.ndarray, bright: bool = False) -> None:
    cx, cy = u * WIDTH, v * HEIGHT
    size = int(np.ceil(radius * (7.0 if bright else 3.2)))
    xs = np.arange(int(cx) - size, int(cx) + size + 1)
    ys = np.arange(max(0, int(cy) - size), min(HEIGHT, int(cy) + size + 1))
    if not len(ys):
        return
    # Pixel-center sampling retains small distant stars without oversized disks.
    distance2 = ((xs[None, :] + 0.5 - cx) ** 2 + (ys[:, None] + 0.5 - cy) ** 2)
    core = np.exp(-distance2 / (2.0 * radius * radius))
    if bright:
        core += 0.040 * np.exp(-distance2 / (2.0 * (radius * 3.4) ** 2))
    contribution = core[:, :, None] * luminance * color[None, None, :]
    image[ys[:, None], (xs % WIDTH)[None, :], :] += contribution.astype(np.float32)


def main() -> None:
    DESTINATION.mkdir(parents=True, exist_ok=True)
    longitude = np.linspace(-np.pi, np.pi, WIDTH, endpoint=False, dtype=np.float32)[None, :]
    latitude = np.linspace(np.pi / 2, -np.pi / 2, HEIGHT, dtype=np.float32)[:, None]
    sx = np.cos(latitude) * np.sin(longitude)
    sy = np.sin(latitude) * np.ones_like(longitude)
    sz = -np.cos(latitude) * np.cos(longitude)
    plane = np.array([0.65, 0.755, 0.065], dtype=np.float32)
    plane /= np.linalg.norm(plane)
    galactic_latitude = sx * plane[0] + sy * plane[1] + sz * plane[2]
    n0 = noise(20, 10, 10)
    n1 = noise(56, 28, 42)
    n2 = noise(145, 72, 731)
    n3 = noise(360, 180, 925)
    n4 = noise(850, 425, 481)
    folds = (n0 - 0.5) * 0.048 + (n1 - 0.5) * 0.018
    broad_band = np.exp(-((galactic_latitude + folds) / 0.150) ** 2)
    narrow_band = np.exp(-((galactic_latitude + folds * 1.6) / 0.062) ** 2)
    turbulence = np.clip(0.13 + n0 * 0.29 + n1 * 0.30 + n2 * 0.19 + n3 * 0.09, 0.0, 1.0)
    knots = np.clip((n1 * 0.65 + n2 * 0.35 - 0.30) * 1.9, 0, 1)
    # An offset dust lane snakes through the luminous arm; it is a reduction in
    # volume density, not an artificial dark line over the finished picture.
    dust_center = 0.022 * np.sin(longitude * 5.0 + n0 * 3.5)
    dust_distance = (galactic_latitude + folds - dust_center) / (0.010 + n2 * 0.023)
    dust = np.exp(-(dust_distance ** 2)) * (0.48 + n1 * 0.42)
    filament = np.power(np.clip(1.0 - np.abs(n2 * 2.0 - 1.0), 0, 1), 2.0)
    emission = broad_band * turbulence * 0.11 + narrow_band * knots * (0.065 + 0.05 * filament)
    emission *= 1.0 - dust * 0.87
    emission *= 0.80 + n3 * 0.28 + n4 * 0.12
    cyan_mix = np.clip(0.5 + np.sin(longitude * 3.6 + n0 * 0.7) * 0.46, 0, 1)
    violet = np.array([0.56, 0.34, 0.75], dtype=np.float32)
    cyan = np.array([0.27, 0.68, 0.82], dtype=np.float32)
    tint = violet[None, None, :] * (1 - cyan_mix[:, :, None]) + cyan[None, None, :] * cyan_mix[:, :, None]
    base = np.array([0.018, 0.029, 0.051], dtype=np.float32)
    sky = np.broadcast_to(base, (HEIGHT, WIDTH, 3)).copy()
    sky += emission[:, :, None] * tint * 1.70
    warm_knots = narrow_band * np.power(np.clip((n0 + n2 - 1.04) * 2.1, 0, 1), 2.0) * 0.045
    sky += warm_knots[:, :, None] * np.array([0.85, 0.58, 0.28], dtype=np.float32)
    sky += (broad_band * n4 * 0.004)[:, :, None] * np.array([0.52, 0.61, 0.72], dtype=np.float32)
    # Far-field background stars cover the entire celestial sphere uniformly.
    total_stars = 15500
    for index in range(total_stars):
        u = rng.random()
        latitude_star = np.arcsin(rng.uniform(-1, 1))
        v = 0.5 - latitude_star / np.pi
        rare = index % 197 == 0
        radius = rng.uniform(0.20, 0.42) if not rare else rng.uniform(0.55, 0.85)
        intensity = rng.uniform(0.12, 0.48) if not rare else rng.uniform(0.65, 1.0)
        palette = [np.array([0.60,0.78,1.0]), np.array([0.87,0.92,1.0]), np.array([1.0,0.81,0.60])]
        color = palette[int(rng.choice(3, p=[0.35,0.51,0.14]))]
        paint_star(sky, u, v, radius, intensity, color, rare)
    # Fainter stars are much denser in the galactic arm, giving the band a
    # photographic granular depth instead of a smooth videogame fog ribbon.
    arm_axis = np.cross(plane, np.array([0,0,1], dtype=np.float32))
    arm_axis /= np.linalg.norm(arm_axis)
    arm_second = np.cross(plane, arm_axis)
    for _ in range(11500):
        angle = rng.uniform(0, 2*np.pi)
        spread = rng.normal(0, 0.065)
        direction = arm_axis*np.cos(angle) + arm_second*np.sin(angle) + plane*spread
        direction /= np.linalg.norm(direction)
        u = (np.arctan2(direction[0], -direction[2]) + np.pi) / (2*np.pi)
        v = np.arccos(direction[1]) / np.pi
        paint_star(sky, u, v, rng.uniform(0.14,0.30), rng.uniform(0.04,0.19), np.array([0.71,0.79,0.89]))
    # The cache is display-referred, intentionally restrained for a 2D backdrop.
    pixels = np.clip(sky * 255.0 + 0.5, 0, 255).astype(np.uint8)
    output = DESTINATION / "deep_sky.png"
    Image.fromarray(pixels, "RGB").save(output, optimize=True)
    print(f"Baked {output}: {WIDTH}x{HEIGHT}, {total_stars + 11500} stars, {output.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
