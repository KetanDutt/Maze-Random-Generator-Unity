# Contributing

Thanks for improving the project. This page documents the conventions, the checks to run and the
typical extension workflows.

## Before you open a pull request

```bash
# 1. Algorithm invariants (fast, no Unity required)
python3 Tools/Reference/verify_maze_reference.py

# 2. Optional: C# syntax check without Unity (Node.js 18+)
node Tools/Reference/lint_csharp.mjs Assets/Code

# 3. Unity: Window -> General -> Test Runner -> EditMode -> Run All
# 4. Unity: Tools -> Maze -> Validate Generator (all algorithms)
```

All four must pass. The CI workflow runs steps 1 and 2 automatically.

## Coding style

* C# 8 compatible, Unity 2021.3 APIs only (`UnityEngine.Random` and `System.Random` are banned from
  the generator - use `MazeRandom` so results stay reproducible).
* One class per file, file name = class name; public members have XML documentation.
* `private` fields use `_camelCase`, properties and methods `PascalCase`, constants `PascalCase`.
* No LINQ and no allocations in hot loops (algorithms run per cell); pre-allocate buffers.
* No `GetComponent`/`FindObjectOfType` inside update loops; resolve references in `Awake`.
* Anything created at runtime (meshes, materials, textures) must be destroyed by its owner; use
  `Destroy` in play mode and `DestroyImmediate` in edit mode.
* Keep the runtime code free of editor APIs. Anything that needs `UnityEditor` belongs in
  `Assets/Code/Editor`.

## Adding an algorithm

1. Create `Assets/Code/Scripts/Algorithms/MyAlgorithm.cs` implementing `IMazeAlgorithm`:

   ```csharp
   public sealed class MyAlgorithm : IMazeAlgorithm
   {
       public string Id => "my-algorithm";
       public string DisplayName => "My Algorithm";
       public string Description => "One line about the look of the result.";
       public int MinimumRoomCount => 1;

       public void Generate(MazeGrid grid, MazeRandom random)
       {
           // Only ever use grid.CarveCorridor(...) / CarveRoom(...) and `random`.
           // Leave the grid fully connected and every room carved.
       }
   }
   ```

2. Add the enum value to `MazeAlgorithmKind` **at the end** (the enum index is the registry index).
3. Register it in `MazeAlgorithmRegistry.Algorithms` in the same order.
4. Add it to the Python reference (`Tools/Reference/maze_reference.py`: `ALGORITHMS`,
   `create_algorithm`) so the verifier covers it.
5. Run the verifier and the EditMode tests. The algorithm matrix in `MazeAlgorithmTests` picks up
   enum values automatically.
6. Regenerate the documentation images (`python3 Tools/Reference/generate_docs_images.py`) and add
   the algorithm to `Docs/Algorithms.md`.

## Adding a palette

1. Add an enum value to `MazePalette` and a matching `MazePaletteDefinition` to
   `MazePalettes.Definitions` (same index) plus a name in `MazePalettes.Names`.
2. The HUD, the inspector and the solution path pick it up automatically.

## Changing settings

Settings live in `MazeGeneratorSettings`. When you add a field:

* mark it `[SerializeField]` with a `[Tooltip]`, add a public property with clamping, and handle it
  in `Sanitize()`;
* add it to the copy constructor;
* export it in `MazeJsonExport` (both directions);
* document it in `Docs/Configuration.md`.

## Documentation

* `README.md` - overview, quick start, features, project layout.
* `Docs/*.md` - everything detailed; keep the tables in sync with the code.
* Images are generated, never hand-drawn: `Docs/images/*.png` come from
  `Tools/Reference/generate_docs_images.py`.

## Commit / PR checklist

- [ ] Verifier passes, EditMode tests pass, `Validate Generator` reports no problems.
- [ ] New public API is documented in the code and in `Docs/API.md`.
- [ ] Behaviour changes are described in `Docs/Changelog.md`.
- [ ] No new files outside `Assets/`, `Tools/`, `Docs/` unless they are project files.
- [ ] Unity `.meta` files are committed for every new asset.
