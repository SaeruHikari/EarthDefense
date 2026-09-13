"""Build EARTHWARD's six original Blender-authored enemy loot pickups.

Run with Blender 5.x, without opening a window:
    blender --background --factory-startup --python tools/build_loot_models.py
Render only the transparent HUD icons from the existing editable source:
    blender --background --factory-startup --python tools/build_loot_models.py -- --icons-only

Outputs are self-contained glTF 2 binaries and an editable Blender library.
Editable sources and the contact sheet live in source/, ignored by Godot.
The source scenes use Blender +Z up; the exporter converts them to glTF +Y up.
All game meshes have identity transforms and a maximum vertex radius of 0.48.
The separate presentation scene is never included in the game exports.
"""

from pathlib import Path
import json
import math
import struct
import sys
import hashlib

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets" / "models" / "loot"
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = OUT / "source"
SOURCE.mkdir(parents=True, exist_ok=True)
(SOURCE / ".gdignore").touch()
TAU = math.tau
PI = math.pi


def material(name, color, metal=0.0, rough=0.35, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
    bsdf.inputs["Emission Strength"].default_value = emission
    return mat


def apply_mat(obj, mat):
    obj.data.materials.append(mat)
    return obj


def raw_mesh(name, vertices, faces, mat):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    apply_mat(obj, mat)
    return obj


def box(name, location, dimensions, mat, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Machined chamfer", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return apply_mat(obj, mat)


def cylinder(name, location, radius, depth, mat, sides=12, chamfer=0.0):
    if chamfer:
        rings = [(-depth / 2, radius - chamfer),
                 (-depth / 2 + chamfer, radius),
                 (depth / 2 - chamfer, radius),
                 (depth / 2, radius - chamfer)]
    else:
        rings = [(-depth / 2, radius), (depth / 2, radius)]
    vertices = [(r * math.cos(TAU * i / sides), r * math.sin(TAU * i / sides), z)
                for z, r in rings for i in range(sides)]
    faces = [tuple(reversed(range(sides))),
             tuple(range((len(rings) - 1) * sides, len(rings) * sides))]
    for j in range(len(rings) - 1):
        for i in range(sides):
            k = (i + 1) % sides
            faces.append((j * sides + i, j * sides + k,
                          (j + 1) * sides + k, (j + 1) * sides + i))
    obj = raw_mesh(name, vertices, faces, mat)
    obj.location = location
    return obj


def bar(name, start, end, radius, mat, sides=6):
    a, b = Vector(start), Vector(end)
    obj = cylinder(name, (a + b) / 2, radius, (b - a).length, mat, sides)
    obj.rotation_euler = (b - a).to_track_quat("Z", "Y").to_euler()
    return obj


def ring(name, radius, thickness, location, mat, rotation=(0, 0, 0), segments=24, cross=4):
    vertices = []
    for i in range(segments):
        a = TAU * i / segments
        for j in range(cross):
            b = TAU * j / cross + PI / 4
            vertices.append(((radius + thickness * math.cos(b)) * math.cos(a),
                             (radius + thickness * math.cos(b)) * math.sin(a),
                             thickness * math.sin(b)))
    faces = []
    for i in range(segments):
        for j in range(cross):
            faces.append((i * cross + j, ((i + 1) % segments) * cross + j,
                          ((i + 1) % segments) * cross + (j + 1) % cross,
                          i * cross + (j + 1) % cross))
    obj = raw_mesh(name, vertices, faces, mat)
    obj.location = location
    obj.rotation_euler = rotation
    return obj


def crystal(name, location, radius, height, mat, sides=5, tilt=(0, 0, 0)):
    vertices = [(radius * math.cos(TAU * i / sides),
                 radius * math.sin(TAU * i / sides), -height * 0.40)
                for i in range(sides)]
    vertices += [(radius * math.cos(TAU * i / sides),
                  radius * math.sin(TAU * i / sides), height * 0.19)
                 for i in range(sides)]
    vertices += [(radius * 0.20, -radius * 0.12, height * 0.60), (0, 0, -height * 0.51)]
    faces = []
    for i in range(sides):
        k = (i + 1) % sides
        faces += [(i, k, sides + k, sides + i),
                  (sides + i, sides + k, sides * 2),
                  (k, i, sides * 2 + 1)]
    obj = raw_mesh(name, vertices, faces, mat)
    obj.location = location
    obj.rotation_euler = tilt
    return obj


def ico(name, location, scale, mat, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale if isinstance(scale, tuple) else (scale,) * 3
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return apply_mat(obj, mat)


def render_icons(models):
    """Render the same six meshes as clean RGBA cutouts for HUD pickup flight."""
    icon_directory = ROOT / "assets" / "ui" / "loot"
    icon_directory.mkdir(parents=True, exist_ok=True)
    scene = bpy.data.scenes.new("PRESENTATION - transparent HUD icons")
    bpy.context.window.scene = scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.cycles.seed = 17
    scene.render.resolution_x = scene.render.resolution_y = 192
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "AgX"
    scene.world = bpy.data.worlds.new("HUD icon reflection environment")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.14, 0.18, 0.24, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.45

    bpy.ops.object.camera_add(location=(0, -3, 1.9))
    camera = bpy.context.object
    camera.name = "HUD icon orthographic camera"
    camera.rotation_euler = (-camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    scene.camera = camera
    camera_basis = camera.rotation_euler.to_matrix()
    camera_right, camera_up = camera_basis.col[0], camera_basis.col[1]

    for name, position, energy, color, size in [
        ("Soft frontal key", (-2.5, -3.5, 4.5), 300, (0.85, 0.93, 1.0), 3),
        ("Warm metal fill", (3.0, -2.0, 1.6), 160, (1.0, 0.89, 0.74), 3),
        ("Cool edge reflection", (1.0, 2.0, 3.0), 240, (0.65, 0.81, 1.0), 2),
    ]:
        bpy.ops.object.light_add(type="AREA", location=position)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.color = color
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (-light.location).to_track_quat("-Z", "Y").to_euler()

    display = bpy.data.objects.new("HUD icon model", next(iter(models.values())).data)
    scene.collection.objects.link(display)
    report = {}
    for currency, model in models.items():
        display.data = model.data
        display.rotation_euler = (.12, -.12, -.28)
        if currency == "resource_cores":
            display.rotation_euler = (.12, .10, -.42)
        if currency in ("alien_points", "alien_chips"):
            display.rotation_euler = (.06, -.08, -.17)
        bpy.context.view_layer.update()
        world_vertices = [display.matrix_world @ vertex.co for vertex in display.data.vertices]
        projected_x = [vertex.dot(camera_right) for vertex in world_vertices]
        projected_y = [vertex.dot(camera_up) for vertex in world_vertices]
        width, height = max(projected_x) - min(projected_x), max(projected_y) - min(projected_y)
        center_x = (max(projected_x) + min(projected_x)) / 2
        center_y = (max(projected_y) + min(projected_y)) / 2
        camera.location = Vector((0, -3, 1.9)) + camera_right * center_x + camera_up * center_y
        camera.data.ortho_scale = max(width, height) / .78
        path = icon_directory / f"loot_{currency}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)

        # Check the actual saved PNG, including transparent corners and framing.
        rendered = bpy.data.images.load(str(path), check_existing=False)
        assert tuple(rendered.size) == (192, 192) and rendered.channels == 4
        pixels = list(rendered.pixels)
        occupied = [(index % 192, index // 192)
                    for index in range(192 * 192) if pixels[index * 4 + 3] > .01]
        assert occupied
        min_x, max_x = min(p[0] for p in occupied), max(p[0] for p in occupied)
        min_y, max_y = min(p[1] for p in occupied), max(p[1] for p in occupied)
        occupancy = max(max_x - min_x + 1, max_y - min_y + 1) / 192
        assert .70 <= occupancy <= .82
        assert all(pixels[index * 4 + 3] == 0 for index in (0, 191, 192 * 191, 192 * 192 - 1))
        report[currency] = {"file": str(path.relative_to(ROOT)).replace("\\", "/"),
                            "size": [192, 192], "channels": "RGBA", "longest_occupied_fraction": round(occupancy, 4),
                            "alpha_bounds_xy": [min_x, min_y, max_x, max_y], "size_bytes": path.stat().st_size}
        bpy.data.images.remove(rendered)
    print("LOOT_HUD_ICON_REPORT " + json.dumps(report))


if "--icons-only" in sys.argv:
    glb_hashes = {path: hashlib.sha256(path.read_bytes()).hexdigest() for path in OUT.glob("loot_*.glb")}
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE / "loot_pickups.blend"))
    existing_models = {currency: bpy.data.objects[f"loot_{currency}"] for currency in
                       ("minerals", "energy", "science", "resource_cores", "alien_points", "alien_chips")}
    render_icons(existing_models)
    assert glb_hashes == {path: hashlib.sha256(path.read_bytes()).hexdigest() for path in OUT.glob("loot_*.glb")}
    print("LOOT_GLB_HASHES_UNCHANGED True")
    sys.exit(0)


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
for data in list(bpy.data.materials):
    bpy.data.materials.remove(data)

GRAPHITE = material("Frame - midnight ceramic metal", (0.040, 0.065, 0.085), 0.78, 0.29)
STEEL = material("Edges - satin titanium", (0.44, 0.56, 0.65), 0.85, 0.25)
COLORS = {
    "minerals": (0.07, 0.82, 0.91),
    "energy": (1.00, 0.64, 0.08),
    "science": (0.16, 0.42, 1.00),
    "resource_cores": (1.00, 0.27, 0.075),
    "alien_points": (0.68, 0.18, 0.97),
    "alien_chips": (0.22, 0.95, 0.34),
}
PALETTES = {}
for key, rgb in COLORS.items():
    PALETTES[key] = (
        material(f"{key} - alloy", tuple(v * 0.42 for v in rgb), 0.70, 0.27),
        material(f"{key} - luminous inlay", rgb, 0.18, 0.23, 0.38),
    )


def minerals():
    alloy, glow = PALETTES["minerals"]
    cylinder("Hexagonal ore cradle", (0, 0, -0.235), 0.27, 0.125, GRAPHITE, 6, 0.025)
    cylinder("Titanium rim", (0, 0, -0.177), 0.245, 0.035, STEEL, 6)
    crystal("Primary mineral crystal", (0.02, 0.025, 0.07), 0.138, 0.55, glow, 5, (0.10, -0.13, 0.20))
    crystal("Mineral shard left", (-0.145, -0.025, -0.035), 0.092, 0.38, alloy, 5, (0.10, -0.28, 0))
    crystal("Mineral shard right", (0.13, -0.085, -0.075), 0.082, 0.29, glow, 5, (-0.15, 0.26, 0))
    for i in range(3):
        a = TAU * i / 3 + PI / 6
        p = (0.226 * math.cos(a), 0.226 * math.sin(a))
        bar("Cradle grip", (*p, -0.23), (*p, -0.07), 0.027, STEEL)


def energy():
    alloy, glow = PALETTES["energy"]
    cylinder("Charged octagonal cell", (0, 0, 0), 0.16, 0.47, glow, 8, 0.02)
    for z in [-0.26, 0.26]:
        cylinder("Battery armored cap", (0, 0, z), 0.245, 0.105, GRAPHITE, 8, 0.023)
        cylinder("Cap titanium lip", (0, 0, z * 1.15), 0.20, 0.037, STEEL, 8)
        cylinder("Terminal contact", (0, 0, z * 1.30), 0.11, 0.07, alloy, 8, 0.012)
    for i in range(3):
        a = TAU * i / 3 + PI / 6
        p = (0.21 * math.cos(a), 0.21 * math.sin(a))
        bar("Protective cell rail", (*p, -0.225), (*p, 0.225), 0.029, STEEL)
    ring("Charge belt", 0.172, 0.022, (0, 0, 0), alloy, segments=16)


def science():
    alloy, glow = PALETTES["science"]
    ico("Research data nucleus", (0, 0, 0), (0.13, 0.13, 0.19), glow, 1)
    ring("Orbital archive ring A", 0.295, 0.026, (0, 0, 0), STEEL, (PI / 2, 0, 0))
    ring("Orbital archive ring B", 0.295, 0.026, (0, 0, 0), alloy, (PI / 2, PI / 3, PI / 3))
    ring("Orbital archive ring C", 0.295, 0.026, (0, 0, 0), STEEL, (PI / 2, -PI / 3, -PI / 3))
    for sign in (-1, 1):
        cylinder("Research capsule endcap", (0, 0, sign * 0.305), 0.095, 0.065, GRAPHITE, 8, 0.012)
        cylinder("Endcap blue indicator", (0, 0, sign * 0.34), 0.052, 0.014, glow, 8)
    ico("Orbiting sample", (0.292, 0, 0), 0.047, glow)
    ico("Orbiting sample", (-0.205, 0, 0.205), 0.040, glow)


def resource_cores():
    alloy, glow = PALETTES["resource_cores"]
    core = box("Compressed resource core", (0, 0, 0), (0.315, 0.315, 0.315), glow, 0.033)
    for axis in range(3):
        for s in (-1, 1):
            position = [0.0, 0.0, 0.0]
            position[axis] = s * 0.181
            dims = [0.235, 0.235, 0.235]
            dims[axis] = 0.035
            box("Core face plate", position, dims, alloy, 0.008)
            position[axis] = s * 0.202
            dims = [0.104, 0.104, 0.104]
            dims[axis] = 0.014
            box("Face power inset", position, dims, glow)
    for axis in range(3):
        other = [i for i in range(3) if i != axis]
        for a in (-1, 1):
            for b in (-1, 1):
                start, end = [0, 0, 0], [0, 0, 0]
                start[axis], end[axis] = -0.23, 0.23
                for point in (start, end):
                    point[other[0]], point[other[1]] = a * 0.225, b * 0.225
                bar("Structural cube edge", start, end, 0.029, STEEL, 4)
    for x in (-1, 1):
        for y in (-1, 1):
            for z in (-1, 1):
                box("Armored corner block", (x * 0.225, y * 0.225, z * 0.225), (0.078,) * 3, GRAPHITE, 0.008)


def alien_points():
    alloy, glow = PALETTES["alien_points"]
    # Alien triangular currency seal with a suspended faceted nucleus.
    vertices = [(0.31 * math.sin(TAU * i / 3), 0, 0.31 * math.cos(TAU * i / 3)) for i in range(3)]
    for i in range(3):
        a, b = Vector(vertices[i]), Vector(vertices[(i + 1) % 3])
        bar("Triangular alien seal", a, b, 0.044, alloy, 4)
        inset_a, inset_b = a * 0.84, b * 0.84
        inset_a.y = inset_b.y = -0.031
        bar("Seal luminous engraving", inset_a, inset_b, 0.012, glow, 4)
        ico("Alien corner clasp", vertices[i], 0.055, GRAPHITE)
        direction = a.normalized()
        shard = crystal("Three-point alien thorn", a * 1.04, 0.065, 0.225, STEEL, 4)
        shard.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    crystal("Suspended alien nucleus", (0, 0, -0.015), 0.095, 0.29, glow, 4, (0, 0, PI / 4))
    ring("Nucleus equatorial band", 0.094, 0.015, (0, 0, -0.012), GRAPHITE, segments=12)


def alien_chips():
    alloy, glow = PALETTES["alien_chips"]
    box("Alien circuit wafer", (0, 0, 0), (0.41, 0.105, 0.51), GRAPHITE, 0.045)
    box("Alien substrate front", (0, -0.060, 0), (0.34, 0.031, 0.43), alloy, 0.026)
    box("Alien substrate back", (0, 0.060, 0), (0.34, 0.031, 0.43), alloy, 0.026)
    for side in (-1, 1):
        for z in (-0.17, -0.055, 0.055, 0.17):
            box("Chip edge contact", (side * 0.23, 0, z), (0.094, 0.075, 0.043), STEEL, 0.009)
    for side in (-1, 1):
        core = box("Central alien processor", (0, side * 0.086, 0), (0.153, 0.05, 0.153), glow, 0.017)
        core.rotation_euler[1] = PI / 4
        for sign in (-1, 1):
            bar("Printed energy trace", (sign * 0.06, side * 0.079, 0.09),
                (sign * 0.06, side * 0.079, 0.187), 0.010, glow, 4)
            bar("Printed energy trace", (sign * 0.06, side * 0.079, -0.09),
                (sign * 0.06, side * 0.079, -0.187), 0.010, glow, 4)
        for z in (-0.182, 0.182):
            bar("Circuit cross trace", (-0.10, side * 0.079, z), (0.10, side * 0.079, z), 0.009, glow, 4)


BUILDERS = {
    "minerals": minerals,
    "energy": energy,
    "science": science,
    "resource_cores": resource_cores,
    "alien_points": alien_points,
    "alien_chips": alien_chips,
}
LABELS = {
    "minerals": ("MINERALS", "FACETED ORE"),
    "energy": ("ENERGY", "CHARGED CELL"),
    "science": ("RESEARCH", "ORBITAL ARCHIVE"),
    "resource_cores": ("RESOURCE CORE", "COMPRESSED MATTER"),
    "alien_points": ("ALIEN POINTS", "TRIAD SEAL"),
    "alien_chips": ("ALIEN CHIP", "CIRCUIT WAFER"),
}
manifest = {
    "generator": "tools/build_loot_models.py",
    "asset_directory": "assets/models/loot",
    "source_directory": "assets/models/loot/source",
    "blender_version": bpy.app.version_string,
    "units": "meters",
    "gltf_up_axis": "+Y",
    "maximum_vertex_radius": 0.48,
    "emission_strength": 0.38,
    "notes": "Original procedural meshes authored in Blender. Opaque PBR. No textures, lights, cameras, animation or presentation props in GLBs.",
    "models": {},
}
game_meshes = {}
for key, builder in BUILDERS.items():
    scene = bpy.data.scenes.new(f"SOURCE - {key}")
    bpy.context.window.scene = scene
    builder()
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = next(obj for obj in scene.objects if obj.type == "MESH")
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = f"loot_{key}"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    low = Vector(tuple(min(v.co[i] for v in obj.data.vertices) for i in range(3)))
    high = Vector(tuple(max(v.co[i] for v in obj.data.vertices) for i in range(3)))
    center = (low + high) / 2
    for vertex in obj.data.vertices:
        vertex.co -= center
    radius = max(v.co.length for v in obj.data.vertices)
    for vertex in obj.data.vertices:
        vertex.co *= 0.48 / radius
    obj.data.update()
    obj.data.calc_loop_triangles()
    bounds = [[min(v.co[i] for v in obj.data.vertices), max(v.co[i] for v in obj.data.vertices)] for i in range(3)]
    gltf_bounds = [bounds[0], bounds[2], [-bounds[1][1], -bounds[1][0]]]
    triangles = len(obj.data.loop_triangles)
    obj["resource_type"] = key
    obj["max_radius"] = 0.48
    obj["triangle_count"] = triangles
    game_meshes[key] = obj
    glb_path = OUT / f"loot_{key}.glb"
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB", use_selection=True,
                              use_active_scene=True,
                              export_yup=True, export_animations=False, export_cameras=False,
                              export_lights=False, export_extras=True, export_materials="EXPORT")
    # Verify the exported binary itself. Blender selection is per scene, so the
    # active-scene export flag above is essential for this multi-scene library.
    payload = glb_path.read_bytes()
    json_size = struct.unpack_from("<I", payload, 12)[0]
    gltf = json.loads(payload[20:20 + json_size])
    assert len(gltf["scenes"]) == 1 and len(gltf["meshes"]) == 1
    assert len(gltf["nodes"]) == 1
    assert not gltf.get("images") and not gltf.get("cameras")
    binary_start = 20 + json_size + 8
    export_triangles = 0
    export_vertices = []
    for primitive in gltf["meshes"][0]["primitives"]:
        assert primitive.get("mode", 4) == 4
        export_triangles += gltf["accessors"][primitive["indices"]]["count"] // 3
        accessor = gltf["accessors"][primitive["attributes"]["POSITION"]]
        view = gltf["bufferViews"][accessor["bufferView"]]
        base = binary_start + view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        for vi in range(accessor["count"]):
            vertex = struct.unpack_from("<3f", payload, base + vi * view.get("byteStride", 12))
            assert all(math.isfinite(value) for value in vertex)
            assert Vector(vertex).length <= 0.480001
            export_vertices.append(vertex)
    assert export_triangles == triangles and triangles < 1000
    for axis in range(3):
        assert abs(min(v[axis] for v in export_vertices) + max(v[axis] for v in export_vertices)) < 1e-6
    manifest["models"][key] = {
        "file": "../" + glb_path.name,
        "mesh_count": 1,
        "triangles": triangles,
        "vertices_before_gltf_split": len(obj.data.vertices),
        "material_surfaces": len(obj.data.materials),
        "bounds_gltf_xyz": [[round(v, 6) for v in axis] for axis in gltf_bounds],
        "size_bytes": glb_path.stat().st_size,
        "silhouette": LABELS[key][1].lower(),
        "color_linear_rgb": COLORS[key],
        "export_validation": "single scene/node/mesh; centered finite vertices; radius <= 0.480001; triangle count verified",
    }

# Camera-facing gallery cards allow direct comparison, while all pickup copies
# retain the same world radius. Each source remains editable in its own scene.
gallery = bpy.data.scenes.new("PRESENTATION - six enemy pickups")
bpy.context.window.scene = gallery
gallery.render.engine = "CYCLES"
gallery.cycles.samples = 40
gallery.cycles.use_denoising = True
gallery.render.resolution_x = 1800
gallery.render.resolution_y = 1400
gallery.render.resolution_percentage = 100
gallery.render.image_settings.file_format = "PNG"
gallery.render.film_transparent = False
gallery.world = bpy.data.worlds.new("Gallery ambient")
gallery.world.use_nodes = True
gallery.world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.23, 0.31, 1)
gallery.world.node_tree.nodes["Background"].inputs[1].default_value = 0.50
gallery.view_settings.view_transform = "AgX"

bpy.ops.object.camera_add(location=(0, -10, 7))
camera = bpy.context.object
camera.name = "Contact sheet camera"
target = Vector((0, 0, 0))
camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 6.4
gallery.camera = camera
camera_quat = camera.rotation_euler.to_quaternion()
right = camera_quat @ Vector((1, 0, 0))
up = camera_quat @ Vector((0, 1, 0))
forward = camera_quat @ Vector((0, 0, -1))

CARD = material("Presentation - midnight card", (0.021, 0.032, 0.048), 0.05, 0.75)
BG = material("Presentation - background", (0.008, 0.015, 0.027), 0.0, 0.85)
TEXT = material("Presentation - heading", (0.74, 0.84, 0.94), 0, 0.5, 0.5)
SUBTEXT = material("Presentation - small type", (0.24, 0.35, 0.47), 0, 0.5, 0.45)


def position_screen(x, y, depth=0):
    return right * x + up * y + forward * depth


def screen_box(name, x, y, width, height, mat, depth=0.6):
    obj = box(name, position_screen(x, y, depth), (width, height, 0.025), mat, 0.025)
    obj.rotation_euler = camera.rotation_euler
    return obj


def screen_text(body, x, y, size, mat, align="CENTER", depth=-0.15):
    curve = bpy.data.curves.new(body, "FONT")
    curve.body = body
    curve.align_x = align
    curve.size = size
    curve.space_character = 1.12
    obj = bpy.data.objects.new(body, curve)
    gallery.collection.objects.link(obj)
    obj.location = position_screen(x, y, depth)
    obj.rotation_euler = camera.rotation_euler
    obj.data.materials.append(mat)
    return obj


screen_box("Gallery backdrop", 0, 0, 7, 6, BG, 0.85)
screen_text("EARTHWARD  /  SALVAGE COLLECTION", -2.96, 2.19, 0.158, TEXT, "LEFT")
screen_text("SIX ENEMY PICKUPS     /     BLENDER PBR MESHES", -2.96, 1.95, 0.075, SUBTEXT, "LEFT")
for i, (key, model) in enumerate(game_meshes.items()):
    column, row = i % 3, i // 3
    x, y = (column - 1) * 2.03, 0.85 - row * 1.83
    screen_box("Pickup card", x, y, 1.91, 1.68, CARD)
    screen_box("Resource color key", x - 0.78, y + 0.67, 0.19, 0.023, PALETTES[key][1], 0.54)
    screen_text(f"0{i + 1}", x + 0.79, y + 0.62, 0.087, SUBTEXT)
    display = bpy.data.objects.new(f"DISPLAY - {key}", model.data)
    gallery.collection.objects.link(display)
    display.location = position_screen(x, y + 0.12, -0.07)
    display.rotation_euler = (0.12, -0.17, -0.32 if key != "alien_chips" else -0.21)
    # Avoid a point-on view for the cube while preserving every object's shape.
    if key == "resource_cores":
        display.rotation_euler = (0.12, 0.1, -0.42)
    screen_text(LABELS[key][0], x, y - 0.52, 0.112, TEXT)
    screen_text(LABELS[key][1], x, y - 0.69, 0.057, SUBTEXT)
screen_text("METALLIC FRAMES   /   CONTROLLED EMISSION   /   0.48 m MAX RADIUS", 0, -2.08, 0.073, SUBTEXT)


def area_light(name, location, power, color, size):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = power
    light.data.color = color
    light.data.shape = "DISK"
    light.data.size = size
    light.rotation_euler = (-light.location).to_track_quat("-Z", "Y").to_euler()


area_light("Large cool key", (-3, -4, 6), 1100, (0.77, 0.89, 1.0), 5)
area_light("Soft warm fill", (4, -2, 3), 780, (1.0, 0.84, 0.64), 4)
area_light("Rim strip", (0, 3, 5), 1000, (0.52, 0.76, 1.0), 3)
gallery.render.filepath = str(SOURCE / "loot_contact_sheet.png")
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "loot_pickups.blend"))
(SOURCE / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
bpy.ops.render.render(write_still=True)
render_icons(game_meshes)
print("LOOT_ASSET_MANIFEST " + json.dumps(manifest))
