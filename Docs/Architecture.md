# Architecture

## Design principles

1. **The maze is data, not scene objects.** Generation produces a `MazeGrid` - a plain `bool[]` with
   geometry helpers. It can be tested, serialised, hashed and analysed without a scene.
2. **Algorithms are interchangeable.** Every algorithm implements `IMazeAlgorithm` and is registered
   in `MazeAlgorithmRegistry`. The UI, the inspector and the exporters all read from the registry.
3. **Deterministic by construction.** All randomness comes from `MazeRandom`, an explicitly seeded
   xorshift128 generator with a platform independent output sequence. `seed → maze` always holds.
4. **One mesh, not one object per wall.** `MazeMeshBuilder` bakes the grid into a single mesh with
   hidden faces culled, which keeps the draw call count at one and the triangle count low.
5. **Everything created at runtime is owned and disposed.** Regenerating in a loop does not leak
   meshes, materials or textures (see `MazeGenerator.DisposeCurrentResult`).

## Folder layout

```
Assets/Code/
├── Scripts/                       Maze.Runtime (asmdef)
│   ├── MazeGenerator.cs           Facade component: generate + apply to the scene
│   ├── Core/                      Engine independent model
│   │   ├── MazeGrid.cs            bool[] grid, room lattice, corridors, openings, fingerprint
│   │   ├── MazeGeneratorSettings.cs  Serialisable settings object (+ enums, sanitising)
│   │   ├── MazeRandom.cs          Seeded xorshift128 randomness, shuffles, 64 bit seeds
│   │   └── MazeDirection.cs       Cardinal direction helpers
│   ├── Algorithms/                One file per algorithm + registry
│   ├── Generation/                Pipeline between model and scene
│   │   ├── MazeGeneratorCore.cs   validate → carve → openings → braid, returns grid + stats
│   │   ├── MazeOpeningCutter.cs   Entrances and exits
│   │   ├── MazeBraider.cs         Loop creation / dead end removal
│   │   ├── MazeBaker.cs           Grid → mesh + spawn/exit/solution
│   │   ├── MazeResult.cs          Immutable result bundle handed to the scene
│   │   └── MazeStatistics.cs      Structural facts about a maze
│   ├── Analysis/MazePathfinder.cs BFS path finding, depth, connectivity, dead ends, junctions
│   ├── Rendering/                 Mesh building and materials
│   │   ├── MazeMeshBuilder.cs     Culled wall + floor mesh, 16/32 bit index buffers
│   │   ├── MazePalette.cs         Colour presets
│   │   └── MazeMaterialFactory.cs Pipeline agnostic material creation
│   ├── Gameplay/                  Player, objective, input coordination
│   ├── Presentation/              Camera rig, minimap, solution path renderer
│   └── UI/MazeHud.cs              Runtime IMGUI panels (stats, authoring, legend, minimap)
├── Editor/                        Maze.Editor (asmdef, editor only)
│   ├── MazeGeneratorEditor.cs     Inspector, scene view helpers, seed tools, export
│   ├── MazeMenuItems.cs           GameObject/Tools menu entries, import/export, validation
│   └── MazeSceneBuilder.cs        One click demo rig
└── Tests/EditMode/                Maze.Tests.EditMode (asmdef) - NUnit suite
Tools/Reference/                   Python reference implementation, verifier, benchmark, docs images
Docs/                              Documentation (this folder)
```

## The generation pipeline

```
MazeGeneratorSettings ──► MazeGeneratorCore.Generate(seed)
                             │  sanitise (odd sizes, clamps)
                             │  resolve seed
                             ├─► IMazeAlgorithm.Generate(grid, random)     carve rooms + corridors
                             ├─► MazeOpeningCutter.Cut(grid, settings)     entrance / exit
                             └─► MazeBraider.Braid(grid, factor)           remove dead ends
                                        │
                                        ▼
                                   MazeGrid (pure data)
                                        │
                             MazeBaker.Bake(grid, settings, ...)
                                        │  MazeMeshBuilder.Build(...)      walls + floor mesh
                                        │  MazePathfinder.FindPath(...)    spawn → exit route
                                        ▼
                                   MazeResult  ──► MazeGenerator applies it to the scene
```

`MazeGenerator` is only responsible for the last step: it owns the scene objects (mesh filter,
renderer, collider, prefab pool), applies materials/palettes and raises events. Everything above it
is reusable without a scene, which is what the EditMode tests exploit.

## Room lattice

The maze works on a lattice of *rooms* that are two cells apart. The cell in between is the
corridor that connects two rooms:

```
padding 1, 7x7 grid             rooms:   (0,0) (1,0) (2,0)
+-------+                       cells:   (1,1) (3,1) (5,1)
| # # # |
| # . # |   room cell           corridor cell between (0,0) and (1,0): (2,1)
| # . # |
| # # # |
+-------+
```

Consequences:

* Every room is reachable by construction - the "stranded wall row/column" of the original
  implementation cannot happen, because all lanes of the lattice are equivalent.
* Odd interior dimensions are required; `MazeGrid.NormalizeDimension` enforces them.
* `Padding` adds extra forced-wall rings around the lattice (useful for a thicker outer wall).

## Extension points

| What you want to change | Where |
| --- | --- |
| Add/replace an algorithm | Implement `IMazeAlgorithm`, add an enum value in `MazeAlgorithmKind`, register it in `MazeAlgorithmRegistry` |
| Different geometry (hex, 3D, thicker walls) | Replace `MazeMeshBuilder.Build` (keep the winding rule documented there) |
| Different look | Add a `MazePalette` value + `MazePalettes` entry, or assign `Wall Material` / `Floor Material` overrides |
| Add a HUD panel | Extend `MazeHud.DrawAuthoring` (IMGUI) or build a UGUI canvas and drive it from `MazeGenerator.Generated` |
| React to generation | `MazeGenerator.Generated`, `Cleared` and `On Maze Generated` (UnityEvent) |
| Post-process mazes | Call `MazeBraider.Braid` the way `MazeGeneratorCore` does, or write your own pass over the `MazeGrid` |

## Assembly definitions

| Assembly | Contents | References |
| --- | --- | --- |
| `Maze.Runtime` | `Assets/Code/Scripts` | (none beyond the engine) |
| `Maze.Editor` | `Assets/Code/Editor` | `Maze.Runtime` |
| `Maze.Tests.EditMode` | `Assets/Code/Tests/EditMode` | `Maze.Runtime`, Test Runner, NUnit |

`Maze.Runtime` is auto-referenced, so scripts in `Assembly-CSharp` can use the `Maze.*` namespaces
without any additional setup. Splitting the assemblies keeps the editor code and the tests out of
player builds and makes the compile times shorter.
