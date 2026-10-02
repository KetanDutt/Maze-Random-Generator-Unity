# Configuration

Everything structural lives in `MazeGeneratorSettings`, a `[Serializable]` class that is edited in
the inspector (`Maze Generator → Maze Settings`) or from script through `MazeGenerator.Settings`.
Values are clamped by `Sanitize()`, which runs in `OnValidate` and before every generation, so an
invalid value can never reach the algorithms.

## Settings reference

| Property | Inspector label | Default | Range | Effect |
| --- | --- | --- | --- | --- |
| `MazeWidth` | Maze Width | 21 | 5 - 501 (odd) | Grid width in cells. |
| `MazeHeight` | Maze Height | 21 | 5 - 501 (odd) | Grid height in cells. |
| `Algorithm` | Algorithm | Randomized Prim | enum | Which generator carves the maze. |
| `UseRandomSeed` | Use Random Seed | true | bool | Draw a fresh seed for every generation. |
| `Seed` | Seed | 12345 | int | Used when `Use Random Seed` is off. Share it to share the maze. |
| `BraidFactor` | Braid Factor | 0.18 | 0 - 1 | Fraction of dead ends that get an extra corridor (loops). |
| `OpeningMode` | Opening Mode | FixedEntranceAndExit | enum | `None`, `Random` or `FixedEntranceAndExit`. |
| `RandomOpeningCount` | Random Opening Count | 2 | 0 - 4 | Number of openings in `Random` mode. |
| `EntranceSide` | Entrance Side | North | enum | Side of the fixed entrance. |
| `ExitSide` | Exit Side | South | enum | Side of the fixed exit (forced to differ from the entrance). |
| `Padding` | Padding | 1 | 1 - 4 | Thickness of the forced wall ring around the room lattice. |
| `CellSize` | Cell Size | 5 | 1 - 50 | World size of one cell (the wall prefab is scaled to it). |
| `WallHeight` | Wall Height | 5 | 1 - 50 | World height of the generated walls. |
| `GenerateFloor` | Generate Floor | true | bool | Adds a floor submesh under every passage cell (mesh mode). |

### Size

The interior (size minus `2 × Padding`) must be odd for the room lattice to fit. Even numbers are
rounded up to the next odd value, tiny values are raised to the minimum, and huge values are clamped
to `MazeGrid.MaximumDimension` (501). `MazeGenerationOutput.SizeWasAdjusted` and the inspector warn
you when this happens; the HUD announces the corrected size.

| Requested | Result |
| --- | --- |
| 20 × 20 | 21 × 21 |
| 3 × 3 | 5 × 5 |
| 1000 × 1000 | 501 × 501 |
| 30 × 30 with padding 2 | 31 × 31 |

Rough world coordinates: `width × CellSize` units. A 101×101 maze with `CellSize = 5` is 505 × 505
units.

### Seed

* **Random seed (default)** - `MazeRandom.CreateSeed()` combines `DateTime.UtcNow.Ticks` and
  `Environment.TickCount`; every generation draws a new one.
* **Fixed seed** - the same seed, algorithm, size, braid factor and opening settings always produce
  exactly the same maze, including on a different machine, because the generator never uses
  `UnityEngine.Random` or `System.Random`.
* `MazeGrid.ComputeFingerprint()` (FNV-1a, 64 bit) is a stable identity for a generated maze and is
  shown in the inspector; it is handy for regression tests and bug reports.

### Braid factor

| Value | Result |
| --- | --- |
| `0` | Perfect maze: exactly one path between any two cells. |
| `0.1 - 0.3` | A few loops - pleasant to explore, still challenging. |
| `0.5` | Half of the dead ends are removed. |
| `1` | Every dead end is connected to a second neighbour: no dead ends, lots of loops. |

Room degree statistics are shown in the HUD ("Dead ends", "Junctions") and in the inspector.

### Openings

| Mode | Description |
| --- | --- |
| `None` | Sealed outer wall. The spawn point becomes the first passage cell, the exit the room farthest away. |
| `Random` | `RandomOpeningCount` openings, each on a random side at a random position, at most one per side. |
| `FixedEntranceAndExit` | Entrance and exit in the middle of `EntranceSide` / `ExitSide`. |

The entrance is the spawn point of the player and the beginning of the solution path; the exit is the
objective of `MazeObjectiveTracker`.

### Presentation settings on the component

| Property | Default | Effect |
| --- | --- | --- |
| `RenderMode` | `Mesh` | `Mesh` = one GameObject with a MeshFilter/MeshRenderer/MeshCollider; `Prefabs` = one instance of `Wall Prefab` per wall cell (pooled and reused). |
| `WallPrefab` | `Assets/Prefabs/Wall.prefab` | Used by prefab mode and as a fallback. |
| `WallMaterial` / `FloorMaterial` | none | Overrides for the palette materials. |
| `Palette` | Slate | Colour preset: Slate, Sandstone, Forest, Neon, Texture. |
| `ReferencePrefabCellSize` | 5 | Cell size the wall prefab was modelled for; instances are scaled by `CellSize / ReferencePrefabCellSize`. |
| `GenerateOnStart` | true | Generate in `Start()`. |
| `CenterOnOrigin` | true | Centre the maze on the transform (instead of growing into +X/+Z). |
| `AutoRegenerateSeconds` | 0 | Attract mode: regenerate every N seconds (0 = off). |
| `LogStatistics` | false | Log a statistics block per generation. |

## Palettes

| Palette | Walls | Floor | Path | Notes |
| --- | --- | --- | --- | --- |
| `Slate` | blue-grey | light grey | magenta | Default, closest to the original demo. |
| `Sandstone` | sand | cream | dark red | Warm, easy on the eyes. |
| `Forest` | green | pale green | amber | Outdoors look. |
| `Neon` | near black | dark blue | glowing cyan | Emissive path, good for dark first person levels. |
| `Texture` | prefab texture | grey | red | Keeps the third party Gridbox texture instead of tinting. |

## Sharing a configuration

* **HUD**: "Copy" copies the current seed; the seed field applies any seed and "Regenerate" rebuilds
  the same maze.
* **Inspector**: `Copy seed`, `Export JSON...` (settings + grid + statistics).
* **Tools → Maze → Import Maze (JSON)...** applies a document to the selected generator.
* From script: `MazeJsonExport.SettingsToJson(settings)` /
  `MazeJsonExport.TryParseSettings(json, out settings)`.

A minimal shared seed document looks like this:

```json
{
  "version": 1,
  "seed": 4242,
  "algorithm": "dfs",
  "width": 41,
  "height": 41,
  "solutionLength": 214,
  "perfect": true,
  "fullyConnected": true,
  "rows": ["###...", "..."]
}
```
