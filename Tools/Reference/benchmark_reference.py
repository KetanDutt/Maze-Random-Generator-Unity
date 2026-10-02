"""Performance characterisation of the maze core - runs without Unity.

The numbers printed by this script are quoted in ``Docs/Performance.md``.  It measures
the *algorithmic* properties of the implementation (operation counts, timing scaling,
mesh budget) rather than absolute Unity frame times, so the results are hardware
independent and reproducible.

Usage::

    python3 Tools/Reference/benchmark_reference.py
"""

from __future__ import annotations

import random
import time
from typing import Dict, List, Tuple

from maze_reference import (  # noqa: E402
    ALGORITHMS,
    DIRECTIONS,
    MazeGrid,
    create_algorithm,
    direction_offset,
    generate_maze,
)

# ---------------------------------------------------------------------------
# 1. Frontier container: HashSet + ElementAt() (original) vs List + swap-remove
# ---------------------------------------------------------------------------


def count_naive_frontier_operations(room_count_x: int, room_count_y: int, rng: random.Random) -> int:
    """Operations spent by ``frontier.ElementAt(randomIndex)`` on a HashSet.

    ``ElementAt`` walks the set from the beginning every time, so the walk length is the
    current frontier size.  It also allocates a fresh enumerator per call.
    """
    operations = 0
    visited = set()
    start = (rng.randrange(room_count_x), rng.randrange(room_count_y))
    visited.add(start)

    def neighbors(room: Tuple[int, int]) -> List[Tuple[int, int]]:
        result = []
        for direction in DIRECTIONS:
            dx, dy = direction_offset(direction)
            nx, ny = room[0] + dx, room[1] + dy
            if 0 <= nx < room_count_x and 0 <= ny < room_count_y:
                result.append((nx, ny))
        return result

    frontier = set(neighbors(start))
    while frontier:
        index = rng.randrange(len(frontier))
        operations += index + 1  # ElementAt() walks `index` entries
        room = list(frontier)[index]
        frontier.discard(room)
        visited.add(room)
        for neighbor in neighbors(room):
            if neighbor not in visited:
                frontier.add(neighbor)
    return operations


def count_fixed_frontier_operations(room_count_x: int, room_count_y: int, rng: random.Random) -> int:
    """Same traversal with an indexable list and O(1) swap-remove."""
    operations = 0
    visited = set()
    start = (rng.randrange(room_count_x), rng.randrange(room_count_y))
    visited.add(start)

    def neighbors(room: Tuple[int, int]) -> List[Tuple[int, int]]:
        result = []
        for direction in DIRECTIONS:
            dx, dy = direction_offset(direction)
            nx, ny = room[0] + dx, room[1] + dy
            if 0 <= nx < room_count_x and 0 <= ny < room_count_y:
                result.append((nx, ny))
        return result

    frontier = list(neighbors(start))
    while frontier:
        index = rng.randrange(len(frontier))
        operations += 1
        room = frontier[index]
        frontier[index] = frontier[-1]
        frontier.pop()
        if room in visited:
            continue
        visited.add(room)
        for neighbor in neighbors(room):
            if neighbor not in visited:
                frontier.append(neighbor)
    return operations


def benchmark_frontier() -> None:
    print("\n== Frontier container (Prim's algorithm) ==")
    print("%-10s %-16s %-16s %-10s" % ("maze", "HashSet+ElementAt", "List+swap-remove", "speedup"))
    for size in (21, 51, 101, 201):
        rng = random.Random(0)
        naive = count_naive_frontier_operations((size + 1) // 2, (size + 1) // 2, rng)
        rng = random.Random(0)
        fixed = count_fixed_frontier_operations((size + 1) // 2, (size + 1) // 2, rng)
        rooms = ((size + 1) // 2) ** 2
        print("%-10s %-16s %-16s %-10s" % (
            "%dx%d" % (size, size),
            "%d ops" % naive,
            "%d ops" % fixed,
            "%.0fx" % (naive / max(1, fixed)),
        ))
        print("%-10s (%.1f ops/room, %d enumerator allocations) -> O(rooms) with zero allocations"
              % ("", naive / rooms, rooms))


# ---------------------------------------------------------------------------
# 2. Generation time scaling
# ---------------------------------------------------------------------------


def benchmark_generation() -> None:
    print("\n== Generation time (pure algorithm, CPython 3.11 for reference) ==")
    print("%-10s %-16s %-16s %-16s %-16s" % ("maze", *ALGORITHMS))
    for size in (21, 51, 101, 251, 501):
        row = []
        for kind in ALGORITHMS:
            best = float("inf")
            for seed in range(3):
                start = time.perf_counter()
                generate_maze(size, size, algorithm=kind, seed=seed, opening_mode="none")
                best = min(best, time.perf_counter() - start)
            row.append("%.1f ms" % (best * 1000))
        print("%-10s %-16s %-16s %-16s %-16s" % ("%dx%d" % (size, size), *row))
    print("(the C# implementation is roughly an order of magnitude faster; use these numbers")
    print(" for scaling behaviour, not as absolute Unity frame times)")


# ---------------------------------------------------------------------------
# 3. Mesh budget
# ---------------------------------------------------------------------------


def mesh_stats(grid: MazeGrid, cell_size: float = 5.0) -> Dict[str, int]:
    """Exactly what ``MazeMeshBuilder`` emits for this grid (top faces + exposed sides)."""
    faces = 0
    for y in range(grid.height):
        for x in range(grid.width):
            if not grid.is_wall(x, y):
                continue
            faces += 1  # top face
            for direction in DIRECTIONS:
                dx, dy = direction_offset(direction)
                if grid.is_visible_neighbour(x + dx, y + dy):
                    faces += 1  # exposed side face
    return {
        "wall_cells": grid.count_walls(),
        "quads": faces,
        "triangles": faces * 2,
        "vertices": faces * 4,
        "legacy_game_objects": grid.count_walls(),
        "legacy_triangles": grid.count_walls() * 12,
    }


def benchmark_mesh() -> None:
    print("\n== Mesh budget (mesh mode vs. one GameObject per wall) ==")
    print("%-10s %-10s %-9s %-11s %-10s %-16s %-9s" % (
        "maze", "walls", "quads", "triangles", "vertices", "legacy objects", "legacy tris"))
    for size in (21, 51, 101, 251, 501):
        grid = generate_maze(size, size, algorithm="prim", seed=4, opening_mode="none")
        stats = mesh_stats(grid)
        print("%-10s %-10s %-9s %-11s %-10s %-16s %-9s" % (
            "%dx%d" % (size, size), stats["wall_cells"], stats["quads"], stats["triangles"],
            stats["vertices"], stats["legacy_game_objects"], stats["legacy_triangles"]))
    print("A 501x501 mesh exceeds 65535 vertices and therefore needs IndexFormat.UInt32.")
    print("Mesh mode: 1 draw call.  Legacy prefab mode: one draw call per wall object.")


def main() -> None:
    benchmark_frontier()
    benchmark_generation()
    benchmark_mesh()


if __name__ == "__main__":
    main()
