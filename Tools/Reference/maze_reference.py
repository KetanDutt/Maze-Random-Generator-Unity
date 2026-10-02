"""Reference implementation of the maze generation core used by this Unity project.

This module is a dependency-free, language-agnostic mirror of the C# code that lives in
``Assets/Code/Scripts``.  It exists for three reasons:

1. **Verification** - the maze algorithms can be validated (perfectness, connectivity,
   determinism, openings, braiding, ...) outside of the Unity editor by running
   ``Tools/Reference/verify_maze_reference.py``.  The C# unit tests in
   ``Assets/Code/Tests`` assert the very same invariants inside Unity.
2. **Documentation** - ``Tools/Reference/generate_docs_images.py`` renders the diagrams
   that are embedded in the ``Docs`` folder straight from these algorithms.
3. **Portability** - if you want to port the generator to another engine, the algorithms,
   the grid model and the post-processing steps are all contained in this single file.

The public names deliberately follow the C# implementation (``carve``, ``room_count_x``,
``carve_corridor``, ...) so that the two can be diffed side by side.

Nothing in here is compiled into the Unity player; it is a developer tool.
"""

from __future__ import annotations

import random
from typing import Iterable, List, Optional, Sequence, Tuple

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

WALL = True
PASSAGE = False

#: Minimum size of the *interior* (the carve-able region) in cells. Must be odd.
MIN_INTERIOR_SIZE = 3

#: Hard cap for a single maze dimension. Keeps mesh memory/O(n) work sane.
MAX_DIMENSION = 501

#: Cardinal directions. ``NORTH`` increases the grid *y* axis (world +Z).
NORTH, EAST, SOUTH, WEST = 0, 1, 2, 3
DIRECTIONS = (NORTH, EAST, SOUTH, WEST)
DIRECTION_OFFSETS = {NORTH: (0, 1), EAST: (1, 0), SOUTH: (0, -1), WEST: (-1, 0)}
DIRECTION_NAMES = {NORTH: "North", EAST: "East", SOUTH: "South", WEST: "West"}


def direction_opposite(direction: int) -> int:
    return (direction + 2) % 4


def direction_offset(direction: int) -> Tuple[int, int]:
    return DIRECTION_OFFSETS[direction]


# ---------------------------------------------------------------------------
# Grid
# ---------------------------------------------------------------------------


class MazeGrid:
    """A rectangular grid of cells that are either a ``WALL`` or a ``PASSAGE``.

    The grid is always *odd sized* and surrounded by ``padding`` rings of cells that are
    forced to stay walls (openings are the only exception).  The cells that can host a
    maze room are those whose distance to the padding boundary is even, i.e. the "room
    lattice".  Rooms are two cells apart, the cell in between is the corridor.
    """

    def __init__(self, width: int, height: int, padding: int = 1) -> None:
        if width < 2 * padding + MIN_INTERIOR_SIZE or height < 2 * padding + MIN_INTERIOR_SIZE:
            raise ValueError("grid too small for the requested padding")
        if (width - 2 * padding) % 2 == 0 or (height - 2 * padding) % 2 == 0:
            raise ValueError("interior dimensions must be odd")
        self.width = width
        self.height = height
        self.padding = padding
        self.cells: List[bool] = [WALL] * (width * height)
        self.openings: List["MazeOpening"] = []

    # -- geometry ---------------------------------------------------------

    @property
    def room_count_x(self) -> int:
        return (self.width - 2 * self.padding + 1) // 2

    @property
    def room_count_y(self) -> int:
        return (self.height - 2 * self.padding + 1) // 2

    @property
    def room_count(self) -> int:
        return self.room_count_x * self.room_count_y

    def in_bounds(self, x: int, y: int) -> bool:
        return 0 <= x < self.width and 0 <= y < self.height

    def is_room_cell(self, x: int, y: int) -> bool:
        if not self.in_bounds(x, y):
            return False
        return (x - self.padding) % 2 == 0 and (y - self.padding) % 2 == 0

    def cell_at_room(self, rx: int, ry: int) -> Tuple[int, int]:
        return self.padding + rx * 2, self.padding + ry * 2

    def room_at_cell(self, x: int, y: int) -> Tuple[int, int]:
        return (x - self.padding) // 2, (y - self.padding) // 2

    # -- cells ------------------------------------------------------------

    def is_wall(self, x: int, y: int) -> bool:
        if not self.in_bounds(x, y):
            return True
        return self.cells[y * self.width + x]

    def is_passage(self, x: int, y: int) -> bool:
        if not self.in_bounds(x, y):
            return False
        return not self.cells[y * self.width + x]

    def set_wall(self, x: int, y: int) -> None:
        if self.in_bounds(x, y):
            self.cells[y * self.width + x] = WALL

    def carve(self, x: int, y: int) -> None:
        if self.in_bounds(x, y):
            self.cells[y * self.width + x] = PASSAGE

    def count_walls(self) -> int:
        return sum(1 for cell in self.cells if cell)

    def count_passages(self) -> int:
        return len(self.cells) - self.count_walls()

    # -- room connections -------------------------------------------------

    def is_room_in_bounds(self, rx: int, ry: int) -> bool:
        return 0 <= rx < self.room_count_x and 0 <= ry < self.room_count_y

    def is_corridor_carved(self, rx: int, ry: int, direction: int) -> bool:
        """``True`` when the room ``(rx, ry)`` is already connected towards ``direction``."""
        dx, dy = direction_offset(direction)
        nx, ny = rx + dx, ry + dy
        if not self.is_room_in_bounds(nx, ny):
            return False
        x, y = self.cell_at_room(rx, ry)
        return self.is_passage(x + dx, y + dy)

    def carve_corridor(self, rx: int, ry: int, direction: int) -> None:
        """Connect the room ``(rx, ry)`` with its neighbour towards ``direction``.

        Carves **both** rooms plus the cell in between, so callers never have to remember
        to carve the destination room (a mistake that leaves isolated rooms behind).
        """
        dx, dy = direction_offset(direction)
        nx, ny = rx + dx, ry + dy
        if not (self.is_room_in_bounds(rx, ry) and self.is_room_in_bounds(nx, ny)):
            return
        x, y = self.cell_at_room(rx, ry)
        self.carve(x, y)
        self.carve(x + dx, y + dy)
        self.carve(*self.cell_at_room(nx, ny))

    def room_degree(self, rx: int, ry: int) -> int:
        return sum(1 for d in DIRECTIONS if self.is_corridor_carved(rx, ry, d))

    def room_neighbors(self, rx: int, ry: int) -> List[Tuple[int, int]]:
        result = []
        for d in DIRECTIONS:
            dx, dy = direction_offset(d)
            if self.is_room_in_bounds(rx + dx, ry + dy):
                result.append((rx + dx, ry + dy))
        return result

    def is_visible_neighbour(self, x: int, y: int) -> bool:
        """True when a face towards this neighbour is visible (passage or outside the grid)."""
        return not self.in_bounds(x, y) or not self.is_wall(x, y)

    def is_opening_room(self, rx: int, ry: int) -> bool:
        cell = self.cell_at_room(rx, ry)
        return any(opening.room_cell == cell for opening in self.openings)

    # -- helpers ----------------------------------------------------------

    def clone(self) -> "MazeGrid":
        other = MazeGrid(self.width, self.height, self.padding)
        other.cells = list(self.cells)
        other.openings = list(self.openings)
        return other

    def fingerprint(self) -> int:
        """FNV-1a 64 bit hash of the grid, used to compare mazes deterministically."""
        value = 0xCBF29CE484222325
        for cell in self.cells:
            value ^= 1 if cell else 0
            value = (value * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
        return value

    def to_ascii(self) -> str:
        rows = []
        for y in range(self.height - 1, -1, -1):
            rows.append("".join("#" if self.is_wall(x, y) else "." for x in range(self.width)))
        return "\n".join(rows)

    def passages(self) -> Iterable[Tuple[int, int]]:
        for y in range(self.height):
            for x in range(self.width):
                if self.is_passage(x, y):
                    yield x, y

    def rooms(self) -> Iterable[Tuple[int, int]]:
        for ry in range(self.room_count_y):
            for rx in range(self.room_count_x):
                yield rx, ry

    @staticmethod
    def normalize_dimension(requested: int, padding: int) -> int:
        """Clamp a requested dimension so the interior is odd and large enough."""
        value = max(int(requested), 2 * padding + MIN_INTERIOR_SIZE)
        value = min(value, MAX_DIMENSION)
        if (value - 2 * padding) % 2 == 0:
            value += 1
        return value


# ---------------------------------------------------------------------------
# Openings
# ---------------------------------------------------------------------------


class MazeOpening:
    """A hole in the outer wall of the maze."""

    __slots__ = ("side", "room_cell", "border_cell")

    def __init__(self, side: int, room_cell: Tuple[int, int], border_cell: Tuple[int, int]) -> None:
        self.side = side
        self.room_cell = room_cell
        self.border_cell = border_cell

    def __repr__(self) -> str:  # pragma: no cover - debug helper
        return "MazeOpening(%s, room=%s, border=%s)" % (
            DIRECTION_NAMES[self.side],
            self.room_cell,
            self.border_cell,
        )


def apply_openings(grid: MazeGrid, sides: Sequence[int], rng: random.Random,
                   middle: bool = True) -> List[MazeOpening]:
    """Carve one opening per entry in ``sides`` and register them on the grid."""
    created: List[MazeOpening] = []
    for side in sides:
        if side == NORTH or side == SOUTH:
            span = grid.room_count_x
            index = span // 2 if middle else rng.randrange(span)
            rx = index
            ry = grid.room_count_y - 1 if side == NORTH else 0
        else:
            span = grid.room_count_y
            index = span // 2 if middle else rng.randrange(span)
            rx = grid.room_count_x - 1 if side == EAST else 0
            ry = index

        room = grid.cell_at_room(rx, ry)
        dx, dy = direction_offset(side)
        border = room
        while grid.in_bounds(border[0] + dx, border[1] + dy):
            border = (border[0] + dx, border[1] + dy)
        # Carve from the room outwards, through the padding, to the grid edge.
        x, y = room
        while True:
            grid.carve(x, y)
            if (x, y) == border:
                break
            x, y = x + dx, y + dy
        created.append(MazeOpening(side, room, border))

    grid.openings.extend(created)
    return created


def make_openings(grid: MazeGrid, mode: str, rng: random.Random,
                  count: int = 2, fixed_sides: Sequence[int] = (NORTH, SOUTH)):
    """``mode`` is one of ``"none"``, ``"random"`` or ``"fixed"``."""
    if mode == "none" or count == 0:
        return []
    if mode == "fixed":
        sides = [s for s in DIRECTIONS if s in fixed_sides]
        return apply_openings(grid, sides, rng, middle=True)

    count = max(0, min(4, int(count)))
    sides = list(DIRECTIONS)
    rng.shuffle(sides)
    return apply_openings(grid, sides[:count], rng, middle=False)


# ---------------------------------------------------------------------------
# Algorithms
# ---------------------------------------------------------------------------


class IMazeAlgorithm:
    """Base class for every generation algorithm."""

    name = "Maze"

    def generate(self, grid: MazeGrid, rng: random.Random) -> None:  # pragma: no cover
        raise NotImplementedError


class RandomizedPrimAlgorithm(IMazeAlgorithm):
    """Randomized Prim's algorithm - grows the maze from a single room."""

    name = "Randomized Prim"

    def generate(self, grid: MazeGrid, rng: random.Random) -> None:
        visited = set()
        start = (rng.randrange(grid.room_count_x), rng.randrange(grid.room_count_y))
        visited.add(start)
        grid.carve(*grid.cell_at_room(*start))

        frontier = list(grid.room_neighbors(*start))
        while frontier:
            index = rng.randrange(len(frontier))
            room = frontier[index]
            frontier[index] = frontier[-1]  # O(1) swap-remove: the original C# used an
            frontier.pop()                  # O(n) ElementAt() on a HashSet here.

            if room in visited:
                continue

            candidates = [n for n in grid.room_neighbors(*room) if n in visited]
            parent = candidates[rng.randrange(len(candidates))]
            dx = parent[0] - room[0]
            dy = parent[1] - room[1]
            grid.carve_corridor(room[0], room[1], EAST if dx > 0 else WEST if dx < 0 else NORTH if dy > 0 else SOUTH)
            visited.add(room)

            for neighbor in grid.room_neighbors(*room):
                if neighbor not in visited:
                    frontier.append(neighbor)


class DepthFirstAlgorithm(IMazeAlgorithm):
    """Randomised depth first search / recursive backtracker."""

    name = "Depth First"

    def generate(self, grid: MazeGrid, rng: random.Random) -> None:
        start = (rng.randrange(grid.room_count_x), rng.randrange(grid.room_count_y))
        grid.carve(*grid.cell_at_room(*start))
        visited = {start}
        stack = [start]

        while stack:
            rx, ry = stack[-1]
            candidates = [n for n in grid.room_neighbors(rx, ry) if n not in visited]
            if not candidates:
                stack.pop()
                continue
            neighbor = candidates[rng.randrange(len(candidates))]
            dx = neighbor[0] - rx
            dy = neighbor[1] - ry
            grid.carve_corridor(rx, ry, EAST if dx > 0 else WEST if dx < 0 else NORTH if dy > 0 else SOUTH)
            visited.add(neighbor)
            stack.append(neighbor)


class RandomizedKruskalAlgorithm(IMazeAlgorithm):
    """Randomized Kruskal's algorithm - uniform spanning tree through union-find."""

    name = "Randomized Kruskal"

    def generate(self, grid: MazeGrid, rng: random.Random) -> None:
        room_count_x, room_count_y = grid.room_count_x, grid.room_count_y
        total = room_count_x * room_count_y

        parent = list(range(total))
        rank = [0] * total

        def find(node: int) -> int:
            while parent[node] != node:
                parent[node] = parent[parent[node]]
                node = parent[node]
            return node

        def union(a: int, b: int) -> bool:
            root_a, root_b = find(a), find(b)
            if root_a == root_b:
                return False
            if rank[root_a] < rank[root_b]:
                root_a, root_b = root_b, root_a
            parent[root_b] = root_a
            if rank[root_a] == rank[root_b]:
                rank[root_a] += 1
            return True

        edges = []
        for ry in range(room_count_y):
            for rx in range(room_count_x):
                node = ry * room_count_x + rx
                if rx + 1 < room_count_x:
                    edges.append((node, node + 1, rx, ry, EAST))
                if ry + 1 < room_count_y:
                    edges.append((node, node + room_count_x, rx, ry, NORTH))

        # Fisher-Yates shuffle driven by the seeded RNG (deterministic per seed).
        for i in range(len(edges) - 1, 0, -1):
            j = rng.randrange(i + 1)
            edges[i], edges[j] = edges[j], edges[i]

        for a, b, rx, ry, direction in edges:
            if union(a, b):
                grid.carve_corridor(rx, ry, direction)


class BinaryTreeAlgorithm(IMazeAlgorithm):
    """Binary tree algorithm - simple, fast and heavily biased to two sides."""

    name = "Binary Tree"

    def generate(self, grid: MazeGrid, rng: random.Random) -> None:
        for ry in range(grid.room_count_y):
            for rx in range(grid.room_count_x):
                grid.carve(*grid.cell_at_room(rx, ry))
        for ry in range(grid.room_count_y):
            for rx in range(grid.room_count_x):
                can_north = ry + 1 < grid.room_count_y
                can_east = rx + 1 < grid.room_count_x
                if not can_north and not can_east:
                    continue
                if can_north and can_east:
                    direction = NORTH if rng.random() < 0.5 else EAST
                else:
                    direction = NORTH if can_north else EAST
                grid.carve_corridor(rx, ry, direction)


ALGORITHMS = ("prim", "dfs", "kruskal", "binary-tree")


def create_algorithm(kind: str) -> IMazeAlgorithm:
    if kind == "prim":
        return RandomizedPrimAlgorithm()
    if kind == "dfs":
        return DepthFirstAlgorithm()
    if kind == "kruskal":
        return RandomizedKruskalAlgorithm()
    if kind == "binary-tree":
        return BinaryTreeAlgorithm()
    raise ValueError("unknown algorithm: %s" % kind)


# ---------------------------------------------------------------------------
# Post processing
# ---------------------------------------------------------------------------


def count_dead_ends(grid: MazeGrid) -> int:
    """Rooms with a single connection (corridors plus border openings)."""
    dead_ends = 0
    for rx, ry in grid.rooms():
        connections = grid.room_degree(rx, ry)
        if grid.is_opening_room(rx, ry):
            connections += 1
        if connections == 1:
            dead_ends += 1
    return dead_ends


def braid(grid: MazeGrid, factor: float, rng: random.Random) -> int:
    """Remove dead ends by knocking extra corridors into the maze (creates loops)."""
    if factor <= 0:
        return 0
    factor = min(1.0, factor)

    rooms = list(grid.rooms())
    rng.shuffle(rooms)
    braided = 0
    for rx, ry in rooms:
        if grid.room_degree(rx, ry) != 1 or grid.is_opening_room(rx, ry):
            continue
        if rng.random() >= factor:
            continue
        candidates = [d for d in DIRECTIONS
                      if grid.is_room_in_bounds(rx + direction_offset(d)[0], ry + direction_offset(d)[1])
                      and not grid.is_corridor_carved(rx, ry, d)]
        if not candidates:
            continue
        grid.carve_corridor(rx, ry, candidates[rng.randrange(len(candidates))])
        braided += 1
    return braided


# ---------------------------------------------------------------------------
# Analysis & path finding
# ---------------------------------------------------------------------------


def find_shortest_path(grid: MazeGrid, start: Tuple[int, int],
                       goal: Tuple[int, int]) -> Optional[List[Tuple[int, int]]]:
    """Breadth first search over passage cells; returns ``None`` when unreachable."""
    if grid.is_wall(*start) or grid.is_wall(*goal):
        return None
    size = grid.width * grid.height
    previous = [-1] * size
    start_index = start[1] * grid.width + start[0]
    goal_index = goal[1] * grid.width + goal[0]
    previous[start_index] = start_index
    queue = [start_index]
    head = 0
    while head < len(queue):
        index = queue[head]
        head += 1
        if index == goal_index:
            break
        x, y = index % grid.width, index // grid.width
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if not grid.is_passage(nx, ny):
                continue
            n_index = ny * grid.width + nx
            if previous[n_index] != -1:
                continue
            previous[n_index] = index
            queue.append(n_index)
    if previous[goal_index] == -1:
        return None
    path = []
    index = goal_index
    while index != start_index:
        path.append((index % grid.width, index // grid.width))
        index = previous[index]
    path.append(start)
    path.reverse()
    return path


def farthest_passage(grid: MazeGrid, start: Tuple[int, int]):
    """Return ``(cell, distance)`` of the passage cell farthest away from ``start``."""
    size = grid.width * grid.height
    distance = [-1] * size
    start_index = start[1] * grid.width + start[0]
    distance[start_index] = 0
    queue = [start_index]
    head = 0
    best_index, best_distance = start_index, 0
    while head < len(queue):
        index = queue[head]
        head += 1
        if distance[index] > best_distance:
            best_index, best_distance = index, distance[index]
        x, y = index % grid.width, index // grid.width
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if not grid.is_passage(nx, ny):
                continue
            n_index = ny * grid.width + nx
            if distance[n_index] != -1:
                continue
            distance[n_index] = distance[index] + 1
            queue.append(n_index)
    return (best_index % grid.width, best_index // grid.width), best_distance


def longest_path_length(grid: MazeGrid) -> int:
    """Length of the longest shortest path between any two passage cells (maze diameter)."""
    first_passage = next(iter(grid.passages()), None)
    if first_passage is None:
        return 0
    far, _ = farthest_passage(grid, first_passage)
    _, distance = farthest_passage(grid, far)
    return distance


def is_fully_connected(grid: MazeGrid) -> bool:
    """``True`` when every passage cell can be reached from any other passage cell."""
    passages = list(grid.passages())
    if not passages:
        return True
    reached = set()
    start = passages[0]
    stack = [start]
    reached.add(start)
    while stack:
        x, y = stack.pop()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            neighbor = (x + dx, y + dy)
            if neighbor in reached or not grid.is_passage(*neighbor):
                continue
            reached.add(neighbor)
            stack.append(neighbor)
    return len(reached) == len(passages)


# ---------------------------------------------------------------------------
# High level entry point (mirrors MazeGenerator's pipeline)
# ---------------------------------------------------------------------------


def generate_maze(width: int = 21, height: int = 21, padding: int = 1, algorithm: str = "prim",
                  seed: int = 0, braid_factor: float = 0.0, opening_mode: str = "random",
                  opening_count: int = 2, fixed_sides: Sequence[int] = (NORTH, SOUTH)):
    """Run the full pipeline (normalize -> carve -> openings -> braid) and return the grid."""
    normalized_width = MazeGrid.normalize_dimension(width, padding)
    normalized_height = MazeGrid.normalize_dimension(height, padding)
    grid = MazeGrid(normalized_width, normalized_height, padding)
    rng = random.Random(seed)
    create_algorithm(algorithm).generate(grid, rng)
    make_openings(grid, opening_mode, rng, opening_count, fixed_sides)
    braid(grid, braid_factor, rng)
    return grid
