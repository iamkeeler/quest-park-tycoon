# Quest Park Tycoon — procedural clay/diorama prop generator, PART 3
#
# Run headless:
#   blender --background --python Assets/Blender/generate_assets_p3.py
#
# Produces FBX files in Assets/_Project/Models/:
#   twist.fbx, topspin.fbx, gokarts.fbx, launchedfreefall.fbx,
#   hauntedhouse.fbx, observationtower.fbx, friesstall.fbx,
#   pizzastall.fbx, icecreamstall.fbx, popcornstall.fbx, coffeestall.fbx,
#   balloonstall.fbx, souvenirstall.fbx, bathroom.fbx,
#   securityfigure.fbx, entertainerfigure.fbx
#
# Same art direction + conventions as generate_assets.py (part 1) and
# generate_assets_p2.py (part 2):
#   - low-poly primitives (6-12 sided cylinders, boxes, icospheres)
#   - seeded random vertex jitter -> handmade clay look
#   - flat shading, baked vertex colors, one material max per asset
#   - every asset < 2000 tris (tri count printed per asset)
#
# Works with Blender 3.x / 4.x.

import bpy
import os
import random
import math
import zlib

SEED = 20260915
JITTER = 0.035  # meters of handmade wobble on organic/clay parts

OUT_DIR = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "_Project", "Models"))
os.makedirs(OUT_DIR, exist_ok=True)

TRI_LOG = []  # (asset name, tri count) for the end-of-run summary


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


def cone(name, verts, radius, depth, loc):
    deselect_all()
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=radius, depth=depth, location=loc)
    o = active_obj()
    o.name = name
    return o


def torus(name, major_seg, minor_seg, major_r, minor_r, loc):
    deselect_all()
    bpy.ops.mesh.primitive_torus_add(
        major_segments=major_seg, minor_segments=minor_seg,
        major_radius=major_r, minor_radius=minor_r, location=loc)
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
    # Stable per-asset seed: zlib.crc32 is deterministic across processes,
    # unlike Python's hash() which is salted per-process.
    rng = random.Random(SEED + zlib.crc32(name.encode("utf-8")) % 10000)
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
    # triangulated tri count (n-gon -> n-2 tris), must stay < 2000
    tris = sum(len(p.vertices) - 2 for p in joined.data.polygons)
    TRI_LOG.append((name, tris))
    path = os.path.join(OUT_DIR, name.lower() + ".fbx")
    deselect_all()
    joined.select_set(True)
    bpy.context.view_layer.objects.active = joined
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                             apply_scale_options='FBX_SCALE_ALL')
    print("wrote %s - %d tris" % (path, tris))
    deselect_all()
    joined.select_set(True)
    bpy.ops.object.delete(use_global=False)


# ---------------------------------------------------------------- palette (clay colors)

CREAM = (0.96, 0.90, 0.78, 1.0)
RED = (0.85, 0.25, 0.22, 1.0)
WHITE = (0.95, 0.95, 0.93, 1.0)
YELLOW = (0.95, 0.78, 0.18, 1.0)
ORANGE = (0.95, 0.55, 0.22, 1.0)
BLUE = (0.28, 0.47, 0.85, 1.0)
PINK = (0.93, 0.50, 0.66, 1.0)
PINK_L = (0.97, 0.72, 0.80, 1.0)
TEAL = (0.30, 0.68, 0.66, 1.0)
SKIN = (0.96, 0.78, 0.62, 1.0)
DARK = (0.22, 0.22, 0.26, 1.0)
NAVY = (0.20, 0.28, 0.50, 1.0)
METAL = (0.25, 0.27, 0.30, 1.0)
METAL_D = (0.35, 0.38, 0.42, 1.0)
WOOD = (0.62, 0.42, 0.24, 1.0)
WOOD_D = (0.48, 0.32, 0.18, 1.0)
PATH = (0.76, 0.71, 0.62, 1.0)
PATH_D = (0.64, 0.59, 0.51, 1.0)

GREEN = (0.35, 0.70, 0.35, 1.0)
PURPLE = (0.55, 0.35, 0.70, 1.0)
BROWN = (0.45, 0.30, 0.18, 1.0)
GOLD = (0.95, 0.75, 0.30, 1.0)
GLASS = (0.65, 0.85, 0.95, 1.0)
LIME = (0.60, 0.85, 0.25, 1.0)

PASTELS = [
    (0.95, 0.65, 0.70, 1.0),  # pastel pink
    (0.55, 0.75, 0.92, 1.0),  # pastel blue
    (0.98, 0.88, 0.55, 1.0),  # pastel yellow
    (0.60, 0.85, 0.65, 1.0),  # pastel green
    (0.75, 0.65, 0.90, 1.0),  # pastel purple
]


# ---------------------------------------------------------------- rides (Phase 2 set)

def make_twist():
    """Teacups ride: round platform, center column, 4 spinning cups."""
    platform = cylinder("platform", 12, 2.4, 0.35, (0, 0, 0.18))
    paint(platform, CREAM)
    rim = torus("rim", 16, 6, 2.4, 0.10, (0, 0, 0.38))
    paint(rim, RED)
    column = cylinder("column", 8, 0.35, 2.2, (0, 0, 1.4))
    paint(column, YELLOW)
    top = cone("top", 8, 0.9, 0.8, (0, 0, 2.9))
    paint(top, RED)
    parts = [(platform, 0.012), (rim, 0.008), (column, 0.008), (top, 0.01)]
    for i in range(4):
        th = i * math.pi / 2.0 + math.pi / 4.0
        cx, cy = 1.4 * math.cos(th), 1.4 * math.sin(th)
        saucer = cylinder("saucer%d" % i, 8, 0.62, 0.10, (cx, cy, 0.42))
        paint(saucer, METAL_D)
        cup = cylinder("cup%d" % i, 8, 0.42, 0.55, (cx, cy, 0.72))
        cup.scale = (1, 1, 1)
        bpy.context.view_layer.update()
        paint(cup, PASTELS[i % len(PASTELS)])
        lid = uv_sphere("lid%d" % i, 8, 4, 0.30, (cx, cy, 1.05))
        paint(lid, WHITE)
        parts += [(saucer, 0.006), (cup, 0.01), (lid, 0.01)]
    build_prop("Twist", parts)


def make_top_spin():
    """Top-spin: two A-frame supports holding a flipping row of seats."""
    parts = []
    for i, sx in enumerate((-1.8, 1.8)):
        for j, sgn in enumerate((-1, 1)):
            leg = box("leg%d%d" % (i, j), 0.30, (sx, sgn * 0.75, 1.6))
            leg.scale = (1, 1, 11)
            leg.rotation_euler = (math.radians(sgn * 25), 0, 0)
            bpy.context.view_layer.update()
            paint(leg, METAL)
            parts.append((leg, 0.008))
        hub = cylinder("hub%d" % i, 8, 0.25, 0.4, (sx, 0, 3.1))
        hub.rotation_euler = (math.radians(90), 0, 0)
        bpy.context.view_layer.update()
        paint(hub, METAL_D)
        parts.append((hub, 0.006))
    arm = box("arm", 1.0, (0, 0, 2.2))
    arm.scale = (4.2, 0.3, 0.3)
    bpy.context.view_layer.update()
    paint(arm, RED)
    parts.append((arm, 0.008))
    bench = box("bench", 1.0, (0, 0, 1.55))
    bench.scale = (3.6, 0.7, 0.35)
    bpy.context.view_layer.update()
    paint(bench, YELLOW)
    backrest = box("backrest", 1.0, (0, -0.35, 1.95))
    backrest.scale = (3.6, 0.25, 0.9)
    bpy.context.view_layer.update()
    paint(backrest, BLUE)
    parts += [(bench, 0.01), (backrest, 0.01)]
    for i in range(4):
        x = -1.35 + i * 0.9
        divider = box("div%d" % i, 0.5, (x, 0, 1.75))
        divider.scale = (0.15, 1.2, 1.4)
        bpy.context.view_layer.update()
        paint(divider, RED)
        parts.append((divider, 0.006))
    build_prop("TopSpin", parts)


def make_go_karts():
    """Go-kart: chunky kart + checkered start tile."""
    tile = box("tile", 1.0, (0, 0, 0.05))
    tile.scale = (2.4, 2.4, 0.1)
    bpy.context.view_layer.update()
    paint(tile, DARK)
    parts = [(tile, 0.004)]
    for i in range(4):
        for j in range(4):
            if (i + j) % 2 == 0:
                sq = box("chk%d%d" % (i, j), 0.58,
                         (-0.9 + i * 0.6, -0.9 + j * 0.6, 0.105))
                sq.scale = (1, 1, 0.02)
                bpy.context.view_layer.update()
                paint(sq, WHITE)
                parts.append((sq, 0.003))
    chassis = box("chassis", 1.0, (0, 0, 0.45))
    chassis.scale = (1.6, 0.9, 0.25)
    bpy.context.view_layer.update()
    paint(chassis, RED)
    nose = box("nose", 0.7, (0.95, 0, 0.42))
    nose.rotation_euler = (0, math.radians(-15), 0)
    bpy.context.view_layer.update()
    paint(nose, RED)
    seat = box("seat", 0.45, (-0.35, 0, 0.62))
    paint(seat, DARK)
    back = box("back", 0.45, (-0.62, 0, 0.85))
    back.scale = (0.35, 1.0, 1.6)
    bpy.context.view_layer.update()
    paint(back, DARK)
    parts += [(chassis, 0.01), (nose, 0.008), (seat, 0.006), (back, 0.006)]
    for i, wx in enumerate((-0.55, 0.55)):
        for j, wy in enumerate((-0.48, 0.48)):
            wheel = cylinder("wheel%d%d" % (i, j), 8, 0.17, 0.14, (wx, wy, 0.17))
            wheel.rotation_euler = (math.radians(90), 0, 0)
            bpy.context.view_layer.update()
            paint(wheel, DARK)
            parts.append((wheel, 0.004))
    steer_col = cylinder("steercol", 6, 0.05, 0.5, (0.35, 0, 0.75))
    steer_col.rotation_euler = (0, math.radians(-30), 0)
    bpy.context.view_layer.update()
    paint(steer_col, METAL)
    wheel_rim = torus("wheelrim", 8, 4, 0.16, 0.04, (0.48, 0, 0.95))
    wheel_rim.rotation_euler = (0, math.radians(60), 0)
    bpy.context.view_layer.update()
    paint(wheel_rim, DARK)
    parts += [(steer_col, 0.004), (wheel_rim, 0.004)]
    build_prop("GoKarts", parts)


def make_launched_freefall():
    """Drop tower: tall column, top cap, ring-shaped drop car."""
    base = cylinder("base", 10, 1.1, 0.3, (0, 0, 0.15))
    paint(base, METAL)
    column = cylinder("column", 8, 0.45, 8.0, (0, 0, 4.3))
    paint(column, TEAL)
    stripe = cylinder("stripe", 8, 0.48, 1.2, (0, 0, 6.5))
    paint(stripe, YELLOW)
    cap = cone("cap", 8, 0.9, 0.9, (0, 0, 8.9))
    paint(cap, RED)
    tip = ico("tip", 0, 0.22, (0, 0, 9.5))
    paint(tip, YELLOW)
    ring = torus("ring", 12, 6, 0.85, 0.18, (0, 0, 2.2))
    paint(ring, ORANGE)
    car = box("car", 1.0, (0, 0, 2.65))
    car.scale = (1.9, 1.9, 0.5)
    bpy.context.view_layer.update()
    paint(car, BLUE)
    parts = [(base, 0.01), (column, 0.01), (stripe, 0.008), (cap, 0.01),
             (tip, 0.008), (ring, 0.008), (car, 0.01)]
    build_prop("LaunchedFreefall", parts)


def make_haunted_house():
    """Crooked dark house: tilted body, crooked roof, door, windows, bats."""
    body = box("body", 1.0, (0, 0, 1.4))
    body.scale = (3.2, 2.6, 2.8)
    body.rotation_euler = (0, 0, math.radians(4))
    bpy.context.view_layer.update()
    paint(body, (0.35, 0.32, 0.38, 1.0))
    roof = cone("roof", 4, 2.9, 1.8, (0.1, 0, 3.6))
    roof.rotation_euler = (0, 0, math.radians(45 + 6))
    bpy.context.view_layer.update()
    paint(roof, (0.18, 0.16, 0.20, 1.0))
    chimney = box("chimney", 0.5, (1.0, 0.5, 4.0))
    chimney.scale = (1, 1, 2.4)
    chimney.rotation_euler = (0, 0, math.radians(-8))
    bpy.context.view_layer.update()
    paint(chimney, (0.30, 0.28, 0.32, 1.0))
    door = box("door", 1.0, (0, 1.33, 0.9))
    door.scale = (0.8, 0.08, 1.6)
    bpy.context.view_layer.update()
    paint(door, (0.12, 0.10, 0.12, 1.0))
    parts = [(body, 0.02), (roof, 0.015), (chimney, 0.01), (door, 0.006)]
    for i, wx in enumerate((-1.0, 1.0)):
        win = box("win%d" % i, 1.0, (wx, 1.32, 2.2))
        win.scale = (0.6, 0.08, 0.7)
        bpy.context.view_layer.update()
        paint(win, (0.95, 0.85, 0.40, 1.0))  # eerie lit windows
        parts.append((win, 0.006))
    for i, (bx, by, bz) in enumerate([(-2.2, 0.6, 4.6), (2.4, -0.4, 5.2),
                                      (0.2, 1.8, 5.6)]):
        bat_body = box("batb%d" % i, 0.28, (bx, by, bz))
        paint(bat_body, DARK)
        wing_l = box("batwl%d" % i, 0.4, (bx - 0.32, by, bz + 0.05))
        wing_l.scale = (1.4, 0.5, 0.25)
        wing_l.rotation_euler = (0, 0, math.radians(25))
        bpy.context.view_layer.update()
        paint(wing_l, DARK)
        wing_r = box("batwr%d" % i, 0.4, (bx + 0.32, by, bz + 0.05))
        wing_r.scale = (1.4, 0.5, 0.25)
        wing_r.rotation_euler = (0, 0, math.radians(-25))
        bpy.context.view_layer.update()
        paint(wing_r, DARK)
        parts += [(bat_body, 0.01), (wing_l, 0.01), (wing_r, 0.01)]
    build_prop("HauntedHouse", parts)


def make_observation_tower():
    """Tall slim tower with a glass viewing cabin on top."""
    base = cylinder("base", 10, 1.4, 0.4, (0, 0, 0.2))
    paint(base, METAL)
    column = cylinder("column", 6, 0.35, 10.0, (0, 0, 5.4))
    paint(column, WHITE)
    parts = [(base, 0.01), (column, 0.01)]
    for i in range(4):
        th = i * math.pi / 2.0 + math.pi / 4.0
        brace = box("brace%d" % i, 0.18, (0.8 * math.cos(th), 0.8 * math.sin(th), 2.2))
        brace.scale = (1, 1, 14)
        brace.rotation_euler = (math.radians(18) * math.sin(th), math.radians(-18) * math.cos(th), 0)
        bpy.context.view_layer.update()
        paint(brace, RED)
        parts.append((brace, 0.008))
    ring = torus("ring", 10, 5, 0.55, 0.09, (0, 0, 10.3))
    paint(ring, RED)
    cabin = cylinder("cabin", 10, 1.3, 1.5, (0, 0, 11.3))
    paint(cabin, GLASS)
    cabin_floor = cylinder("cabinfl", 10, 1.35, 0.18, (0, 0, 10.6))
    paint(cabin_floor, METAL_D)
    roof = cone("roof", 10, 1.6, 0.8, (0, 0, 12.45))
    paint(roof, RED)
    antenna = cylinder("antenna", 6, 0.05, 1.2, (0, 0, 13.3))
    paint(antenna, METAL)
    parts += [(ring, 0.008), (cabin, 0.01), (cabin_floor, 0.008),
              (roof, 0.01), (antenna, 0.006)]
    build_prop("ObservationTower", parts)


# ---------------------------------------------------------------- stalls (Phase 2 set)

def make_fries_stall():
    """Red/white fries stall with a giant fries carton sign."""
    body = box("stallBody", 1.0, (0, 0, 0.9))
    body.scale = (2.6, 1.8, 1.8)
    bpy.context.view_layer.update()
    paint(body, RED)
    counter = box("counter", 1.0, (0, 1.05, 0.85))
    counter.scale = (2.7, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    for i in range(6):
        x = -1.09 + i * 0.44
        slat = box("awn%d" % i, 0.42, (x, 0.35, 2.15))
        slat.scale = (1, 3.4, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, RED if i % 2 == 0 else WHITE)
        parts.append((slat, 0.008))
    carton = box("carton", 0.55, (0, 0, 2.7))
    carton.scale = (1, 0.7, 1.1)
    bpy.context.view_layer.update()
    paint(carton, WHITE)
    parts.append((carton, 0.008))
    for i in range(5):
        fry = box("fry%d" % i, 0.12, (-0.18 + i * 0.09, 0, 3.15))
        fry.scale = (1, 1, 3.2)
        bpy.context.view_layer.update()
        paint(fry, YELLOW)
        parts.append((fry, 0.006))
    build_prop("FriesStall", parts)


def make_pizza_stall():
    """Orange/cream pizza stall with a big pizza-slice sign."""
    body = box("stallBody", 1.0, (0, 0, 0.9))
    body.scale = (2.6, 1.8, 1.8)
    bpy.context.view_layer.update()
    paint(body, ORANGE)
    counter = box("counter", 1.0, (0, 1.05, 0.85))
    counter.scale = (2.7, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    for i in range(6):
        x = -1.09 + i * 0.44
        slat = box("awn%d" % i, 0.42, (x, 0.35, 2.15))
        slat.scale = (1, 3.4, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, ORANGE if i % 2 == 0 else CREAM)
        parts.append((slat, 0.008))
    crust = cylinder("crust", 3, 0.85, 0.12, (0, 0, 2.75))
    crust.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.view_layer.update()
    paint(crust, (0.90, 0.70, 0.40, 1.0))
    cheese = cylinder("cheese", 3, 0.68, 0.13, (0, -0.02, 2.72))
    cheese.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.view_layer.update()
    paint(cheese, YELLOW)
    parts += [(crust, 0.008), (cheese, 0.008)]
    for i, (px, py) in enumerate([(-0.2, 2.9), (0.25, 2.75), (0.0, 2.55)]):
        pep = cylinder("pep%d" % i, 6, 0.09, 0.05, (px, py, 2.80))
        paint(pep, RED)
        parts.append((pep, 0.004))
    build_prop("PizzaStall", parts)


def make_ice_cream_stall():
    """Pink/white ice-cream stall with a cone-and-scoop sign."""
    body = box("stallBody", 1.0, (0, 0, 0.9))
    body.scale = (2.6, 1.8, 1.8)
    bpy.context.view_layer.update()
    paint(body, PINK_L)
    counter = box("counter", 1.0, (0, 1.05, 0.85))
    counter.scale = (2.7, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    for i in range(6):
        x = -1.09 + i * 0.44
        slat = box("awn%d" % i, 0.42, (x, 0.35, 2.15))
        slat.scale = (1, 3.4, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, PINK if i % 2 == 0 else WHITE)
        parts.append((slat, 0.008))
    cone_sign = cone("conesign", 8, 0.30, 0.7, (0, 0, 2.55))
    cone_sign.rotation_euler = (math.pi, 0, 0)
    bpy.context.view_layer.update()
    paint(cone_sign, (0.85, 0.65, 0.40, 1.0))
    scoop = ico("scoop", 1, 0.34, (0, 0, 3.05))
    paint(scoop, PINK)
    cherry = uv_sphere("cherry", 8, 5, 0.10, (0, 0, 3.38))
    paint(cherry, RED)
    parts += [(cone_sign, 0.008), (scoop, JITTER), (cherry, 0.006)]
    build_prop("IceCreamStall", parts)


def make_popcorn_stall():
    """Red/yellow popcorn stall with a bucket of popcorn puffs."""
    body = box("stallBody", 1.0, (0, 0, 0.9))
    body.scale = (2.6, 1.8, 1.8)
    bpy.context.view_layer.update()
    paint(body, RED)
    counter = box("counter", 1.0, (0, 1.05, 0.85))
    counter.scale = (2.7, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    for i in range(6):
        x = -1.09 + i * 0.44
        slat = box("awn%d" % i, 0.42, (x, 0.35, 2.15))
        slat.scale = (1, 3.4, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, YELLOW if i % 2 == 0 else RED)
        parts.append((slat, 0.008))
    bucket = cylinder("bucket", 8, 0.42, 0.55, (0, 0, 2.6))
    paint(bucket, WHITE)
    band = cylinder("band", 8, 0.43, 0.14, (0, 0, 2.6))
    paint(band, RED)
    parts += [(bucket, 0.008), (band, 0.006)]
    for i, (px, py) in enumerate([(-0.18, 0.1), (0.15, -0.08), (0.0, 0.2),
                                 (-0.05, -0.18), (0.22, 0.12)]):
        puff = ico("puff%d" % i, 0, 0.16, (px, py, 3.0))
        paint(puff, CREAM)
        parts.append((puff, 0.01))
    build_prop("PopcornStall", parts)


def make_coffee_stall():
    """Brown/cream coffee stall with a big coffee-cup sign."""
    body = box("stallBody", 1.0, (0, 0, 0.9))
    body.scale = (2.6, 1.8, 1.8)
    bpy.context.view_layer.update()
    paint(body, BROWN)
    counter = box("counter", 1.0, (0, 1.05, 0.85))
    counter.scale = (2.7, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    for i in range(6):
        x = -1.09 + i * 0.44
        slat = box("awn%d" % i, 0.42, (x, 0.35, 2.15))
        slat.scale = (1, 3.4, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, BROWN if i % 2 == 0 else CREAM)
        parts.append((slat, 0.008))
    cup = cylinder("cup", 10, 0.35, 0.55, (0, 0, 2.65))
    paint(cup, WHITE)
    coffee = cylinder("coffee", 10, 0.30, 0.06, (0, 0, 2.90))
    paint(coffee, (0.25, 0.15, 0.08, 1.0))
    handle = torus("handle", 8, 4, 0.16, 0.05, (0.38, 0, 2.65))
    handle.rotation_euler = (0, math.radians(90), 0)
    bpy.context.view_layer.update()
    paint(handle, WHITE)
    parts += [(cup, 0.008), (coffee, 0.004), (handle, 0.006)]
    build_prop("CoffeeStall", parts)


def make_balloon_stall():
    """Teal balloon stall: cart + bunch of colorful balloons on strings."""
    cart = box("cart", 1.0, (0, 0, 0.7))
    cart.scale = (2.2, 1.4, 1.4)
    bpy.context.view_layer.update()
    paint(cart, TEAL)
    counter = box("counter", 1.0, (0, 0.85, 0.85))
    counter.scale = (2.3, 0.3, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    weight = box("weight", 0.4, (0.7, 0, 1.55))
    paint(weight, METAL_D)
    parts = [(cart, 0.012), (counter, 0.008), (weight, 0.006)]
    balloon_cols = [RED, YELLOW, BLUE, GREEN, PINK]
    for i in range(5):
        th = i * math.pi * 2.0 / 5.0
        sx = 0.7 + 0.35 * math.cos(th)
        sy = 0.35 * math.sin(th)
        bz = 3.1 + 0.15 * math.sin(i * 2.3)
        string = cylinder("string%d" % i, 5, 0.015, bz - 1.75, (sx, sy, (bz + 1.75) / 2))
        paint(string, WHITE)
        balloon = ico("balloon%d" % i, 1, 0.32, (sx, sy, bz))
        balloon.scale = (1, 1, 1.25)
        bpy.context.view_layer.update()
        paint(balloon, balloon_cols[i])
        knot = cone("knot%d" % i, 5, 0.07, 0.12, (sx, sy, bz - 0.42))
        paint(knot, balloon_cols[i])
        parts += [(string, 0.004), (balloon, 0.015), (knot, 0.006)]
    build_prop("BalloonStall", parts)


def make_souvenir_stall():
    """Purple/yellow souvenir stall with a gift-box sign."""
    body = box("stallBody", 1.0, (0, 0, 0.9))
    body.scale = (2.6, 1.8, 1.8)
    bpy.context.view_layer.update()
    paint(body, PURPLE)
    counter = box("counter", 1.0, (0, 1.05, 0.85))
    counter.scale = (2.7, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    for i in range(6):
        x = -1.09 + i * 0.44
        slat = box("awn%d" % i, 0.42, (x, 0.35, 2.15))
        slat.scale = (1, 3.4, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, PURPLE if i % 2 == 0 else YELLOW)
        parts.append((slat, 0.008))
    gift = box("gift", 0.6, (0, 0, 2.6))
    paint(gift, YELLOW)
    ribbon_x = box("ribbonX", 0.62, (0, 0, 2.6))
    ribbon_x.scale = (0.25, 1.05, 1.05)
    bpy.context.view_layer.update()
    paint(ribbon_x, RED)
    ribbon_y = box("ribbonY", 0.62, (0, 0, 2.6))
    ribbon_y.scale = (1.05, 0.25, 1.05)
    bpy.context.view_layer.update()
    paint(ribbon_y, RED)
    bow = ico("bow", 0, 0.16, (0, 0, 3.0))
    paint(bow, RED)
    parts += [(gift, 0.008), (ribbon_x, 0.006), (ribbon_y, 0.006), (bow, 0.01)]
    build_prop("SouvenirStall", parts)


def make_bathroom():
    """Small restroom building: blue/white body, door, WC sign board."""
    body = box("body", 1.0, (0, 0, 1.1))
    body.scale = (2.4, 2.0, 2.2)
    bpy.context.view_layer.update()
    paint(body, (0.75, 0.82, 0.90, 1.0))
    roof = box("roof", 1.0, (0, 0, 2.3))
    roof.scale = (2.7, 2.3, 0.18)
    bpy.context.view_layer.update()
    paint(roof, BLUE)
    door = box("door", 1.0, (0, 1.02, 0.85))
    door.scale = (0.8, 0.06, 1.7)
    bpy.context.view_layer.update()
    paint(door, WHITE)
    handle = uv_sphere("handle", 6, 4, 0.06, (0.28, 1.06, 0.85))
    paint(handle, METAL_D)
    signboard = box("signboard", 1.0, (0, 0, 2.75))
    signboard.scale = (1.4, 0.14, 0.45)
    bpy.context.view_layer.update()
    paint(signboard, WHITE)
    # WC letters as blocky sign trim (no font ops headless): blue bars on the board
    bar_w = box("barW", 1.0, (-0.35, 0, 2.75))
    bar_w.scale = (0.28, 0.16, 0.28)
    bpy.context.view_layer.update()
    paint(bar_w, BLUE)
    bar_c = box("barC", 1.0, (0.35, 0, 2.75))
    bar_c.scale = (0.28, 0.16, 0.28)
    bpy.context.view_layer.update()
    paint(bar_c, BLUE)
    parts = [(body, 0.015), (roof, 0.008), (door, 0.006), (handle, 0.004),
             (signboard, 0.008), (bar_w, 0.005), (bar_c, 0.005)]
    build_prop("Bathroom", parts)


# ---------------------------------------------------------------- staff figures

def make_security_figure():
    """Security guard peep: navy uniform, cap, gold badge."""
    leg_l = cylinder("legL", 6, 0.07, 0.28, (-0.09, 0, 0.14))
    paint(leg_l, DARK)
    leg_r = cylinder("legR", 6, 0.07, 0.28, (0.09, 0, 0.14))
    paint(leg_r, DARK)
    body = cylinder("body", 10, 0.20, 0.55, (0, 0, 0.55))
    paint(body, NAVY)
    arm_l = cylinder("armL", 6, 0.06, 0.38, (-0.27, 0, 0.55))
    arm_l.rotation_euler = (0, 0, math.radians(10))
    bpy.context.view_layer.update()
    paint(arm_l, NAVY)
    arm_r = cylinder("armR", 6, 0.06, 0.38, (0.27, 0, 0.55))
    arm_r.rotation_euler = (0, 0, math.radians(-10))
    bpy.context.view_layer.update()
    paint(arm_r, NAVY)
    head = uv_sphere("head", 10, 6, 0.17, (0, 0, 1.02))
    paint(head, SKIN)
    cap_top = cylinder("capTop", 8, 0.185, 0.09, (0, 0, 1.16))
    paint(cap_top, NAVY)
    brim = box("brim", 0.3, (0, 0.24, 1.13))
    brim.scale = (1.4, 1.2, 0.35)
    bpy.context.view_layer.update()
    paint(brim, NAVY)
    badge = box("badge", 0.12, (-0.12, 0.19, 0.68))
    badge.scale = (1, 0.4, 1)
    bpy.context.view_layer.update()
    paint(badge, GOLD)
    parts = [(leg_l, 0.006), (leg_r, 0.006), (body, 0.01),
             (arm_l, 0.006), (arm_r, 0.006), (head, 0.012),
             (cap_top, 0.006), (brim, 0.006), (badge, 0.004)]
    build_prop("SecurityFigure", parts)


def make_entertainer_figure():
    """Panda-costume entertainer peep: white body, black ears and eye patches."""
    leg_l = cylinder("legL", 6, 0.08, 0.30, (-0.10, 0, 0.15))
    paint(leg_l, DARK)
    leg_r = cylinder("legR", 6, 0.08, 0.30, (0.10, 0, 0.15))
    paint(leg_r, DARK)
    body = cylinder("body", 10, 0.24, 0.60, (0, 0, 0.60))
    paint(body, WHITE)
    belly = uv_sphere("belly", 8, 5, 0.17, (0, 0.10, 0.58))
    belly.scale = (1.1, 0.7, 1.2)
    bpy.context.view_layer.update()
    paint(belly, WHITE)
    arm_l = cylinder("armL", 6, 0.07, 0.40, (-0.30, 0, 0.60))
    arm_l.rotation_euler = (0, 0, math.radians(12))
    bpy.context.view_layer.update()
    paint(arm_l, DARK)
    arm_r = cylinder("armR", 6, 0.07, 0.40, (0.30, 0, 0.60))
    arm_r.rotation_euler = (0, 0, math.radians(-12))
    bpy.context.view_layer.update()
    paint(arm_r, DARK)
    head = uv_sphere("head", 10, 6, 0.20, (0, 0, 1.08))
    paint(head, WHITE)
    ear_l = uv_sphere("earL", 8, 5, 0.08, (-0.13, 0, 1.24))
    paint(ear_l, DARK)
    ear_r = uv_sphere("earR", 8, 5, 0.08, (0.13, 0, 1.24))
    paint(ear_r, DARK)
    patch_l = uv_sphere("patchL", 6, 4, 0.055, (-0.07, 0.16, 1.10))
    patch_l.scale = (1, 0.5, 1.2)
    bpy.context.view_layer.update()
    paint(patch_l, DARK)
    patch_r = uv_sphere("patchR", 6, 4, 0.055, (0.07, 0.16, 1.10))
    patch_r.scale = (1, 0.5, 1.2)
    bpy.context.view_layer.update()
    paint(patch_r, DARK)
    nose = uv_sphere("nose", 6, 4, 0.045, (0, 0.19, 1.02))
    paint(nose, DARK)
    parts = [(leg_l, 0.006), (leg_r, 0.006), (body, 0.01), (belly, 0.01),
             (arm_l, 0.006), (arm_r, 0.006), (head, 0.012),
             (ear_l, 0.008), (ear_r, 0.008), (patch_l, 0.005),
             (patch_r, 0.005), (nose, 0.004)]
    build_prop("EntertainerFigure", parts)


# ---------------------------------------------------------------- main

clear_scene()
make_twist()
make_top_spin()
make_go_karts()
make_launched_freefall()
make_haunted_house()
make_observation_tower()
make_fries_stall()
make_pizza_stall()
make_ice_cream_stall()
make_popcorn_stall()
make_coffee_stall()
make_balloon_stall()
make_souvenir_stall()
make_bathroom()
make_security_figure()
make_entertainer_figure()

print("DONE ->", OUT_DIR)
print("--- tri summary (must all be < 2000) ---")
for n, t in TRI_LOG:
    print("  %-16s %4d tris %s" % (n, t, "OK" if t < 2000 else "OVER BUDGET"))
