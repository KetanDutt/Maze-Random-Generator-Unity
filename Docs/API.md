# API reference

Namespaces: `Maze` (components), `Maze.Core` (model), `Maze.Algorithms`, `Maze.Generation`,
`Maze.Analysis`, `Maze.Rendering`, `Maze.Gameplay`, `Maze.Presentation`, `Maze.UI`, `Maze.Tools`,
`Maze.EditorTools` (editor only).

## Components

### `MazeGenerator` (`Maze`)

The facade: generates a maze and turns it into scene objects.

| Member | Description |
| --- | --- |
| `MazeGeneratorSettings Settings` | Structure settings; edit and call `Generate()`. |
| `MazeResult Result` | Current maze (mesh, spawn, exit, solution, statistics) or `null`. |
| `bool HasResult` | Convenience flag. |
| `MazeStatistics Statistics` | Statistics of the current maze. |
| `int LastSeed` | Seed of the current maze. |
| `MazeRenderMode RenderMode` | `Mesh` (default) or `Prefabs`. |
| `MazePalette Palette` | Get/set the palette (setting it re-tints the current maze). |
| `GameObject WallPrefab` | Prefab used by prefab mode. |
| `int PooledWallCount` | Number of pooled wall objects (prefab mode). |
| `MazeResult Generate()` | New maze with a fresh or fixed seed (per settings). |
| `MazeResult Generate(int seed)` | New maze with an explicit seed. |
| `MazeResult Regenerate()` | Rebuild the same maze. |
| `MazeResult GenerateWithNewSeed()` | Switch to random seeds and generate. |
| `MazeResult GenerateWithSeed(int seed)` | Switch to a fixed seed and generate. |
| `void Clear()` | Destroy the generated objects and runtime assets. |
| `MazeResult SetAlgorithm(MazeAlgorithmKind)` | Change + regenerate. |
| `MazeResult SetSize(int width, int height)` | Change + regenerate. |
| `MazeResult SetBraidFactor(float)` | Change + regenerate. |
| `void ApplyPalette(MazePalette)` / `RefreshAppearance()` | Re-colour without regenerating. |
| `bool IsWalkable(Vector3 worldPosition)` | Is that world position inside a passage? |
| `Vector2Int WorldToCell(Vector3 worldPosition)` | Grid cell of a world position. |
| `event Action<MazeResult> Generated` | Raised after every generation. |
| `event Action Cleared` | Raised when the maze objects are removed. |
| `UnityEvent On Maze Generated` | Inspector-friendly version of `Generated`. |

### `MazePlayerController` (`Maze.Gameplay`)

First person movement on top of a `CharacterController`. Requires the legacy Input Manager.

| Member | Description |
| --- | --- |
| `bool LookEnabled` / `bool InputEnabled` | Enable/disable look or the whole input (used by the HUD). |
| `Vector3 EyePosition` / `Quaternion LookRotation` | Where the camera has to be (read by `MazeCameraRig`). |
| `float HorizontalSpeed` / `float SpeedFactor` | Current speed and its normalised value. |
| `bool IsGrounded` / `float VerticalVelocity` | Physics state. |
| `void Respawn()` / `void Teleport(Vector3, Quaternion)` | Move the player. |
| `void MoveToSpawn(MazeResult)` | Place the player at the maze entrance. |
| `void SetCursorLocked(bool)` | Control the cursor lock. |

### `MazeCameraRig` (`Maze.Presentation`)

Drives the single camera in three modes.

| Member | Description |
| --- | --- |
| `MazeCameraMode Mode` / `void SetMode(MazeCameraMode)` | `FirstPerson`, `Orbit`, `FreeFly`. |
| `MazeCameraMode CycleMode()` | Next mode. |
| `void FrameMaze(MazeResult)` | Frame the whole maze with the orbit camera. |
| `Camera Camera` | The camera being driven. |

### `MazeMinimap` (`Maze.Presentation`)

Rasterises the grid into a `Texture2D`, renders it with its own camera into a `RenderTexture` on the
`Minimap` layer and draws player/goal markers. Intended to be displayed by `MazeHud`.

| Member | Description |
| --- | --- |
| `RenderTexture Texture` | The rendered minimap (or `null`). |
| `bool RotateWithPlayer` | Rotate the map so the player faces up. |

### `MazeObjectiveTracker` (`Maze.Gameplay`)

Small gameplay layer: reach the exit.

| Member | Description |
| --- | --- |
| `bool HasObjective`, `bool IsReached`, `int CompletedCount` | State. |
| `float Distance`, `float StraightLineDistance`, `int SolutionLength` | Numbers for the HUD. |
| `Vector3 ObjectiveWorldPosition` | Where the exit is. |
| `void SetHintVisible(bool)` / `bool ToggleHint()` | Show/hide the solution path. |
| `event Action Reached`, `event Action<MazeResult> ObjectiveChanged` | Events. |

### `MazePathRenderer` (`Maze.Presentation`)

Draws the solution with a `LineRenderer` plus floor discs at the entrance and the exit.

| Member | Description |
| --- | --- |
| `bool IsVisible` | Current state. |
| `void SetVisible(bool)` | Show/hide path and markers. |

### `MazeHud` (`Maze.UI`)

IMGUI panels: statistics, authoring (size, algorithm, seed, braiding, openings, palette, camera) and
a legend, plus the minimap and a seed announcement. Press `F1` to toggle the panels.

| Member | Description |
| --- | --- |
| `void SetVisible(bool)` | Hide/show all panels. |
| `void SetMinimapVisible(bool)` | Hide/show the minimap. |

## Core model (`Maze.Core`)

### `MazeGrid`

| Member | Description |
| --- | --- |
| `MazeGrid(int width, int height, int padding = 1)` | Validates odd interior dimensions. |
| `int Width`, `int Height`, `int Padding` | Geometry. |
| `int RoomCountX`, `RoomCountY`, `RoomCount` | Room lattice size. |
| `int WallCount`, `int PassageCount` | Cell counts. |
| `IReadOnlyList<MazeOpening> Openings` | Registered openings. |
| `bool IsWall(int x, int y)` / `IsPassage(...)` | Cell state (out of bounds counts as wall). |
| `bool InBounds(...)`, `bool IsRoomCell(...)`, `bool InRoomBounds(...)` | Bounds helpers. |
| `Vector2Int CellOfRoom(int roomX, int roomY)` / `RoomOfCell(int x, int y)` | Lattice conversion. |
| `void Carve(int x, int y)`, `SetWall(...)`, `CarveRoom(...)` | Mutation. |
| `void CarveCorridor(int roomX, int roomY, MazeDirection)` | Connect a room to its neighbour (carves both rooms plus the corridor). |
| `bool IsCorridorCarved(...)`, `int RoomDegree(...)` | Connectivity queries. |
| `int CollectRoomNeighbours(int roomX, int roomY, Vector2Int[] buffer)` | Allocation free neighbour lookup. |
| `IEnumerable<Vector2Int> EnumeratePassages()` / `EnumerateRooms()` | Iteration. |
| `ulong ComputeFingerprint()` | Stable 64 bit identity of the grid. |
| `string ToAscii(char wall = '#', char passage = '.')` | ASCII rendering. |
| `MazeGrid Clone()` | Deep copy. |
| `static int NormalizeDimension(int requested, int padding)` | Clamp to a valid odd size. |
| `const int MaximumDimension`, `const int MinimumInteriorSize` | 501 / 3. |

### `MazeRandom`

| Member | Description |
| --- | --- |
| `MazeRandom(int seed)` | Seeded xorshift128. |
| `uint NextUInt()`, `int NextInt()`, `float NextFloat()` | Values. |
| `int Range(int minInclusive, int maxExclusive)` | Bounded integer. |
| `int NextIndex(int count)` | `0 .. count-1` (safe for 0 and 1). |
| `bool Chance(float probability)` | Probability check. |
| `void Shuffle<T>(IList<T>)` | Fisher-Yates. |
| `static int CreateSeed()` | Fresh seed from time + tick count. |

### `MazeGeneratorSettings`

Serialisable structure settings (see [Configuration.md](Configuration.md)) with
`Sanitize()`, `ResolveSeed()`, `Describe()` and computed `Width`, `Height`, `RoomCountX`,
`RoomCountY`.

### `MazeDirection` / `MazeDirections`

Enum plus `Opposite()`, `ToOffset()`, `ToDisplayName()`, `TryParse(string, out ...)`,
`FromOffset(int dx, int dy)` and `Values`.

## Generation (`Maze.Generation`)

| Type | Description |
| --- | --- |
| `MazeGeneratorCore` | `Generate(settings)` / `Generate(settings, seed)` → `MazeGenerationOutput`. |
| `MazeGenerationOutput` | `Grid`, `Seed`, `AlgorithmId`, `Milliseconds`, `SizeWasAdjusted`. |
| `MazeBaker` | `Bake(grid, settings, algorithmId, seed, ms, includeFloor, center)` → `MazeResult`; `ComputeOrigin`. |
| `MazeResult` | `Grid`, `Statistics`, `MeshData`, `Origin`, `CellSize`, `WallHeight`, `SpawnPosition`, `SpawnRotation`, `ExitPosition`, `SolutionPath`, `HasOpenings`, `HasSolution`, `CellToWorld`, `WorldToCell`, `WorldSize`, `WorldCenter`. |
| `MazeStatistics` | `WallCount`, `PassageCount`, `CorridorCount`, `DeadEndCount`, `JunctionCount`, `OpeningCount`, `SolutionPathLength`, `Depth`, `IsPerfect`, `IsFullyConnected`, `ToSummary()`. |
| `MazeOpeningCutter` | `Cut(grid, settings, random)` → `List<MazeOpening>`. |
| `MazeBraider` | `Braid(grid, factor, random)` → number of opened corridors. |

## Algorithms (`Maze.Algorithms`)

| Type | Description |
| --- | --- |
| `IMazeAlgorithm` | `Id`, `DisplayName`, `Description`, `MinimumRoomCount`, `Generate(grid, random)`. |
| `MazeAlgorithmRegistry` | `All`, `Count`, `Get(kind)`, `GetDisplayName(kind)`, `GetDescription(kind)`, `TryParse(id, out kind)`, `GetDisplayNames()`, `GetIds()`, `GetIdList()`. |
| `RandomizedPrimAlgorithm`, `DepthFirstAlgorithm`, `RandomizedKruskalAlgorithm`, `BinaryTreeAlgorithm` | The four implementations. |

## Analysis (`Maze.Analysis`)

| Member | Description |
| --- | --- |
| `List<Vector2Int> FindPath(grid, start, goal)` | BFS shortest path (or `null`). |
| `int MeasureDepth(grid, start, out Vector2Int farthest)` | Distance to the farthest reachable cell. |
| `int CountReachable(grid, start)` | Reachable passage cells. |
| `bool IsFullyConnected(grid)` | Every passage reachable from every other. |
| `int CountDeadEnds(grid)` / `CountJunctions(grid)` | Room degree statistics. |
| `Vector2Int FindFirstPassage(grid)` | First passage in scan order. |

## Rendering (`Maze.Rendering`)

| Type | Description |
| --- | --- |
| `MazeMeshBuilder.Build(grid, cellSize, wallHeight, includeFloor, origin)` | → `MazeMeshData` (submesh 0 = walls, submesh 1 = floor). |
| `MazeMeshBuilder.EstimateTriangleCount(grid, includeFloor)` | Budget estimate before building. |
| `MazeMeshData` | `Mesh`, `WallQuadCount`, `FloorQuadCount`, `VertexCount`, `TriangleCount`, `Bounds`, `HasFloor`. |
| `MazeMaterialFactory` | `FindBestShader()`, `Create(name, color, texture, smoothness, metallic)`, `CreateUnlit(...)`, `CreatePathMaterial(...)`, `CreateSolidTexture(...)`, `SetColor/SetTexture` (pipeline agnostic). |
| `MazePalette` / `MazePalettes` | Presets, `Get`, `GetName`, `TryParse`, `GetNames`. |

## Tools (`Maze.Tools`)

| Member | Description |
| --- | --- |
| `MazeAsciiExporter.ToAscii(grid)` / `ToAscii(grid, path)` | Walls `#`, passages `.`, path `o` with `S`/`E`. |
| `MazeJsonExport.ToJson(result, settings)` | Versioned document: identity, statistics, ASCII, rows, settings. |
| `MazeJsonExport.SettingsToJson(settings)` / `TryParseSettings(json, out settings)` | Settings round trip. |
| `MazeJsonExport.TryParseIdentity(json, out seed, out algorithm, out width, out height)` | Read the identity only. |
| `MazeJsonExport.GridFromRows(rows, padding)` | Rebuild a grid from exported rows. |

## Editor (`Maze.EditorTools`, editor only)

| Entry | Description |
| --- | --- |
| `MazeGeneratorEditor` | Inspector: Generate / Regenerate / Clear / Random seed / Copy seed / Copy ASCII / Export JSON / Export PNG, statistics foldout, seed tools, scene view highlighting (right click a cell to highlight its room). |
| `MazeMenuItems` | `GameObject → Maze → Maze Generator`, `Create Demo Rig`, `Generate/Regenerate/Clear Selected`, `Export Selected Maze (JSON)...`, `Import Maze (JSON)...`, `Validate Generator (all algorithms)`. |
| `MazeSceneBuilder` | `BuildDemoRig()` creates the complete demo rig. |

## Examples

### React to the maze and place an object at the exit

```csharp
using Maze;
using Maze.Generation;
using UnityEngine;

public sealed class ExitMarker : MonoBehaviour
{
    [SerializeField] private MazeGenerator _generator;
    [SerializeField] private GameObject _marker;

    private void OnEnable()  => _generator.Generated += OnGenerated;
    private void OnDisable() => _generator.Generated -= OnGenerated;

    private void OnGenerated(MazeResult result)
    {
        _marker.transform.position = _generator.transform.TransformPoint(result.ExitPosition);
    }
}
```

### Ask the maze for a route between two world positions

```csharp
using Maze.Analysis;
using System.Collections.Generic;
using UnityEngine;

public static class Route
{
    public static List<Vector3> Find(MazeGenerator generator, Vector3 from, Vector3 to)
    {
        var result = generator.Result;
        var a = result.WorldToCell(generator.transform.InverseTransformPoint(from));
        var b = result.WorldToCell(generator.transform.InverseTransformPoint(to));
        var cells = MazePathfinder.FindPath(result.Grid, a, b);

        var points = new List<Vector3>();
        if (cells == null) return points;
        foreach (var cell in cells) points.Add(generator.transform.TransformPoint(result.CellToWorld(cell)));
        return points;
    }
}
```

### Use the core without Unity objects

```csharp
using Maze.Core;
using Maze.Generation;

MazeGeneratorCore core = new MazeGeneratorCore();
MazeGenerationOutput output = core.Generate(new MazeGeneratorSettings { MazeWidth = 51, MazeHeight = 51 }, 7);
string ascii = output.Grid.ToAscii();
```
