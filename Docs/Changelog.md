# Changelog

All notable changes to this project. The project started as a single script that generated a random
maze with cubes; this changelog documents the evolution and the reasoning behind the changes.

## Unreleased (2026 rewrite)

### Fixed bugs

* **Rooms could stay uncarved.** The original code picked the start cell with
  `Random.Range(3, size - 3)`, i.e. with an arbitrary parity. If the index was even, one row/column
  of the room lattice could never be reached and stayed a solid wall: the "maze size and initial
  cell" limitation of the original README. Generation now works on the room lattice
  (`padding + even` offsets), so every lane is reachable. Covered by `MazeAlgorithmTests` and
  `verify_maze_reference.py`.
* **Quadratic frontier handling.** `frontierCells.ElementAt(randomIndex)` on a `HashSet` enumerates
  from the start on every pick: O(rooms²) work plus one enumerator allocation per pick. Replaced
  with a `List` and O(1) swap removal (6× to 55× fewer operations, see
  [Performance.md](Performance.md)).
* **`Random.Range(3, width - 3)` could throw or pick an invalid cell** for small mazes
  (the original silently produced mazes without a proper start on tiny grids). Sizes are now clamped
  and validated.
* **Mesh winding** was verified and fixed for `Mesh.triangles` conventions (counter clockwise from
  the front) so the mesh is correct with back face culling in the built-in pipeline, URP and HDRP.
* **Outer walls had no visible faces** in the mesh path (a neighbour outside the grid was treated as
  a wall); the maze was hollow when seen from below or outside.
* **Resource leaks**: the original created one `GameObject` per wall cell with `Instantiate` and never
  destroyed them; regenerating leaked objects, meshes, materials and textures. Everything created at
  runtime is now owned, reused (prefab pool) or destroyed before a new maze is built.

### Added features

* **Four algorithms** (Randomized Prim, Depth First, Randomized Kruskal, Binary Tree) behind
  `IMazeAlgorithm` + `MazeAlgorithmRegistry`.
* **Entrances and exits** (`MazeOpeningCutter`): sealed, random or fixed entrance/exit - the missing
  feature of the original project.
* **Braiding** (`MazeBraider`): remove dead ends and create loops for easier mazes.
* **Deterministic seeds** (`MazeRandom`, xorshift128) - share a number, get the same maze.
* **Mesh rendering** with hidden face culling, floor submesh, 32 bit index support and one draw call.
* **Path finding** (`MazePathfinder`) and a solution path renderer.
* **Minimap** rendered from the grid into a `RenderTexture` on its own layer.
* **First person player** (`MazePlayerController`) with sprint, jump, head bob, respawn and
  `MazeInputGateway` so UI and gameplay never fight over the cursor.
* **Camera rig** with first person, orbit and free fly modes.
* **Runtime HUD** (IMGUI): live statistics, size/algorithm/seed/braid/opening controls, palettes,
  camera switching, legend, minimap and seed announcements.
* **Objective tracking** (`MazeObjectiveTracker`) that turns the maze into a small game.
* **Statistics** (`MazeStatistics`): walls, passages, corridors, dead ends, junctions, depth,
  solution length, perfection and connectivity.
* **Export / import**: ASCII, versioned JSON documents (settings, grid, statistics) with editor
  menu entries and clipboard helpers.
* **Editor tooling**: rewritten inspector (statistics, seed tools, preview generation, PNG/JSON
  export), scene view highlighting, one click demo rig, batch validation.
* **Test suite**: EditMode NUnit tests (5 files, matrix over algorithms/sizes/seeds) plus a Python
  reference implementation with 658 invariant checks.
* **Documentation** (`Docs/`) and generated diagrams.

### Performance

* O(rooms) Prim/DFS/Binary Tree, O(edges log edges) Kruskal, zero allocations in the hot loops.
* One draw call instead of one per wall cell (242 → 1 for a 21×21 maze, 31 752 → 1 for 251×251).
* Roughly 50 % (perfect mazes) to 72 % (loops / thicker walls) fewer triangles than a cube per wall.
* Prefab mode pools wall objects instead of instantiating them per generation.
* Minimap costs one small texture instead of a second scene render.

### Code quality

* Split into focused namespaces and folders (`Core`, `Algorithms`, `Generation`, `Analysis`,
  `Rendering`, `Gameplay`, `Presentation`, `UI`, `Tools`, `EditorTools`).
* Assembly definitions (`Maze.Runtime`, `Maze.Editor`, `Maze.Tests.EditMode`).
* Guard clauses, argument validation, XML documentation on every public member.
* Settings object with clamping/sanitising instead of loose public fields.
* Events (`Generated`, `Cleared`, `On Maze Generated`) instead of polling.

### UI

* The original scene had no UI at all. The demo now ships with a HUD (statistics, authoring panel,
  legend, minimap), a proper first person player, three camera modes and a ground plane.
* Palette system with five presets (Slate, Sandstone, Forest, Neon, Texture), applied at runtime.
* Warnings in the inspector and the HUD when the requested size had to be adjusted.

### Documentation

* `Docs/` with Getting Started, Architecture, Algorithms, Configuration, API, Performance, Testing,
  Troubleshooting, Contributing and this changelog.
* README rewritten: features, quick start, project layout, limitations and acknowledgements.
* Generated diagrams (`Docs/images`) rendered from the algorithms themselves.

### Removed

* Obsolete limitations section (the described limitation is fixed).
* `LICENSE.md` reference in the README (the file is `LICENSE`) and the wrong "MIT" license claim -
  the repository is licensed "All Rights Reserved" (see [../LICENSE](../LICENSE)).
* The URP/HDRP material variants and the `Template/prototype_texture_512x512.psd` template of the
  Gridbox package - the project uses the built-in pipeline and those materials render pink. The
  Standard materials and all textures were kept.
* Unused packages from `Packages/manifest.json`: **Unity Collaborate** (`com.unity.collab-proxy`, the
  service was retired), **Visual Scripting** and **Timeline**. Nothing in the project used them, and
  dropping them removes several packages from the import. Add them back through the Package Manager
  if you need them; `packages-lock.json` is rewritten by Unity on the next open.

### Possible next steps

Ideas that were deliberately left out of this change set, roughly ordered by value:

1. **Multi-threaded / job based generation** - `MazeGeneratorCore` only touches plain arrays, so it can
   run in a `Task` or an `IJob`; only the mesh upload has to happen on the main thread.
2. **Mesh combining for prefab mode** - use `Mesh.CombineMeshes` or `Graphics.DrawMeshInstanced` for
   per-wall objects when individual walls are needed (destructible walls).
3. **Alternative geometries** - hex or triangular grids, or 3D (room/corridor volumes) by extending
   `MazeGrid`/`MazeMeshBuilder`.
4. **Save / load of the full grid** - the JSON exporter already carries the rows; a runtime loader for
   `MazeJsonExport.GridFromRows` would allow storing a solved maze as an asset.
5. **Weights and themed regions** - tag rooms with a region id (treasure, spawn, boss) after
   generation and drive level content from the statistics.
6. **Playable "collect and escape" game mode** - the objective tracker and the minimap already provide
   the pieces (distance, completion count, solution toggle).
7. **URP/HDRP sample scenes** - the material factory detects the pipeline at runtime; a sample scene
   per pipeline would document it.
8. **Burst compiled mesh builder** - `MazeMeshBuilder` is pure array math and a good Burst candidate
   for very large mazes.
