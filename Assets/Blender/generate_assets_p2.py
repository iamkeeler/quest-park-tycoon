# Quest Park Tycoon — procedural clay/diorama prop generator, PART 2
#
# Run headless:
#   blender --background --python Assets/Blender/generate_assets_p2.py
#
# Produces FBX files in Assets/_Project/Models/:
#   coastercar.fbx, coasterstation.fbx, tracksupport.fbx, ferriswheel.fbx,
#   merrygoround.fbx, swigingship.fbx, drinksstall.fbx,
#   cottoncandystall.fbx, infokiosk.fbx, pathtile.fbx, queuetile.fbx,
#   guestfigure.fbx, stafffigure.fbx, entrancegate.fbx
#
# Same art direction + conventions as generate_assets.py (part 1):
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

PASTELS = [
    (0.95, 0.65, 0.70, 1.0),  # pastel pink
    (0.55, 0.75, 0.92, 1.0),  # pastel blue
    (0.98, 0.88, 0.55, 1.0),  # pastel yellow
    (0.60, 0.85, 0.65, 1.0),  # pastel green
    (0.75, 0.65, 0.90, 1.0),  # pastel purple
]


# ---------------------------------------------------------------- props (Phase 1 set)

def make_coaster_car():
    """Chunky 4-seat open coaster car, bright red/yellow."""
    chassis = box("chassis", 0.3, (0, 0, 0.30))
    chassis.scale = (4.2, 2.0, 0.9)
    bpy.context.view_layer.update()
    paint(chassis, METAL)
    tub = box("tub", 1.0, (0, 0, 0.75))
    tub.scale = (2.2, 1.4, 0.9)
    bpy.context.view_layer.update()
    paint(tub, RED)
    rim = box("rim", 1.0, (0, 0, 1.16))
    rim.scale = (2.3, 1.5, 0.18)
    bpy.context.view_layer.update()
    paint(rim, YELLOW)
    parts = [(chassis, 0.008), (tub, 0.01), (rim, 0.008)]
    for i, sx in enumerate((-0.5, 0.5)):
        for j, sy in enumerate((-0.32, 0.32)):
            seat = box("seat%d%d" % (i, j), 0.42, (sx, sy, 0.72))
            paint(seat, YELLOW)
            back = box("back%d%d" % (i, j), 0.42, (sx - 0.30, sy, 0.95))
            back.scale = (0.35, 1.0, 1.8)
            bpy.context.view_layer.update()
            paint(back, RED)
            parts += [(seat, 0.006), (back, 0.006)]
    for i, wx in enumerate((-0.75, 0.75)):
        for j, wy in enumerate((-0.55, 0.55)):
            wheel = cylinder("wheel%d%d" % (i, j), 8, 0.13, 0.10, (wx, wy, 0.13))
            wheel.rotation_euler = (math.radians(90), 0, 0)
            bpy.context.view_layer.update()
            paint(wheel, DARK)
            parts.append((wheel, 0.004))
    nose = box("nose", 0.8, (1.30, 0, 0.80))
    nose.rotation_euler = (0, math.radians(-18), 0)
    bpy.context.view_layer.update()
    paint(nose, RED)
    parts.append((nose, 0.008))
    build_prop("CoasterCar", parts)


def make_coaster_station():
    """Boarding platform + posts + striped awning roof + sign."""
    platform = box("platform", 1.0, (0, 0, 0.15))
    platform.scale = (7.0, 3.4, 0.3)
    bpy.context.view_layer.update()
    paint(platform, WOOD)
    edge = box("edge", 1.0, (0, 0, 0.33))
    edge.scale = (7.1, 3.5, 0.06)
    bpy.context.view_layer.update()
    paint(edge, YELLOW)
    parts = [(platform, 0.01), (edge, 0.006)]
    for i, px in enumerate((-3.0, 3.0)):
        for j, py in enumerate((-1.4, 1.4)):
            post = cylinder("post%d%d" % (i, j), 6, 0.12, 3.2, (px, py, 1.9))
            paint(post, WOOD_D)
            parts.append((post, 0.008))
    for i in range(10):
        x = -2.7 + i * 0.6
        slat = box("awn%d" % i, 0.55, (x, 0, 3.70))
        slat.scale = (1, 7.5, 0.3)
        slat.rotation_euler = (math.radians(10), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, RED if i % 2 == 0 else WHITE)
        parts.append((slat, 0.008))
    sign = box("sign", 1.0, (0, 0, 4.35))
    sign.scale = (2.4, 0.2, 0.6)
    bpy.context.view_layer.update()
    paint(sign, RED)
    parts.append((sign, 0.008))
    build_prop("CoasterStation", parts)


def make_track_support():
    """Steel support column with cross arms + diagonal braces."""
    base = cylinder("base", 8, 0.5, 0.18, (0, 0, 0.09))
    paint(base, METAL)
    column = cylinder("column", 8, 0.16, 4.2, (0, 0, 2.2))
    paint(column, METAL)
    arm_x = box("armX", 1.0, (0, 0, 4.15))
    arm_x.scale = (2.2, 0.35, 0.35)
    bpy.context.view_layer.update()
    paint(arm_x, METAL_D)
    arm_y = box("armY", 1.0, (0, 0, 4.15))
    arm_y.scale = (0.35, 2.2, 0.35)
    bpy.context.view_layer.update()
    paint(arm_y, METAL_D)
    parts = [(base, 0.008), (column, 0.008), (arm_x, 0.006), (arm_y, 0.006)]
    for i, (bx, rot) in enumerate([(-0.7, 40), (0.7, -40)]):
        brace = box("braceX%d" % i, 0.12, (bx, 0, 3.55))
        brace.scale = (1, 1, 8)
        brace.rotation_euler = (0, math.radians(rot), 0)
        bpy.context.view_layer.update()
        paint(brace, METAL_D)
        parts.append((brace, 0.006))
    for i, (by, rot) in enumerate([(-0.7, -40), (0.7, 40)]):
        brace = box("braceY%d" % i, 0.12, (0, by, 3.55))
        brace.scale = (1, 1, 8)
        brace.rotation_euler = (math.radians(rot), 0, 0)
        bpy.context.view_layer.update()
        paint(brace, METAL_D)
        parts.append((brace, 0.006))
    saddle = box("saddle", 1.0, (0, 0, 4.45))
    saddle.scale = (0.8, 0.8, 0.3)
    bpy.context.view_layer.update()
    paint(saddle, METAL)
    parts.append((saddle, 0.006))
    build_prop("TrackSupport", parts)


def make_ferris_wheel():
    """Pastel ferris wheel: rim + 8 spokes + 8 gondolas + A-frame legs."""
    H = 4.2
    R = 3.2
    rim = torus("rim", 16, 6, R, 0.12, (0, 0, H))
    rim.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.view_layer.update()
    paint(rim, RED)
    parts = [(rim, 0.008)]
    for i in range(8):
        th = i * math.pi / 4.0
        spoke = cylinder("spoke%d" % i, 6, 0.05, R, (R / 2 * math.cos(th), 0,
                                                    H + R / 2 * math.sin(th)))
        spoke.rotation_euler = (0, math.pi / 2 - th, 0)
        bpy.context.view_layer.update()
        paint(spoke, WHITE)
        parts.append((spoke, 0.006))
    hub = cylinder("hub", 8, 0.3, 0.6, (0, 0, H))
    hub.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.view_layer.update()
    paint(hub, METAL)
    parts.append((hub, 0.006))
    axle = cylinder("axle", 8, 0.12, 2.4, (0, 0, H))
    axle.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.view_layer.update()
    paint(axle, METAL)
    parts.append((axle, 0.006))
    for i in range(8):
        th = i * math.pi / 4.0
        gx = R * math.cos(th)
        gz = H + R * math.sin(th) - 0.45
        cab = box("cab%d" % i, 0.7, (gx, 0, gz))
        paint(cab, PASTELS[i % len(PASTELS)])
        roof = box("cabroof%d" % i, 0.75, (gx, 0, gz + 0.42))
        roof.scale = (1, 1, 0.3)
        bpy.context.view_layer.update()
        paint(roof, WHITE)
        parts += [(cab, 0.01), (roof, 0.008)]
    for i, sx in enumerate((-1, 1)):
        for j, sy in enumerate((-1, 1)):
            leg = box("leg%d%d" % (i, j), 0.35, (-sx * 0.66, sy * 1.0, 2.15))
            leg.scale = (1, 1, 13)
            leg.rotation_euler = (0, math.radians(sx * 17), 0)
            bpy.context.view_layer.update()
            paint(leg, METAL)
            parts.append((leg, 0.008))
    build_prop("FerrisWheel", parts)


def make_merry_go_round():
    """Carousel: base + center column + cone roof + 4 blob horses on poles."""
    base = cylinder("base", 12, 2.3, 0.3, (0, 0, 0.15))
    paint(base, WHITE)
    trim = cylinder("trim", 12, 2.36, 0.12, (0, 0, 0.32))
    paint(trim, RED)
    column = cylinder("column", 8, 0.35, 2.8, (0, 0, 1.7))
    paint(column, YELLOW)
    roof = cone("roof", 12, 3.0, 1.5, (0, 0, 3.9))
    paint(roof, RED)
    tip = ico("tip", 0, 0.2, (0, 0, 4.75))
    paint(tip, YELLOW)
    parts = [(base, 0.012), (trim, 0.008), (column, 0.008),
             (roof, 0.01), (tip, 0.008)]
    for i in range(4):
        th = i * math.pi / 2.0
        cx, cy = 1.5 * math.cos(th), 1.5 * math.sin(th)
        pole = cylinder("hpole%d" % i, 6, 0.06, 2.6, (cx, cy, 1.6))
        paint(pole, METAL)
        body = ico("hbody%d" % i, 1, 0.45, (cx, cy, 1.5))
        body.scale = (1.7, 0.9, 1.0)
        bpy.context.view_layer.update()
        paint(body, PASTELS[i % len(PASTELS)])
        hx = cx + 0.62 * math.cos(th)
        hy = cy + 0.62 * math.sin(th)
        head = box("hhead%d" % i, 0.4, (hx, hy, 1.85))
        head.rotation_euler = (0, 0, th - math.radians(25))
        bpy.context.view_layer.update()
        paint(head, PASTELS[i % len(PASTELS)])
        parts += [(pole, 0.006), (body, JITTER), (head, 0.01)]
    build_prop("MerryGoRound", parts)


def make_swinging_ship():
    """Pirate ship ride: hull + mast + A-frame supports."""
    hull = box("hull", 1.0, (0, 0, 1.0))
    hull.scale = (3.4, 1.3, 1.1)
    bpy.context.view_layer.update()
    paint(hull, BLUE)
    bow = box("bow", 0.8, (1.85, 0, 1.15))
    bow.rotation_euler = (0, math.radians(-20), 0)
    bpy.context.view_layer.update()
    paint(bow, BLUE)
    stripe = box("stripe", 1.0, (0, 0, 1.35))
    stripe.scale = (3.45, 1.35, 0.18)
    bpy.context.view_layer.update()
    paint(stripe, YELLOW)
    parts = [(hull, 0.012), (bow, 0.01), (stripe, 0.008)]
    for i, sx in enumerate((-1.0, 0.0, 1.0)):
        seat = box("seat%d" % i, 0.5, (sx, 0, 1.0))
        paint(seat, WOOD)
        parts.append((seat, 0.008))
    mast = cylinder("mast", 6, 0.08, 3.0, (0, 0, 2.6))
    paint(mast, WOOD_D)
    flag = box("flag", 0.5, (0.32, 0, 4.0))
    flag.scale = (1.2, 0.15, 0.7)
    bpy.context.view_layer.update()
    paint(flag, RED)
    parts += [(mast, 0.008), (flag, 0.008)]
    for i, sy in enumerate((-1, 1)):
        for j, sgn in enumerate((-1, 1)):
            leg = box("aframe%d%d" % (i, j), 0.25, (0, sy * 0.85, 1.5))
            leg.scale = (1, 1, 12)
            leg.rotation_euler = (math.radians(sgn * 22), 0, 0)
            bpy.context.view_layer.update()
            paint(leg, WOOD_D)
            parts.append((leg, 0.008))
    pivot = cylinder("pivot", 8, 0.1, 2.6, (0, 0, 2.6))
    pivot.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.view_layer.update()
    paint(pivot, METAL)
    parts.append((pivot, 0.006))
    build_prop("SwingingShip", parts)


def make_drinks_stall():
    """Wide blue drinks stall with cup-and-straw sign."""
    body = box("stallBody", 1.0, (0, 0, 0.9))
    body.scale = (3.0, 1.8, 1.8)
    bpy.context.view_layer.update()
    paint(body, BLUE)
    counter = box("counter", 1.0, (0, 1.05, 0.85))
    counter.scale = (3.1, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    for i in range(7):
        x = -1.26 + i * 0.42
        slat = box("awn%d" % i, 0.4, (x, 0.35, 2.15))
        slat.scale = (1, 3.6, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, BLUE if i % 2 == 0 else WHITE)
        parts.append((slat, 0.008))
    cup = cylinder("cup", 8, 0.35, 0.55, (0, 0, 2.75))
    paint(cup, WHITE)
    straw = cylinder("straw", 6, 0.05, 0.7, (0.12, 0, 3.05))
    straw.rotation_euler = (0, 0, math.radians(15))
    bpy.context.view_layer.update()
    paint(straw, RED)
    parts += [(cup, 0.01), (straw, 0.006)]
    build_prop("DrinksStall", parts)


def make_cotton_candy_stall():
    """Pink stall with a giant cotton-candy puff on a stick."""
    body = box("stallBody", 1.0, (0, 0, 0.9))
    body.scale = (2.4, 1.8, 1.8)
    bpy.context.view_layer.update()
    paint(body, PINK)
    counter = box("counter", 1.0, (0, 1.05, 0.85))
    counter.scale = (2.5, 0.35, 0.12)
    bpy.context.view_layer.update()
    paint(counter, WOOD)
    parts = [(body, 0.012), (counter, 0.008)]
    for i in range(6):
        x = -1.05 + i * 0.42
        slat = box("awn%d" % i, 0.4, (x, 0.35, 2.15))
        slat.scale = (1, 3.2, 0.35)
        slat.rotation_euler = (math.radians(18), 0, 0)
        bpy.context.view_layer.update()
        paint(slat, PINK if i % 2 == 0 else WHITE)
        parts.append((slat, 0.008))
    stick = cylinder("stick", 6, 0.06, 1.1, (0, 0, 2.6))
    paint(stick, WOOD_D)
    puff = ico("puff", 1, 0.55, (0, 0, 3.35))
    paint(puff, PINK_L)
    parts += [(stick, 0.006), (puff, JITTER)]
    build_prop("CottonCandyStall", parts)


def make_info_kiosk():
    """Tall narrow teal kiosk with an 'i' sign board."""
    body = box("kioskBody", 1.0, (0, 0, 1.3))
    body.scale = (1.6, 1.6, 2.6)
    bpy.context.view_layer.update()
    paint(body, TEAL)
    roof = box("roof", 1.0, (0, 0, 2.75))
    roof.scale = (2.0, 2.0, 0.18)
    bpy.context.view_layer.update()
    paint(roof, WOOD_D)
    window = box("window", 1.0, (0, 0.82, 1.6))
    window.scale = (1.2, 0.06, 0.8)
    bpy.context.view_layer.update()
    paint(window, CREAM)
    pole = cylinder("signpole", 6, 0.07, 1.2, (0, 0, 3.4))
    paint(pole, METAL)
    board = box("board", 1.0, (0, 0, 4.05))
    board.scale = (0.9, 0.16, 0.7)
    bpy.context.view_layer.update()
    paint(board, YELLOW)
    dot = uv_sphere("dot", 8, 5, 0.16, (0, 0, 4.62))
    paint(dot, YELLOW)
    parts = [(body, 0.012), (roof, 0.008), (window, 0.006), (pole, 0.006),
             (board, 0.008), (dot, 0.008)]
    build_prop("InfoKiosk", parts)


def make_path_tile():
    """1x1m path tile with inset center."""
    tile = box("tile", 1.0, (0, 0, 0.05))
    tile.scale = (1, 1, 0.1)
    bpy.context.view_layer.update()
    paint(tile, PATH)
    inset = box("inset", 1.0, (0, 0, 0.105))
    inset.scale = (0.8, 0.8, 0.02)
    bpy.context.view_layer.update()
    paint(inset, PATH_D)
    build_prop("PathTile", [(tile, 0.004), (inset, 0.004)])


def make_queue_tile():
    """1x1m queue tile with metal railings on three sides."""
    tile = box("tile", 1.0, (0, 0, 0.05))
    tile.scale = (1, 1, 0.1)
    bpy.context.view_layer.update()
    paint(tile, PATH_D)
    parts = [(tile, 0.004)]
    for side, (px, pz_rot) in enumerate([(-0.45, 0), (0.45, 0)]):
        for i, py in enumerate((-0.33, 0, 0.33)):
            post = cylinder("post%d%d" % (side, i), 6, 0.05, 0.9, (px, py, 0.55))
            paint(post, METAL_D)
            parts.append((post, 0.004))
        for rz in (0.35, 0.75):
            rail = box("rail%d%d" % (side, int(rz * 100)), 1.0, (px, 0, rz))
            rail.scale = (0.08, 1.0, 0.08)
            bpy.context.view_layer.update()
            paint(rail, METAL_D)
            parts.append((rail, 0.004))
    back_rail_top = box("backrail", 1.0, (0, -0.45, 0.75))
    back_rail_top.scale = (1.0, 0.08, 0.08)
    bpy.context.view_layer.update()
    paint(back_rail_top, METAL_D)
    back_rail_mid = box("backrail2", 1.0, (0, -0.45, 0.35))
    back_rail_mid.scale = (1.0, 0.08, 0.08)
    bpy.context.view_layer.update()
    paint(back_rail_mid, METAL_D)
    parts += [(back_rail_top, 0.004), (back_rail_mid, 0.004)]
    build_prop("QueueTile", parts)


def make_peep(name, shirt, cap):
    """Low-poly peep: stubby legs, capsule body, round head. Staff gets a cap."""
    leg_l = cylinder("legL", 6, 0.07, 0.28, (-0.09, 0, 0.14))
    paint(leg_l, NAVY)
    leg_r = cylinder("legR", 6, 0.07, 0.28, (0.09, 0, 0.14))
    paint(leg_r, NAVY)
    body = cylinder("body", 10, 0.20, 0.55, (0, 0, 0.55))
    paint(body, shirt)
    arm_l = cylinder("armL", 6, 0.06, 0.38, (-0.27, 0, 0.55))
    arm_l.rotation_euler = (0, 0, math.radians(10))
    bpy.context.view_layer.update()
    paint(arm_l, shirt)
    arm_r = cylinder("armR", 6, 0.06, 0.38, (0.27, 0, 0.55))
    arm_r.rotation_euler = (0, 0, math.radians(-10))
    bpy.context.view_layer.update()
    paint(arm_r, shirt)
    head = uv_sphere("head", 10, 6, 0.17, (0, 0, 1.02))
    paint(head, SKIN)
    parts = [(leg_l, 0.006), (leg_r, 0.006), (body, 0.01),
             (arm_l, 0.006), (arm_r, 0.006), (head, 0.012)]
    if cap:
        cap_top = cylinder("capTop", 8, 0.185, 0.09, (0, 0, 1.16))
        paint(cap_top, RED)
        brim = box("brim", 0.3, (0, 0.24, 1.13))
        brim.scale = (1.4, 1.2, 0.35)
        bpy.context.view_layer.update()
        paint(brim, RED)
        parts += [(cap_top, 0.006), (brim, 0.006)]
    build_prop(name, parts)


def make_entrance_gate():
    """Park entrance: two pillars + arch beam + sign board + ball caps."""
    parts = []
    for i, px in enumerate((-1.8, 1.8)):
        pillar = box("pillar%d" % i, 0.6, (px, 0, 1.5))
        pillar.scale = (1, 1, 5)
        bpy.context.view_layer.update()
        paint(pillar, CREAM)
        ball = ico("ball%d" % i, 0, 0.4, (px, 0, 3.35))
        paint(ball, RED)
        parts += [(pillar, 0.01), (ball, 0.012)]
    beam = box("beam", 1.0, (0, 0, 3.1))
    beam.scale = (4.6, 0.5, 0.5)
    bpy.context.view_layer.update()
    paint(beam, WOOD_D)
    sign = box("sign", 1.0, (0, 0, 3.75))
    sign.scale = (3.4, 0.18, 0.7)
    bpy.context.view_layer.update()
    paint(sign, YELLOW)
    parts += [(beam, 0.01), (sign, 0.008)]
    build_prop("EntranceGate", parts)


# ---------------------------------------------------------------- main

clear_scene()
make_coaster_car()
make_coaster_station()
make_track_support()
make_ferris_wheel()
make_merry_go_round()
make_swinging_ship()
make_drinks_stall()
make_cotton_candy_stall()
make_info_kiosk()
make_path_tile()
make_queue_tile()
make_peep("GuestFigure", TEAL, cap=False)
make_peep("StaffFigure", BLUE, cap=True)
make_entrance_gate()

print("DONE ->", OUT_DIR)
print("--- tri summary (must all be < 2000) ---")
for n, t in TRI_LOG:
    print("  %-16s %4d tris %s" % (n, t, "OK" if t < 2000 else "OVER BUDGET"))
