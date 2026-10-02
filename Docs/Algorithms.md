# Algorithms

All four algorithms are *spanning tree* generators: they carve every room exactly once and connect
them without creating a loop, which produces a **perfect maze** - exactly one path between any two
cells. Every algorithm finishes with an optional post-processing pass
([openings](#openings) and [braiding](#braiding)) and produces a maze on the same room lattice, so
they can be swapped at runtime without changing anything else.

![Algorithm comparison](images/algorithm-comparison.png)

| Algorithm | Id | Dead ends (31×31) | Character | Cost |
| --- | --- | --- | --- | --- |
| Randomized Prim | `prim` | ~78 | Many short dead ends, "classic" look, forgiving to explore | O(rooms) with a frontier list |
| Depth First | `dfs` | ~27 | Long winding corridors, few but very long dead ends, hardest to solve | O(rooms) stack |
| Randomized Kruskal | `kruskal` | ~70 | Uniform texture, no directional bias, well balanced | O(edges log edges) shuffle |
| Binary Tree | `binary-tree` | ~56 | Two diagonal "highways", strongly biased to the south west corner | O(rooms), no frontier at all |

(The dead end numbers are measured by `Tools/Reference/benchmark_reference.py` and the test suite for
seed dependent runs; they vary slightly per seed but the ordering is stable.)

## Randomized Prim (`RandomizedPrimAlgorithm`)

1. Pick a random start room, carve it, and put all its neighbours into a *frontier* list.
2. While the frontier is not empty: take a random frontier room, connect it to one already carved
   neighbour and push its untouched neighbours onto the frontier.

Frontier handling matters for performance. The original implementation kept the frontier in a
`HashSet` and picked a random entry with `frontier.ElementAt(randomIndex)`, which enumerates the set
from the start every single time: O(frontier) per pick expanded to O(rooms²) work for the whole
maze, plus one enumerator allocation per pick. The frontier is now a `List<Vector2Int>` with O(1)
swap-removal:

| Maze | `HashSet` + `ElementAt` | `List` + swap remove | Speed-up |
| --- | --- | --- | --- |
| 21×21 | 1 218 ops | 220 ops | 6× |
| 51×51 | 16 484 ops | 1 300 ops | 13× |
| 101×101 | 136 619 ops | 5 100 ops | 27× |
| 201×201 | 1 111 330 ops | 20 200 ops | 55× |

Measured by `Tools/Reference/verify_maze_reference.py` / `benchmark_reference.py` (operation counts,
hardware independent; see [Performance.md](Performance.md)).

## Depth First (`DepthFirstAlgorithm`)

A recursive backtracker implemented with an explicit stack: walk to a random unvisited neighbour
until you get stuck, then backtrack. Produces long corridors and the smallest number of dead ends -
the best choice for a "find your way out" level.

## Randomized Kruskal (`RandomizedKruskalAlgorithm`)

Shuffles all candidate corridors and connects the two rooms of an edge when they are not connected
yet (union-find with path halving and union by rank). The result is a uniform spanning tree: no
region of the maze is systematically easier than another.

## Binary Tree (`BinaryTreeAlgorithm`)

Every room is connected either north or east with a 50/50 chance. It is the fastest generator
(no frontier, no bookkeeping) and produces a maze with a strong diagonal bias - the north east
corner is always a dead end. Ideal for very large mazes where the texture matters more than the
difficulty.

## Openings

`MazeOpeningCutter` cuts one-cell holes into the outer wall:

* `None` - a sealed maze (perfect maze, for puzzle levels).
* `FixedEntranceAndExit` - one opening in the middle of two opposite sides (default: north/south).
* `Random` - 1-4 openings on randomly chosen sides and positions.

An opening is carved from the room through the padding to the grid border, so it is exactly one cell
wide and can be walked through. Entrances and exits are kept as *dead ends* when braiding
(`MazeBraider` skips opening rooms), so the goal of the maze stays meaningful.

## Braiding

A perfect maze can feel like a single corridor with extra steps. `MazeBraider` removes dead ends by
opening an additional corridor for a fraction of them (`Braid Factor`):

![Braid comparison](images/braid-comparison.png)

* `0.0` - perfect maze, no loops (default for puzzle solving).
* `0.25` - a few shortcuts, easier navigation (demo default).
* `1.0` - no dead ends at all, every room is on a loop.

Because braiding only adds corridors, the maze stays fully connected and every previously reachable
cell remains reachable.

## Theory

### The original algorithm

Randomized Prim on a randomly weighted grid graph:

1. Start with every cell as a wall.
2. Pick a random cell, make it a passage - this is the first room.
3. Add its *frontier* cells (walls exactly two cells away horizontally or vertically) to a set.
4. While the set is not empty:
   * pick a random frontier cell, carve it,
   * connect it to one of its already carved neighbours at distance two,
   * carve the cell in between,
   * add the new frontier cells.
5. The result is a spanning tree of the room lattice: a perfect maze.

### Improvements over the original implementation

| Aspect | Original | Now |
| --- | --- | --- |
| Start cell | `Random.Range(3, size - 3)` - could be even, which stranded a whole wall row/column | room lattice (`padding + even`), no stranded lanes possible |
| Frontier | `HashSet` + `ElementAt` - O(rooms²) | `List` + swap remove - O(rooms) |
| Neighbour scan | tuples + `HashSet` allocations per cell | pre-allocated `Vector2Int[4]` buffers, zero allocation |
| Render output | one `GameObject` + collider per wall cell | single mesh, culled faces, 1 draw call |
| Entrance / exit | not supported | openings (fixed or random) |
| Loops | not supported | braiding |
| Algorithms | one | four, registry based |
| Randomness | `UnityEngine.Random` (not reproducible across versions) | seeded xorshift128, reproducible |
| Room count | could leave rooms uncarved (parity bug) | every room is carved and connected, verified by tests |

## References

* Jamis Buck - *Maze Generation: Prim's Algorithm*: <https://weblog.jamisbuck.org/2011/1/10/maze-generation-prim-s-algorithm>
* Jamis Buck - *Maze Generation: Growing Tree algorithm* (covers recursive backtracker and Prim):
  <https://weblog.jamisbuck.org/2011/1/27/maze-generation-growing-tree-algorithm>
* Arne Stenkrona - *MazeFun* (the source of the illustrations of the original README):
  <https://github.com/ArneStenkrona/MazeFun>
