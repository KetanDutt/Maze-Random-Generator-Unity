# Testing

There are four layers of testing, so the algorithms are verified even without a Unity installation.

## 1. Reference implementation (no Unity required)

`Tools/Reference/maze_reference.py` is a dependency-free Python mirror of the C# code
(class and method names match, so the two can be diffed side by side). It exists so that invariants
can be checked anywhere - CI, another machine, or while refactoring the C#.

```bash
python3 Tools/Reference/verify_maze_reference.py     # 658 invariant checks, exits non-zero on failure
python3 Tools/Reference/benchmark_reference.py       # performance tables used in Docs/Performance.md
python3 Tools/Reference/generate_docs_images.py      # regenerates Docs/images (needs pillow)
```

What the verifier checks:

* every algorithm, for sizes 7/21/41/101 and several seeds: all rooms carved, fully connected,
  perfect tree (passage count), no 2×2 open blocks, no stranded wall row/column, determinism per
  seed, distinctness per seed and per algorithm;
* openings: count, position on the border, exactly one cell wide, entrance and exit connected;
* braiding: dead ends removed, connectivity kept, factor 0 is a no-op;
* path finding: continuity, passability, optimality, diameter, "walls are not a path";
* mesh: winding vs. expected outward normals (the same rule as `Mesh.triangles`), face culling,
  triangle budget;
* the documented limitation of the original implementation stays fixed.

## 2. Unity EditMode tests

`Assets/Code/Tests/EditMode` runs in the Unity Test Runner (**Window → General → Test Runner →
EditMode → Run All**).

| File | Coverage |
| --- | --- |
| `MazeCoreTests.cs` | `MazeRandom` determinism/bounds/shuffle, `MazeGrid` geometry and mutation, settings sanitising, direction helpers. |
| `MazeAlgorithmTests.cs` | Per algorithm/size/seed: all rooms carved, perfect maze, connectivity, no 2×2 blocks, no stranded lanes, determinism, distinctness; openings and braiding. |
| `MazeGenerationTests.cs` | Pipeline: size clamping, determinism, timing, spawn/exit/solution baking, statistics, the mesh builder (submeshes, culling, 32 bit indices, winding, triangle estimate). |
| `MazePathfinderTests.cs` | BFS validity and optimality, depth, reachability, dead end/junction counting, degenerate cases. |
| `MazeExportTests.cs` | ASCII export (plain and with a path), JSON settings round trip, grid round trip from rows, malformed input handling. |

The tests use `[TestCaseSource]` matrices (algorithm × size × seed) which makes failures
easy to reproduce: the test name contains the algorithm, the size and the seed.

## 3. In-editor validation

**Window → Tools → Maze → Validate Generator (all algorithms)** runs the generation pipeline over
4 sizes × 4 algorithms × 3 braid factors × 3 opening modes × 3 seeds (432 configurations), checks the
invariants and reports the result in a dialog plus the console. Use it after changing an algorithm.

## Continuous integration

`.github/workflows/ci.yml` runs three jobs on every push and pull request:

| Job | Runs | Needs |
| --- | --- | --- |
| `reference-tests` | the Python verifier, the benchmark and a documentation image smoke test | Python 3 |
| `csharp-checks` | `node Tools/Reference/lint_csharp.mjs Assets/Code` (syntax) and `check_references.mjs` (member references) | Node.js 18+ |
| `unity-tests` | the Unity EditMode suite through `game-ci/unity-test-runner` | a Unity license in the repository secrets |

The `unity-tests` job ships with `if: false` so it is skipped until a license is configured - remove
that line (and add `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) to enable it. The other two jobs
need no secrets and fail within seconds.

## Test matrix summary

| Layer | Runs | Requires | Catches |
| --- | --- | --- | --- |
| `verify_maze_reference.py` | seconds | Python 3 | algorithm, opening, braid, path and mesh invariant regressions |
| Unity EditMode tests | ~1 min | Unity | the same invariants plus Unity API usage and serialisation |
| `Validate Generator` menu | seconds | Unity editor | size/braid/opening combinations end to end |
| `lint_csharp.mjs` | < 1 s | Node.js | syntax errors that would stop the Unity compiler |
| `check_references.mjs` | < 1 s | Node.js | member accesses on project types that do not exist (typos, renamed members, stale call sites) |
