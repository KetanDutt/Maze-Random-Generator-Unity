# Performance

Two things dominate the cost of a maze generator: the algorithm that carves the grid and the way
the result is handed to the renderer. Both were rewritten in this project.

## 1. Carving the grid

### The frontier fix (Randomized Prim)

The original code picked a random frontier cell with `frontier.ElementAt(randomIndex)` on a
`HashSet`. `ElementAt` enumerates the set from its beginning on every call, so a pick costs
O(frontier) instead of O(1) - and allocates an enumerator each time:

| Maze | `HashSet` + `ElementAt` (ops) | `List` + swap remove (ops) | Speed-up |
| --- | --- | --- | --- |
| 21×21 | 1 218 | 220 | 6× |
| 51×51 | 16 484 | 1 300 | 13× |
| 101×101 | 136 619 | 5 100 | 27× |
| 201×201 | 1 111 330 | 20 200 | 55× |

The frontier now uses O(1) swap-removal (`frontier[i] = frontier[last]; RemoveAt(last)`), which makes
the algorithm linear in the number of rooms with **zero allocations** inside the loop. The same
pattern (pre-allocated `Vector2Int[4]` neighbour buffers, no LINQ, no tuples in the hot path) is used
by all four algorithms.

### Complexity

| Algorithm | Time | Extra memory |
| --- | --- | --- |
| Randomized Prim | O(rooms) | `bool[rooms]` + frontier `List` |
| Depth First | O(rooms) | `bool[rooms]` + stack (≤ rooms) |
| Randomized Kruskal | O(edges log edges) | union-find arrays + `Edge[edges]` |
| Binary Tree | O(rooms) | none |
| Openings | O(cells) | - |
| Braiding | O(rooms) | `Vector2Int[rooms]` |
| Path finding | O(cells) | `int[cells]` + queue |
| Mesh building | O(cells) | vertex/triangle lists |

Indicative timings from the reference implementation (`Tools/Reference/benchmark_reference.py`,
CPython 3.11 on a 2026 laptop - use them for *scaling*, not as absolute Unity numbers; the C# code is
an order of magnitude faster):

| Maze | Prim | DFS | Kruskal | Binary Tree |
| --- | --- | --- | --- | --- |
| 21×21 | 0.7 ms | 0.6 ms | 0.3 ms | 0.2 ms |
| 101×101 | 17 ms | 14 ms | 7 ms | 5 ms |
| 251×251 | 108 ms | 97 ms | 62 ms | 31 ms |
| 501×501 | 502 ms | 426 ms | 300 ms | 134 ms |

In Unity, generating a 101×101 maze (5 202 wall cells) including the mesh, the solution path and the
statistics typically takes a few milliseconds - a regeneration per frame stays possible, a
regeneration per key press is unnoticeable. The HUD prints the measured time of the last generation
("Generation" row).

## 2. Bringing the maze into the scene

### Draw calls

| Mode | Objects per maze | Draw calls | Colliders |
| --- | --- | --- | --- |
| `Prefabs` (original approach) | one per wall cell | one per wall cell (242 for 21×21, 31 752 for 251×251) | one per wall cell |
| `Mesh` (default) | 1 | 1 (2 submeshes: walls + floor) | 1 mesh collider |

### Triangles

Hidden faces are never emitted: a wall face is only created when the neighbouring cell is a passage
or outside the grid, and every wall cell emits exactly one top face.

| Maze | Walls | Mesh quads | Mesh triangles | Cube per wall (triangles) | Saved |
| --- | --- | --- | --- | --- | --- |
| 21×21 perfect | 242 | 726 | 1 452 | 2 904 | 50 % |
| 51×51 perfect | 1 352 | 4 056 | 8 112 | 16 224 | 50 % |
| 101×101 perfect | 5 202 | 15 606 | 31 212 | 62 424 | 50 % |
| 21×21 braid 1.0 | 216 | 700 | 1 400 | 2 592 | 46 % |
| 21×21 with padding 2 | 360 | 604 | 1 208 | 4 320 | 72 % |

In a perfect maze most wall cells are isolated, so the saving is a factor of two; mazes with loops or
thicker walls (where wall cells touch each other) save 45-72 %. The algorithm is exact and cheap:
`MazeMeshBuilder.EstimateTriangleCount` gives you the number before building.

### Other rendering notes

* **Index format** - `Mesh.indexFormat` switches to `UInt32` above 65 000 vertices, which is required
  for mazes larger than roughly 120×120 cells. Without it Unity would silently truncate the indices.
* **Mesh collider** - mesh mode adds a single `MeshCollider` with `EnableMeshCleaning` and
  `WeldColocatedVertices`; cooking a 101×101 mesh takes a few milliseconds. Disable the collider if
  your game does not need physics.
* **Prefab pooling** - prefab mode keeps a pool of wall objects and reuses them between generations,
  so neither `Instantiate` nor `Destroy` runs per cell.
* **Minimap** - the minimap rasterises the grid into a `Texture2D` (a few thousand pixels) and renders
  it with its own camera on its own layer. Rendering the maze a second time would cost a full pass
  over the scene; this costs one small texture upload per generation.
* **Regenerating** - the previous mesh, materials and minimap texture are destroyed before new ones
  are created (`MazeGenerator.DisposeCurrentResult`), so a regeneration loop does not leak memory.

### Memory

| Item | Size (101×101 maze) |
| --- | --- |
| `MazeGrid` (`bool[]`) | 10 201 bytes + overhead |
| Mesh | 62 424 vertices × 12 B (position) ≈ 750 KB + 62 424 indices × 4 B ≈ 250 KB |
| Minimap texture | 404 × 404 px × 4 B ≈ 650 KB (only when the minimap is present) |

## 3. Practical guidance

| Maze size | Use case | Notes |
| --- | --- | --- |
| 5 - 21 | Puzzle levels, tutorials | Instant, tiny meshes. |
| 21 - 101 | First person exploration | Recommended range; a few milliseconds per generation. |
| 101 - 251 | Overview, strategy, minimaps | Mesh mode is essential; prefab mode becomes slow. |
| 251 - 501 | Tech demos, screensavers | Use `Binary Tree` or `Kruskal`; `UInt32` index buffers kick in. |

Practical tips:

* Keep `Render Mode = Mesh` unless you need per-wall objects (for example for destructible walls).
* Prefer `BinaryTree`/`Kruskal` for very large mazes, `Prim`/`DFS` for gameplay mazes.
* Braiding beyond 0.5 mostly adds corridors without changing the difficulty - and it reduces the wall
  count, which makes the mesh smaller.
* Use `MazeMeshBuilder.EstimateTriangleCount` (or the inspector statistics) to check the budget
  before generating a huge maze at runtime.
* Generation happens on the main thread. For mazes above ~300×300, generate the `MazeGrid` with
  `MazeGeneratorCore` inside a `Task`/job (the model is engine independent and allocates only plain
  arrays) and apply the mesh on the main thread.

## 4. Reproducing the numbers

```bash
python3 Tools/Reference/verify_maze_reference.py     # invariants, mesh budget
python3 Tools/Reference/benchmark_reference.py       # frontier, timings, mesh tables
```

Windows → **Tools → Maze → Validate Generator (all algorithms)** runs the same validation inside
Unity, and the EditMode test suite covers the invariants on every algorithm.
