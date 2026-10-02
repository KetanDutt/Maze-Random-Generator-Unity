# Troubleshooting

## Pink / magenta maze

The materials of the generated maze use a shader that is not available in the active render
pipeline. `MazeMaterialFactory.FindBestShader()` tries `Universal Render Pipeline/Lit`, `HDRP/Lit`,
`Standard` and a few fallbacks - in a URP project the URP Lit shader is found automatically, but in a
*stripped* build the shader can be missing from the build.

Fixes:

1. Add the shader to **Project Settings → Graphics → Always Included Shaders** (for example
   `Universal Render Pipeline/Lit`).
2. Or assign an explicit material: `Maze Generator → Wall Material` / `Floor Material`.

Note: the third party folder only ships **Standard** (built-in pipeline) materials; the URP/HDRP
variants of the Gridbox package were removed because they render pink in this project. If you switch
the project to URP or HDRP, create your own materials there instead.

## Nothing is generated

| Symptom | Cause | Fix |
| --- | --- | --- |
| No maze after Play | `Generate On Start` disabled | Enable it or call `Generate()` yourself. |
| Nothing in edit mode | Generation does not run automatically outside play mode | Press **Generate** in the inspector. |
| Only the HUD, no maze | The generated objects are children of the generator; the camera is elsewhere | Press `2` (orbit camera) or **Frame Maze** via `MazeCameraRig.FrameMaze`. |
| Warning "Prefab mode needs a wall prefab" | `Render Mode = Prefabs` without a `Wall Prefab` | Assign `Assets/Prefabs/Wall.prefab` or switch to mesh mode. |

## The maze is regenerated on every frame

`Auto Regenerate Seconds` is set to a small value, or another script calls `Generate()` in
`Update()`. Set it to 0 to disable the attract mode. Note that `Generate()` rebuilds the mesh and
re-cooks the collider, so it is not free.

## The player falls through the floor / cannot move

* The player needs a `CharacterController` (`MazePlayerController` requires it).
* The maze mesh collider is created in mesh mode. If you disabled it, add a floor collider (the demo
  scene has a ground plane).
* Falling below `Fall Limit` (-25 by default) teleports the player back to the spawn point - lower
  the limit for very large mazes.
* If the player does not move, check that the legacy Input Manager is active:
  **Project Settings → Player → Active Input Handling** must be *Input Manager (Old)* or *Both*.
  With *Input System Package (New)* only, `Input.GetAxis` throws and nothing moves.

## The mouse cursor is not locked / the camera does not turn

* Click once with the left mouse button in the game view (that is the standard lock gesture,
  `F1` panels do not steal it).
* While the pointer is over the authoring panel the player input is intentionally suspended
  (`MazeInputGateway`), so moving the mouse over the panel never rotates the camera.
* In orbit mode the left mouse button drags the camera instead; press `1` for first person.

## The minimap is black or empty

* The `Minimap` layer must exist. The project creates it in
  **Project Settings → Tags and Layers** (layer 31). If it is missing, `MazeMinimap` falls back to
  layer 31 and logs a warning - make sure layer 31 is unused in that case.
* The minimap only draws after a generation (`MazeGenerator.Generated`).
* `M` toggles the minimap; if the HUD minimap is hidden the render texture still exists.

## "Size adjusted to 21 x 21" although 20 was requested

The room lattice requires an odd interior size; even values are rounded up. This is reported by
`MazeGenerationOutput.SizeWasAdjusted`, in the inspector, in the HUD and in the console.

## The maze is not fully connected / a room is missing

That would be a bug - the generator cannot produce it (tests cover all algorithms, sizes, seeds,
braid factors and opening modes). If you see the warning
"The generated maze is not fully connected (seed ...)", please report the seed, size, algorithm and
braid factor: the same seed reproduces the exact same maze everywhere.

## Tests do not appear in the Test Runner

The test assembly needs `UNITY_INCLUDE_TESTS` (set automatically for editor-only test assemblies) and
the `com.unity.test-framework` package, which is part of the project manifest. If the Test Runner
window is empty, re-import (`Assets → Refresh`) and check the Console for compile errors in
`Maze.Tests.EditMode`.

## Importing an external maze JSON fails

`MazeJsonExport.TryParseSettings` expects the settings block written by
`MazeJsonExport.ToJson`/`SettingsToJson`. A minimal document with only `seed`, `algorithm`, `width`
and `height` is also accepted (the importer then keeps the current settings). Row arrays must be
rectangular and describe a valid lattice (odd interior); otherwise the parser returns `null`
instead of throwing.

## The HUD is too big / too small

`Maze Generator → HUD → Ui Scale` (0.75 - 2). The panel width follows it, and the minimap is scaled
with it. `Panel Width` adjusts the base width.

## Regenerating is slow for very large mazes

See [Performance.md](Performance.md#3-practical-guidance): keep `Render Mode = Mesh`, prefer
`Binary Tree`/`Kruskal` above ~250 cells per side, and disable the mesh collider if you do not need
physics. Generating the `MazeGrid` off the main thread is possible because `MazeGeneratorCore` only
uses plain C#; apply the mesh on the main thread.
