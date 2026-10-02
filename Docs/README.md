# Documentation

Everything you need to use, configure, extend or ship the maze generator.

| Document | What it covers |
| --- | --- |
| [GettingStarted.md](GettingStarted.md) | Install the project, open the demo, controls, first custom maze, first build. |
| [Architecture.md](Architecture.md) | How the code is organised, the generation pipeline, extension points and class map. |
| [Algorithms.md](Algorithms.md) | The four algorithms, their look and feel, the theory and the frontier/braiding details. |
| [Configuration.md](Configuration.md) | Every setting of `MazeGeneratorSettings` with ranges, defaults and effects. |
| [API.md](API.md) | Public API reference: components, events, core types, tools and editor menus. |
| [Performance.md](Performance.md) | Measured numbers, complexity, draw calls, mesh budget and scaling guidance. |
| [Testing.md](Testing.md) | Unity test suite, the reference implementation, the CLI validator and CI. |
| [Troubleshooting.md](Troubleshooting.md) | Common problems (pink materials, wrong input system, missing prefab, ...) and fixes. |
| [Contributing.md](Contributing.md) | Coding style, adding an algorithm, running the checks before a pull request. |
| [Changelog.md](Changelog.md) | What changed compared to the original implementation. |

## Images

The diagrams in the documentation and the README are generated from the algorithms
themselves:

| Image | Content |
| --- | --- |
| `images/algorithm-comparison.png` | The same seed with all four algorithms. |
| `images/braid-comparison.png` | Braid factor 0 / 0.5 / 1 and the removed dead ends. |
| `images/solution-path.png` | The entrance to exit route found by the pathfinder. |
| `images/mesh-preview.png` | Isometric preview of the generated mesh (1 draw call). |

Regenerate them after changing an algorithm:

```bash
python3 -m pip install pillow
python3 Tools/Reference/generate_docs_images.py
```
