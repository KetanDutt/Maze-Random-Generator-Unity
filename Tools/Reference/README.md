# Tools

Developer tooling for the maze generator. Nothing in this folder is compiled into the Unity project;
it exists to verify, benchmark and document the algorithms without opening Unity.

| File | Purpose |
| --- | --- |
| `maze_reference.py` | Dependency-free Python mirror of the C# core (`MazeGrid`, `MazeRandom`, the four algorithms, openings, braiding, path finding, mesh stats). Same names as the C# code so the two can be compared side by side. |
| `verify_maze_reference.py` | The invariant test suite: 658 checks over algorithms, sizes, seeds, openings, braiding, paths, mesh winding and budgets. Exits non-zero on failure - used by CI. |
| `benchmark_reference.py` | Performance characterisation: frontier container operations, generation timing per algorithm/size, mesh budget tables. The numbers are quoted in `Docs/Performance.md`. |
| `generate_docs_images.py` | Renders `Docs/images/*.png` from the algorithms (needs `pillow`). |
| `lint_csharp.mjs` | Parses every `.cs` file with tree-sitter and fails on syntax errors - a Unity-free smoke test for CI (needs Node.js and two npm packages). |
| `check_references.mjs` | Cross checks member accesses on project types (private fields, static members, locals) against the declared members - catches typos and stale call sites without a compiler. |

## Usage

```bash
# 1. Algorithm invariants (no dependencies, seconds)
python3 Tools/Reference/verify_maze_reference.py

# 2. Performance tables
python3 Tools/Reference/benchmark_reference.py

# 3. Regenerate the diagrams in Docs/images (needs pillow)
python3 -m pip install pillow
python3 Tools/Reference/generate_docs_images.py

# 4. C# static checks without Unity (needs Node.js 18+)
npm install --no-save web-tree-sitter@0.22.6 tree-sitter-wasms
node Tools/Reference/lint_csharp.mjs Assets/Code        # syntax errors
node Tools/Reference/check_references.mjs Assets/Code   # unknown members
```

When you change an algorithm in C#, change `maze_reference.py` the same way, run the verifier, then
re-run the Unity EditMode tests. The two implementations are meant to stay in sync; the C# tests and
the Python verifier assert the same invariants.
