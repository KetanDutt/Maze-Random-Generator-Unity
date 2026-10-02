"""Render the diagrams that are embedded in ``README.md`` and ``Docs/``.

Everything is drawn from ``maze_reference.py`` (the same algorithms the Unity scripts
use), so the images always match the shipped generator.  The isometric picture uses the
exact same faces + winding as ``MazeMeshBuilder.BuildWallMesh`` and therefore doubles as a
preview of what the mesh looks like inside Unity.

Usage::

    python3 Tools/Reference/generate_docs_images.py

Requires ``pillow`` (``pip install pillow``).  Output: ``Docs/images/*.png``.
"""

from __future__ import annotations

import math
import os
from typing import Dict, Iterable, List, Sequence, Tuple

from PIL import Image, ImageDraw, ImageFont

from benchmark_reference import mesh_stats  # noqa: E402
from maze_reference import (  # noqa: E402
    ALGORITHMS,
    DIRECTION_OFFSETS,
    EAST,
    NORTH,
    MazeGrid,
    count_dead_ends,
    find_shortest_path,
    generate_maze,
)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "Docs", "images")

FONT_DIR = "/usr/share/fonts/truetype/dejavu"
SUPERSAMPLE = 3

# Palette (kept in sync with the Unity demo scene: blue-grey walls, light floor).
BACKGROUND = (17, 22, 31)
PANEL_BG = (24, 31, 44)
FLOOR = (244, 246, 250)
WALL = (52, 64, 90)
WALL_TOP = (96, 112, 148)
WALL_RIGHT = (62, 76, 106)
WALL_LEFT = (43, 53, 76)
DEAD_END = (255, 138, 61)
PATH = (255, 61, 129)
OPENING = (23, 195, 214)
TEXT = (226, 232, 240)
TEXT_DIM = (148, 163, 184)

ALGORITHM_TITLES = {
    "prim": "Randomized Prim",
    "dfs": "Depth First (backtracker)",
    "kruskal": "Randomized Kruskal",
    "binary-tree": "Binary Tree",
}


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    name = "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"
    path = os.path.join(FONT_DIR, name)
    if os.path.exists(path):
        return ImageFont.truetype(path, size * SUPERSAMPLE)
    return ImageFont.load_default()


def draw_maze(draw: ImageDraw.ImageDraw, grid: MazeGrid, origin: Tuple[int, int], scale: int,
              wall_color=WALL, floor_color=FLOOR, dead_ends: Sequence[Tuple[int, int]] = (),
              path: Sequence[Tuple[int, int]] = (), openings: bool = False,
              grid_line: bool = False) -> None:
    ox, oy = origin
    s = scale * SUPERSAMPLE
    for y in range(grid.height):
        for x in range(grid.width):
            left = ox + x * s
            top = oy + (grid.height - 1 - y) * s
            if grid.is_wall(x, y):
                draw.rectangle([left, top, left + s - 1, top + s - 1], fill=wall_color)
            elif floor_color is not None:
                draw.rectangle([left, top, left + s - 1, top + s - 1], fill=floor_color)
            if grid_line and not grid.is_wall(x, y):
                draw.rectangle([left, top, left + s - 1, top + s - 1], outline=(226, 232, 240))

    for cell in dead_ends:
        x, y = cell
        left = ox + x * s + s * 0.25
        top = oy + (grid.height - 1 - y) * s + s * 0.25
        draw.ellipse([left, top, left + s * 0.5, top + s * 0.5], fill=DEAD_END)

    if openings:
        for opening in grid.openings:
            x, y = opening.border_cell
            left = ox + x * s
            top = oy + (grid.height - 1 - y) * s
            draw.rectangle([left, top, left + s - 1, top + s - 1], fill=OPENING)

    if path:
        points = []
        for cell in path:
            x, y = cell
            points.append(((ox + x * s + s / 2), (oy + (grid.height - 1 - y) * s + s / 2)))
        draw.line(points, fill=PATH, width=max(2, int(s * 0.28)), joint="curve")
        for point in (points[0], points[-1]):
            radius = s * 0.42
            draw.ellipse([point[0] - radius, point[1] - radius, point[0] + radius, point[1] + radius],
                         fill=PATH)


def introspect_dead_ends(grid: MazeGrid) -> List[Tuple[int, int]]:
    result = []
    for rx, ry in grid.rooms():
        connections = grid.room_degree(rx, ry) + (1 if grid.is_opening_room(rx, ry) else 0)
        if connections == 1:
            result.append(grid.cell_at_room(rx, ry))
    return result


def save(image: Image.Image, name: str) -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name)
    image.save(path, optimize=True)
    print("  wrote %-42s %6.1f KB  %dx%d" % (os.path.relpath(path, ROOT),
                                             os.path.getsize(path) / 1024.0,
                                             image.width, image.height))


# ---------------------------------------------------------------------------
# 1. Algorithm comparison
# ---------------------------------------------------------------------------


def render_algorithm_comparison() -> None:
    size = 31
    scale = 6
    columns = len(ALGORITHMS)
    panel_pixels = size * scale * SUPERSAMPLE
    margin = 18 * SUPERSAMPLE
    header = 34 * SUPERSAMPLE
    footer = 34 * SUPERSAMPLE

    width = margin * (columns + 1) + panel_pixels * columns
    height = header + panel_pixels + footer
    image = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(image)

    draw.text((margin, int(header * 0.35)), "Four algorithms, same seed (31 x 31, entrance + exit)",
              font=font(15, bold=True), fill=TEXT)

    for column, kind in enumerate(ALGORITHMS):
        grid = generate_maze(width=size, height=size, algorithm=kind, seed=20240517)
        x = margin + column * (panel_pixels + margin)
        y = header
        draw.rectangle([x - 2, y - 2, x + panel_pixels + 2, y + panel_pixels + 2], fill=PANEL_BG)
        draw_maze(draw, grid, (x, y), scale, openings=True)
        label = ALGORITHM_TITLES[kind]
        draw.text((x, y + panel_pixels + int(footer * 0.28)), label, font=font(12, bold=True), fill=TEXT)
        draw.text((x, y + panel_pixels + int(footer * 0.66)),
                  "dead ends %d" % count_dead_ends(grid), font=font(10), fill=TEXT_DIM)

    save(image.resize((width // SUPERSAMPLE, height // SUPERSAMPLE), Image.LANCZOS),
         "algorithm-comparison.png")


# ---------------------------------------------------------------------------
# 2. Braiding
# ---------------------------------------------------------------------------


def render_braid_comparison() -> None:
    size = 41
    scale = 5
    factors = (0.0, 0.5, 1.0)
    titles = ("Perfect maze - braid 0", "Loops - braid 0.5", "No dead ends - braid 1")
    panel_pixels = size * scale * SUPERSAMPLE
    margin = 18 * SUPERSAMPLE
    header = 34 * SUPERSAMPLE
    footer = 34 * SUPERSAMPLE

    width = margin * (len(factors) + 1) + panel_pixels * len(factors)
    height = header + panel_pixels + footer
    image = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(image)
    draw.text((margin, int(header * 0.35)),
              "Braiding removes dead ends (orange) by opening extra corridors",
              font=font(15, bold=True), fill=TEXT)

    for column, factor in enumerate(factors):
        grid = generate_maze(width=size, height=size, seed=7, braid_factor=factor)
        x = margin + column * (panel_pixels + margin)
        y = header
        draw.rectangle([x - 2, y - 2, x + panel_pixels + 2, y + panel_pixels + 2], fill=PANEL_BG)
        draw_maze(draw, grid, (x, y), scale, openings=True,
                  dead_ends=introspect_dead_ends(grid) if factor < 1 else ())
        draw.text((x, y + panel_pixels + int(footer * 0.28)), titles[column],
                  font=font(12, bold=True), fill=TEXT)
        draw.text((x, y + panel_pixels + int(footer * 0.66)),
                  "dead ends %d" % count_dead_ends(grid), font=font(10), fill=TEXT_DIM)

    save(image.resize((width // SUPERSAMPLE, height // SUPERSAMPLE), Image.LANCZOS),
         "braid-comparison.png")


# ---------------------------------------------------------------------------
# 3. Solution path
# ---------------------------------------------------------------------------


def render_solution_path() -> None:
    size = 31
    scale = 9
    grid = generate_maze(width=size, height=size, seed=4, opening_mode="fixed")
    path = find_shortest_path(grid, grid.openings[0].border_cell, grid.openings[-1].border_cell)

    panel = size * scale * SUPERSAMPLE
    margin = 20 * SUPERSAMPLE
    header = 36 * SUPERSAMPLE
    footer = 30 * SUPERSAMPLE
    width = panel + margin * 2
    height = header + panel + footer
    image = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(image)
    draw.text((margin, int(header * 0.4)), "Entrance to exit path (BFS shortest route)",
              font=font(15, bold=True), fill=TEXT)
    draw.rectangle([margin - 2, header - 2, margin + panel + 2, header + panel + 2], fill=PANEL_BG)
    draw_maze(draw, grid, (margin, header), scale, openings=True, path=path or ())
    draw.text((margin, header + panel + int(footer * 0.3)),
              "cyan = entrance / exit    magenta = shortest path (%d cells)" % (len(path or [])),
              font=font(11), fill=TEXT_DIM)

    save(image.resize((width // SUPERSAMPLE, height // SUPERSAMPLE), Image.LANCZOS),
         "solution-path.png")


# ---------------------------------------------------------------------------
# 4. Isometric preview of the generated mesh
# ---------------------------------------------------------------------------


def render_isometric() -> None:
    """Painter's-algorithm render of exactly the faces MazeMeshBuilder emits."""
    size = 21
    cell = 10 * SUPERSAMPLE
    wall_height = 5.0
    cos30, sin30 = math.cos(math.pi / 6), math.sin(math.pi / 6)
    grid = generate_maze(width=size, height=size, seed=2024, opening_mode="random", opening_count=2)
    path = find_shortest_path(grid, grid.openings[0].border_cell, grid.openings[-1].border_cell) or []

    def project(x: float, y: float, z: float) -> Tuple[float, float]:
        return ((x - z) * cos30, (x + z) * sin30 - y)

    # Bounds of the projection so the canvas can be sized exactly.
    corners = [project(x, 0, z) for x in (0, size * cell) for z in (0, size * cell)]
    corners += [project(x, wall_height * cell, z) for x in (0, size * cell) for z in (0, size * cell)]
    min_x = min(c[0] for c in corners)
    min_y = min(c[1] for c in corners)
    max_x = max(c[0] for c in corners)
    max_y = max(c[1] for c in corners)

    margin = 26 * SUPERSAMPLE
    title = "21 x 21 mesh preview - %d triangles in a single draw call" % (
        2 * mesh_stats(grid)["quads"])
    subtitle = ("hidden faces are culled: a cube per wall would need %d triangles and %d objects"
                % (mesh_stats(grid)["legacy_triangles"], mesh_stats(grid)["legacy_game_objects"]))
    probe = ImageDraw.Draw(Image.new("RGB", (8, 8)))
    text_width = max(probe.textlength(title, font=font(14, bold=True)),
                     probe.textlength(subtitle, font=font(10)),
                     probe.textlength("magenta = shortest entrance/exit route (drawn above the walls)"
                                      "    cyan = entrance / exit", font=font(10)))
    width = int(max(max_x - min_x + margin * 2, text_width + margin * 2))
    height = int(max_y - min_y) + margin * 2 + 74 * SUPERSAMPLE
    image = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(image)

    def translate(point: Tuple[float, float]) -> Tuple[float, float]:
        return (point[0] - min_x + margin, point[1] - min_y + margin + 56 * SUPERSAMPLE)

    # Floor slab (slightly larger than the maze so the border reads as a plateau).
    overhang = cell * 0.6
    floor = [translate(project(-overhang, -2, -overhang)), translate(project(size * cell + overhang, -2, -overhang)),
             translate(project(size * cell + overhang, -2, size * cell + overhang)),
             translate(project(-overhang, -2, size * cell + overhang))]
    draw.polygon(floor, fill=(34, 42, 60))

    # Walls, far to near (painter's algorithm): the viewer looks along -(x + z).
    walls = [(x, y) for y in range(size) for x in range(size) if grid.is_wall(x, y)]
    walls.sort(key=lambda cell_xy: (cell_xy[0] + cell_xy[1], cell_xy[0]))

    for x, y in walls:
        x0, z0 = x * cell, y * cell
        x1, z1 = x0 + cell, z0 + cell
        top = wall_height * cell
        # Only the faces that look towards the viewer can be seen: -Z and -X.
        if not grid.is_wall(x, y - 1):
            draw.polygon([translate(project(x0, 0, z0)), translate(project(x1, 0, z0)),
                          translate(project(x1, top, z0)), translate(project(x0, top, z0))],
                         fill=WALL_RIGHT)
        if not grid.is_wall(x - 1, y):
            draw.polygon([translate(project(x0, 0, z0)), translate(project(x0, 0, z1)),
                          translate(project(x0, top, z1)), translate(project(x0, top, z0))],
                         fill=WALL_LEFT)
        draw.polygon([translate(project(x0, top, z0)), translate(project(x1, top, z0)),
                      translate(project(x1, top, z1)), translate(project(x0, top, z1))],
                     fill=WALL_TOP)

    # The route is drawn as a translucent ribbon floating above the walls so that it stays
    # readable from a 3/4 view; without the overlay the wall tops hide the corridor floor.
    overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
    overlay_draw = ImageDraw.Draw(overlay)
    ribbon_y = wall_height * cell + cell * 0.9
    points = [translate(project((cell_x + 0.5) * cell, ribbon_y, (cell_y + 0.5) * cell))
              for cell_x, cell_y in path]
    if len(points) > 1:
        overlay_draw.line(points, fill=PATH + (215,), width=max(2, int(cell * 0.34)), joint="curve")
    for opening in grid.openings:
        cell_x, cell_y = opening.border_cell
        direction_x, direction_y = DIRECTION_OFFSETS[opening.side]
        point = translate(project((cell_x + 0.5 + direction_x * 0.4) * cell, cell * 0.15,
                                  (cell_y + 0.5 + direction_y * 0.4) * cell))
        radius = cell * 0.5
        overlay_draw.ellipse([point[0] - radius, point[1] - radius, point[0] + radius, point[1] + radius],
                             fill=OPENING + (230,))
    image = Image.alpha_composite(image.convert("RGBA"), overlay).convert("RGB")
    draw = ImageDraw.Draw(image)

    draw.text((margin, margin * 0.6), title, font=font(14, bold=True), fill=TEXT)
    draw.text((margin, margin * 0.6 + 22 * SUPERSAMPLE), subtitle, font=font(10), fill=TEXT_DIM)
    draw.text((margin, height - 20 * SUPERSAMPLE),
              "magenta = shortest entrance/exit route (drawn above the walls)    cyan = entrance / exit",
              font=font(10), fill=TEXT_DIM)

    save(image.resize((width // SUPERSAMPLE, height // SUPERSAMPLE), Image.LANCZOS),
         "mesh-preview.png")


def main() -> None:
    print("Rendering documentation images ...")
    render_algorithm_comparison()
    render_braid_comparison()
    render_solution_path()
    render_isometric()


if __name__ == "__main__":
    main()
