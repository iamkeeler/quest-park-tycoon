# Quest Park Tycoon — procedural clay/diorama prop generator
#
# Run headless:
#   blender --background --python Assets/Blender/generate_assets.py
#
# Produces FBX files in Assets/_Project/Models/:
#   tree.fbx, bench.fbx, lamp_post.fbx, litter_bin.fbx, burger_stall.fbx
#
# Art direction (per Gary): colorful, cartoony, handmade clay / miniature-diorama
# feel. Every prop gets:
#   - low-poly primitives (6-8 sided cylinders, boxes, icospheres)
#   - seeded random vertex jitter -> the imperfect handmade look
#   - flat shading (no smooth normals)
#   - baked vertex colors (no textures; cheap on Quest 3)
#
# Works with Blender 3.x / 4.x.

import bpy
import os
import random
import math

SEED = 20260915
JITTER = 0.035  # meters of handmade wobble on foliage/wood

OUT_DIR = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "_Project", "Models"))
os.makedirs(OUT_DIR, exist_ok=True)


# ---------------------------------------------------------------- helpers

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    # purge orphan meshes
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)


def active_obj():
    return bpy.context.view_layer.objects.active


def deselect_all():
    bpy.ops.object.select_all(action='DESELECT')


def box(name, size, loc):
    deselect_all()
    bpy.ops.mesh.primitive_cube_add(size=size, location=loc)
    o = active_obj()
    o.name = name
    return o


def cylinder(name, verts, radius, depth, loc):
    deselect_all()
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth, location=loc)
    o = active_obj()
    o.name = name
    return o


def ico(name, subdiv, radius, loc):
    deselect_all()
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdiv, radius=radius, location=loc)
    o = active_obj()
    o.name = name
    return o


def uv_sphere(name, seg, rings, radius, loc):
    deselect_all()
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=seg, ring_count=rings, radius=radius, location=loc)
    o = active_obj()
    o.name = name
    return o


def jitter(obj, amount, rng):
    for v in obj.data.vertices:
        v.co.x += (rng.random() * 2.0 - 1.0) * amount
        v.co.y += (rng.random() * 2.0 - 1.0) * amount
        v.co.z += (rng.random() * 2.0 - 1.0) * amount


def paint(obj, color):
    """Bake a flat vertex color (r, g, b, a) onto every vertex."""
    mesh = obj.data
    attr = mesh.color_attributes.new(name="Col", type='FLOAT_COLOR', domain='POINT')
    c = (color[0], color[1], color[2], color[3] if len(color) > 3 else 1.0)
    for i in range(len(mesh.vertices)):
        attr.data[i].color = c


def flatten(obj):
    for p in obj.data.polygons:
        p.use_smooth = False


def build_prop(name, parts):
    """parts: list of (object, jitter_amount or 0). Joins, shades, exports FBX."""
    rng = random.Random(SEED + hash(name) % 10000)
    for obj, jamt in parts:
        if jamt > 0:
            jitter(obj, jamt, rng)
        flatten(obj)
    deselect_all()
    for obj, _ in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0][0]
    bpy.ops.object.join()
    joined = active_obj()
    joined.name = name
    path = os.path.join(OUT_DIR, name.lower() + ".fbx")
    deselect_all()
    joined.select_set(True)
    bpy.context.view_layer.objects.active = joined
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                             apply_scale_options='FBX_SCALE_ALL')
    print("wrote", path)
    deselect_all()
    joined.select_set(True)
    bpy.ops.object.delete(use_global=False)


# ---------------------------------------------------------------- palette (clay colors)

GRASS_D = (0.30, 0.60, 0.27, 1.0)
GRASS_L = (0.42, 0.72, 0.32, 1.0)
BARK = (0.42, 0.28, 0.16, 1.0)
WOOD = (0.62, 0.42, 0.24, 1.0)
WOOD_D = (0.48, 0.32, 0.18, 1.0)
METAL = (0.25, 0.27, 0.30, 1.0)
LAMP_GLOW = (1.0, 0.85, 0.45, 1.0)
BIN_GREEN = (0.30, 0.52, 0.36, 1.0)
CREAM = (0.96, 0.90, 0.78, 1.0)
RED = (0.85, 0.25, 0.22, 1.0)
WHITE = (0.95, 0.95, 0.93, 1.0)


# ---------------------------------------------------------------- props

def make_tree():
    trunk = cylinder("trunk", 6, 0.14, 1.2, (0, 0, 0.6))
    paint(trunk, BARK)
    blob1 = ico("blob1", 1, 0.85, (0, 0, 1.7))
    paint(blob1, GRASS_D)
    blob2 = ico("blob2", 1, 0.6, (0.35, 0.15, 2.25))
    paint(blob2, GRASS_L)
    blob3 = ico("blob3", 1, 0.55, (-0.3, -0.2, 2.2))
    paint(blob3, GRASS_D)
    build_prop("Tree", [(trunk, 0.01), (blob1, JITTER),
                        (blob2, JITTER), (blob3, JITTER)])


def make_bench():
    leg_l = box("legL", 0.12, (-0.7, 0, 0.23))
    leg_l.scale = (1, 2.5, 3.8)
    bpy.context.view_layer.update()
    paint(leg_l, WOOD_D)
    leg_r = box("legR", 0.12, (0.7, 0, 0.23))
    leg_r.scale = (1, 2.5, 3.8)
    bpy.context.view_layer.update()
    paint(leg_r, WOOD_D)
    slats = []
    for i, z in enumerate([-0.18, 0.0, 0.18]):
        s = box("slat%d" % i, 0.09, (0, z, 0.48))
        s.scale = (17, 1, 1)
        bpy.context.view_layer.update()
        paint(s, WOOD)
        slats.append((s, 0.008))
    back = box("back", 0.09, (0, -0.26, 0.85))
    back.scale = (17, 1, 5)
    back.rotation_euler = (math.radians(-12), 0, 0)
    bpy.context.view_layer.update()
    paint(back, WOOD)
    build_prop("Bench", [(leg_l, 0.008), (leg_r, 0.008)] + slats + [(back, 0.008)])


def make_lamp_post():
    pole = cylinder("pole", 8, 0.07, 3.0, (0, 0, 1.5))
    paint(pole, METAL)
    arm = box("arm", 0.08, (0.3, 0, 2.95))
    arm.scale = (8, 1, 1)
    bpy.context.view_layer.update()
    paint(arm, METAL)
    head = uv_sphere("head", 8, 5, 0.22, (0.62, 0, 2.85))
    paint(head, LAMP_GLOW)
    cap = cylinder("cap", 8, 0.26, 0.08, (0.62, 0, 3.05))
    paint(cap, METAL)
    build_prop("LampPost", [(pole, 0.008), (arm, 0.008),
                            (head, 0.01), (cap, 0.008)])


def make_litter_bin():
    body = cylinder("body", 8, 0.32, 0.8, (0, 0, 0.4))
    paint(body, BIN_GREEN)
    rim = cylinder("rim", 8, 0.36, 0.1, (0, 0, 0.82))
    paint(rim, METAL)
    lid = uv_sphere("lid", 8, 4, 0.3, (0, 0, 0.88))
    lid.scale = (1, 1, 0.45)
    bpy.context.view_layer.update()
    paint(lid, METAL)
    build_prop("LitterBin", [(body, 0.012), (rim, 0.008), (lid, 0.01)])


def make_burger_stall():
    body = box("stallBody", 1.0, (0, 0, 1.0))
    body.scale = (2.4, 1.8, 2.0)
    bpy.context.view_layer.update()
    paint(body, CREAM)
    counter = box("counter", 1.0, (0, 1.05, 0.95))
    counter.scale = (2.5, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    # striped awning: alternating red/white slanted slats
    for i in range(6):
        x = -1.05 + i * 0.42
        slat = box("awn%d" % i, 0.4, (x, 0.35, 2.45))
        slat.scale = (1, 3.2, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, RED if i % 2 == 0 else WHITE)
        parts.append((slat, 0.008))
    sign = box("sign", 1.0, (0, 0, 2.95))
    sign.scale = (1.8, 0.18, 0.5)
    bpy.context.view_layer.update()
    paint(sign, RED)
    parts.append((sign, 0.008))
    build_prop("BurgerStall", parts)


# ---------------------------------------------------------------- main

clear_scene()
make_tree()
make_bench()
make_lamp_post()
make_litter_bin()
make_burger_stall()
print("DONE ->", OUT_DIR)
