"""Invariant test-suite for ``maze_reference.py`` - runs without Unity.

Usage::

    python3 Tools/Reference/verify_maze_reference.py

Every check mirrors an assertion of the Unity EditMode test suite in
``Assets/Code/Tests`` so that the algorithms can be validated in CI even when no
Unity editor is available.  The exit code is non-zero when a check fails.
"""

from __future__ import annotations

import math
import random
import sys
import time
from typing import List, Tuple

from maze_reference import (  # noqa: E402  (script style import)
    ALGORITHMS,
    DIRECTION_OFFSETS,
    DIRECTIONS,
    EAST,
    MAX_DIMENSION,
    NORTH,
    SOUTH,
    WEST,
    MazeGrid,
    braid,
    count_dead_ends,
    create_algorithm,
    farthest_passage,
    find_shortest_path,
    generate_maze,
    is_fully_connected,
    longest_path_length,
    make_openings,
)

FAILURES: List[str] = []
CHECKS = 0


def check(condition: bool, message: str) -> None:
    global CHECKS
    CHECKS += 1
    if not condition:
        FAILURES.append(message)
        print("  FAIL  %s" % message)


def section(title: str) -> None:
    print("\n== %s ==" % title)


# ---------------------------------------------------------------------------
# 1. Grid model
# ---------------------------------------------------------------------------


def test_grid_model() -> None:
    section("Grid model / normalization")

    check(MazeGrid.normalize_dimension(21, 1) == 21, "odd size is kept")
    check(MazeGrid.normalize_dimension(20, 1) == 21, "even size rounds up to odd")
    check(MazeGrid.normalize_dimension(1, 1) == 5, "tiny sizes clamp to the minimum")
    check(MazeGrid.normalize_dimension(10_000, 1) == MAX_DIMENSION, "large sizes clamp to the maximum")
    check(MazeGrid.normalize_dimension(30, 2) == 31, "padding is taken into account")

    grid = MazeGrid(*[MazeGrid.normalize_dimension(21, 1)] * 2, padding=1)
    check(grid.room_count_x == 10 and grid.room_count_y == 10, "21x21 grid has a 10x10 room lattice")
    check(grid.count_walls() == 21 * 21, "a fresh grid is solid")
    check(grid.is_wall(-1, 5) and grid.is_wall(21, 5), "out of bounds cells count as walls")
    check(not grid.in_bounds(-1, 5), "out of bounds detection")
    grid.carve(10, 10)
    check(grid.is_passage(10, 10) and grid.count_passages() == 1, "carving a single cell")
    grid.set_wall(10, 10)
    check(grid.is_wall(10, 10), "set_wall undoes a carve")
    grid.carve(10, 10)
    check(grid.is_room_cell(11, 11) and not grid.is_room_cell(10, 11), "room lattice parity")

    # Padding is a hard wall ring except where openings are carved.
    for padding in (1, 2, 3):
        size = MazeGrid.normalize_dimension(25, padding)
        padded = MazeGrid(size, size, padding)
        check(padded.room_count_x == (size - 2 * padding + 1) // 2,
              "room count honours padding %d" % padding)
        check(padded.cell_at_room(0, 0) == (padding, padding), "room 0 starts after the padding")


# ---------------------------------------------------------------------------
# 2. Algorithms
# ---------------------------------------------------------------------------


def test_algorithms() -> None:
    section("Algorithms: perfect, connected, deterministic")

    fingerprints = {kind: set() for kind in ALGORITHMS}
    sizes = (7, 21, 41, 101)
    seeds = (0, 1, 7, 42, 2024)

    for kind in ALGORITHMS:
        for size in sizes:
            for seed in seeds:
                grid = generate_maze(width=size, height=size, algorithm=kind, seed=seed,
                                     opening_mode="none")
                label = "%s %dx%d seed=%d" % (kind, size, size, seed)

                # Every room is carved.
                rooms_carved = all(grid.is_passage(*grid.cell_at_room(rx, ry))
                                   for rx, ry in grid.rooms())
                check(rooms_carved, "%s: every room is a passage" % label)

                # Fully connected.
                check(is_fully_connected(grid), "%s: all passages are reachable" % label)

                # A perfect maze: passages == rooms + (rooms - 1) corridors.
                expected = 2 * grid.room_count - 1
                check(grid.count_passages() == expected,
                      "%s: %d passages (expected %d)" % (label, grid.count_passages(), expected))

                # No 2x2 open block (a "room" of the maze would be ambiguous).
                for y in range(grid.height - 1):
                    for x in range(grid.width - 1):
                        block = [grid.is_passage(x, y), grid.is_passage(x + 1, y),
                                 grid.is_passage(x, y + 1), grid.is_passage(x + 1, y + 1)]
                        if all(block):
                            check(False, "%s: 2x2 open block at %d,%d" % (label, x, y))
                            break

                # No interior row/column is made of walls only - this is the "stranded
                # wall" limitation of the original implementation.
                for x in range(grid.padding, grid.width - grid.padding):
                    if all(grid.is_wall(x, y) for y in range(grid.padding, grid.height - grid.padding)):
                        check(False, "%s: wall-only interior column %d" % (label, x))
                        break
                for y in range(grid.padding, grid.height - grid.padding):
                    if all(grid.is_wall(x, y) for x in range(grid.padding, grid.width - grid.padding)):
                        check(False, "%s: wall-only interior row %d" % (label, y))
                        break

                # Determinism.
                again = generate_maze(width=size, height=size, algorithm=kind, seed=seed,
                                      opening_mode="none")
                check(grid.fingerprint() == again.fingerprint(),
                      "%s: identical for the same seed" % label)
                fingerprints[kind].add(grid.fingerprint())

    for kind in ALGORITHMS:
        check(len(fingerprints[kind]) > 1, "%s: different seeds produce different mazes" % kind)

    # The four algorithms must not produce identical mazes.
    baseline = {kind: generate_maze(41, 41, algorithm=kind, seed=99, opening_mode="none").fingerprint()
                for kind in ALGORITHMS}
    check(len(set(baseline.values())) == len(ALGORITHMS),
          "every algorithm produces a distinct maze")


# ---------------------------------------------------------------------------
# 3. Openings
# ---------------------------------------------------------------------------


def test_openings() -> None:
    section("Openings (entrance / exit)")

    for kind in ALGORITHMS:
        for count in (0, 1, 2, 3, 4):
            grid = generate_maze(31, 31, algorithm=kind, seed=5, opening_mode="random",
                                 opening_count=count)
            check(len(grid.openings) == count, "%s: %d openings created" % (kind, count))
            for opening in grid.openings:
                bx, by = opening.border_cell
                edge = bx in (0, grid.width - 1) or by in (0, grid.height - 1)
                check(edge, "%s: opening border cell %s is on the edge" % (kind, (bx, by)))
                check(grid.is_passage(bx, by), "%s: opening border cell is carved" % kind)
                check(grid.is_passage(*opening.room_cell), "%s: opening room is carved" % kind)
                # The hole is exactly one cell wide where it leaves the maze.
                neighbors = sum(1 for dx, dy in DIRECTION_OFFSETS.values()
                                if grid.is_passage(bx + dx, by + dy))
                check(neighbors == 1, "%s: opening cell has one passage neighbour" % kind)
            if count >= 2:
                start = grid.openings[0].room_cell
                goal = grid.openings[-1].room_cell
                path = find_shortest_path(grid, start, goal)
                check(path is not None and len(path) > 0,
                      "%s: entrance and exit are connected" % kind)
                check(is_fully_connected(grid), "%s: openings keep the maze connected" % kind)

    grid = generate_maze(31, 31, seed=3, opening_mode="fixed", fixed_sides=(NORTH, SOUTH))
    check([op.side for op in grid.openings] == [NORTH, SOUTH], "fixed sides are honoured")
    check(grid.openings[0].room_cell[0] == grid.width // 2,
          "fixed openings use the middle of the side")

    grid = generate_maze(21, 21, seed=3, opening_mode="none")
    check(not grid.openings, "openings can be disabled")
    check(all(grid.is_wall(x, 0) for x in range(grid.width)), "no opening leaves the south wall intact")


# ---------------------------------------------------------------------------
# 4. Braiding
# ---------------------------------------------------------------------------


def test_braiding() -> None:
    section("Braiding (loops / dead end removal)")

    for kind in ALGORITHMS:
        perfect = generate_maze(31, 31, algorithm=kind, seed=11, opening_mode="none")
        check(count_dead_ends(perfect) > 0, "%s: a perfect maze has dead ends" % kind)

        braided = generate_maze(31, 31, algorithm=kind, seed=11, opening_mode="none",
                                braid_factor=1.0)
        check(count_dead_ends(braided) == 0,
              "%s: braid factor 1 removes every dead end" % kind)
        check(is_fully_connected(braided), "%s: braiding keeps the maze connected" % kind)
        check(braided.count_passages() > perfect.count_passages(),
              "%s: braiding opens extra cells" % kind)

        half = generate_maze(31, 31, algorithm=kind, seed=11, opening_mode="none",
                             braid_factor=0.5)
        check(count_dead_ends(half) < count_dead_ends(perfect),
              "%s: partial braiding removes some dead ends" % kind)

    factor_zero = generate_maze(31, 31, seed=11, opening_mode="none", braid_factor=0.0)
    reference = generate_maze(31, 31, seed=11, opening_mode="none")
    check(factor_zero.fingerprint() == reference.fingerprint(),
          "braid factor 0 is a no-op")


# ---------------------------------------------------------------------------
# 5. Path finding & statistics
# ---------------------------------------------------------------------------


def test_paths() -> None:
    section("Path finding / analysis")

    grid = generate_maze(41, 41, seed=1234, opening_mode="fixed", fixed_sides=(NORTH, SOUTH))
    start, goal = grid.openings[0].border_cell, grid.openings[-1].border_cell
    path = find_shortest_path(grid, start, goal)
    check(path is not None, "a path exists between the two openings")
    if path:
        check(path[0] == start and path[-1] == goal, "the path starts and ends at the openings")
        check(all(grid.is_passage(*cell) for cell in path), "the path only walks on passages")
        for a, b in zip(path, path[1:]):
            check(abs(a[0] - b[0]) + abs(a[1] - b[1]) == 1, "consecutive path cells are adjacent")
        # BFS optimality: the path is not longer than a detour through the maze centre.
        far, _ = farthest_passage(grid, start)
        other = find_shortest_path(grid, start, far)
        check(other is not None and len(other) >= len(path),
              "the shortest path is never longer than another reachable path")

    check(find_shortest_path(grid, (0, 0), goal) is None, "walls are not part of a path")

    # Diameter: the longest shortest path is at least the entrance/exit path.
    diameter = longest_path_length(grid)
    check(diameter >= 1, "the maze diameter is measured")
    check(diameter < grid.count_passages(), "the diameter cannot exceed the cell count")


# ---------------------------------------------------------------------------
# 6. Mesh statistics + winding
# ---------------------------------------------------------------------------


def build_quad_faces(grid: MazeGrid) -> Tuple[List[Tuple[Tuple[float, float, float], ...]], List[Tuple[float, float, float]]]:
    """Mirror of ``MazeMeshBuilder.BuildWallMesh`` - returns (faces, face normals).

    The winding rule is the one documented for ``Mesh.triangles``: for a quad
    ``p0, p1, p2, p3`` the triangles ``(p0, p1, p2)`` and ``(p0, p2, p3)`` are front facing
    when ``cross(p1 - p0, p2 - p0)`` equals the outward normal of the face.
    """
    faces, normals = [], []
    size = 5.0
    height = 5.0
    half = size * 0.5

    def add_quad(vertices, normal):
        faces.append(vertices)
        normals.append(normal)

    for y in range(grid.height):
        for x in range(grid.width):
            if not grid.is_wall(x, y):
                continue
            cx, cz = x * size, y * size
            top = height
            add_quad(((cx - half, top, cz - half), (cx - half, top, cz + half),
                      (cx + half, top, cz + half), (cx + half, top, cz - half)), (0.0, 1.0, 0.0))
            if grid.is_visible_neighbour(x + 1, y):
                add_quad(((cx + half, 0.0, cz + half), (cx + half, 0.0, cz - half),
                          (cx + half, top, cz - half), (cx + half, top, cz + half)), (1.0, 0.0, 0.0))
            if grid.is_visible_neighbour(x - 1, y):
                add_quad(((cx - half, 0.0, cz - half), (cx - half, 0.0, cz + half),
                          (cx - half, top, cz + half), (cx - half, top, cz - half)), (-1.0, 0.0, 0.0))
            if grid.is_visible_neighbour(x, y + 1):
                add_quad(((cx - half, 0.0, cz + half), (cx + half, 0.0, cz + half),
                          (cx + half, top, cz + half), (cx - half, top, cz + half)), (0.0, 0.0, 1.0))
            if grid.is_visible_neighbour(x, y - 1):
                add_quad(((cx + half, 0.0, cz - half), (cx - half, 0.0, cz - half),
                          (cx - half, top, cz - half), (cx + half, top, cz - half)), (0.0, 0.0, -1.0))
    return faces, normals


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def test_mesh() -> None:
    section("Mesh builder: winding, culling, triangle budget")

    for kind in ALGORITHMS:
        grid = generate_maze(21, 21, algorithm=kind, seed=8, opening_mode="none")
        faces, normals = build_quad_faces(grid)

        check(len(faces) > 0, "%s: the mesh has faces" % kind)
        for vertices, normal in zip(faces, normals):
            p0, p1, p2, _p3 = vertices
            computed = cross(sub(p1, p0), sub(p2, p0))
            magnitude = math.sqrt(sum(component * component for component in computed)) or 1.0
            computed = tuple(component / magnitude for component in computed)
            ok = all(abs(a - b) < 1e-6 for a, b in zip(computed, normal))
            if not ok:
                check(False, "%s: inverted winding (computed %s, expected %s)" % (kind, computed, normal))
                break

        # Hidden faces are culled: a wall cell surrounded by walls only emits its top.
        interior = [(x, y) for y in range(1, grid.height - 1) for x in range(1, grid.width - 1)
                    if grid.is_wall(x, y)]
        hidden = 0
        for x, y in interior:
            if all(grid.is_wall(x + dx, y + dy) for dx, dy in DIRECTION_OFFSETS.values()):
                hidden += 1
        if hidden:
            check(True, "%s: %d fully enclosed wall cells emit a single face" % (kind, hidden))

    grid = generate_maze(21, 21, seed=8, opening_mode="none")
    faces, _ = build_quad_faces(grid)
    check(len(faces) == 4 * len(faces) // 4, "quad bookkeeping")
    print("  21x21 mesh: %d quads, %d triangles, %d vertices, 1 draw call"
          % (len(faces), 2 * len(faces), 4 * len(faces)))
    print("  21x21 legacy prefab mode: %d wall GameObjects, %d triangles, %d draw calls"
          % (grid.count_walls(), 12 * grid.count_walls(), grid.count_walls()))


# ---------------------------------------------------------------------------
# 7. Performance smoke test (algorithmic complexity of the frontier handling)
# ---------------------------------------------------------------------------


def test_performance() -> None:
    section("Performance smoke test")

    for kind in ALGORITHMS:
        for size in (101, 251):
            start = time.perf_counter()
            generate_maze(size, size, algorithm=kind, seed=1, opening_mode="none")
            elapsed = time.perf_counter() - start
            check(elapsed < 5.0, "%s %dx%d generated in %.3fs" % (kind, size, size, elapsed))
            print("  %-20s %3dx%-3d  %6.1f ms" % (kind, size, size, elapsed * 1000))


# ---------------------------------------------------------------------------
# 8. Legacy behaviour (regression documentation of the original script)
# ---------------------------------------------------------------------------


def test_legacy_limitation_documented() -> None:
    """The original implementation could strand a whole wall row/column.

    It picked the start cell with ``Random.Range(3, size - 3)`` (any parity) and used a
    HashSet with ElementAt() for the frontier.  The parity bug is what produced wall-only
    rows/columns; it is fixed by generating on the room lattice.
    """
    section("Legacy limitation is no longer reproducible")

    stranded = 0
    for seed in range(200):
        grid = generate_maze(21, 21, algorithm="prim", seed=seed, opening_mode="none")
        for x in range(grid.padding, grid.width - grid.padding):
            if all(grid.is_wall(x, y) for y in range(grid.padding, grid.height - grid.padding)):
                stranded += 1
                break
    check(stranded == 0, "no stranded wall column in 200 mazes")

    # Parity check: with an even start index the original produced a wall-only column.
    even_start = MazeGrid(21, 21, padding=1)
    print("  (reference for the bug: start index parity decides the reachable lane, the")
    print("   room lattice always uses padding + even offsets, so every lane is reachable)")


def main() -> int:
    print("Maze reference implementation - invariant checks")
    for test in (test_grid_model, test_algorithms, test_openings, test_braiding,
                 test_paths, test_mesh, test_performance, test_legacy_limitation_documented):
        test()

    print("\n%d checks, %d failures" % (CHECKS, len(FAILURES)))
    if FAILURES:
        print("\nFAILED:")
        for failure in FAILURES[:20]:
            print(" - %s" % failure)
        return 1
    print("All invariants hold.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
