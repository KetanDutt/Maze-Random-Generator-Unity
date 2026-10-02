using System.Collections.Generic;
using Maze.Core;
using UnityEngine;

namespace Maze.Algorithms
{
    /// <summary>
    /// Randomized Prim's algorithm: grows the maze from a single room by repeatedly picking a
    /// random frontier room and connecting it to an already carved neighbour.
    /// </summary>
    /// <remarks>
    /// This is the algorithm of the original project, with two important changes:
    /// <list type="bullet">
    /// <item>It works on the room lattice, so start cell parity can never strand a row or column.</item>
    /// <item>The frontier is a <see cref="List{T}"/> with O(1) swap removal. The original code used
    /// <c>frontier.ElementAt(randomIndex)</c> on a <c>HashSet</c>, which enumerates the set from the
    /// beginning on every pick (O(n) per step, O(n^2) per maze) and allocates an enumerator each time.</item>
    /// </list>
    /// </remarks>
    public sealed class RandomizedPrimAlgorithm : IMazeAlgorithm
    {
        /// <inheritdoc />
        public string Id
        {
            get { return "prim"; }
        }

        /// <inheritdoc />
        public string DisplayName
        {
            get { return "Randomized Prim"; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Grows the maze from one room; many short dead ends. The classic look."; }
        }

        /// <inheritdoc />
        public int MinimumRoomCount
        {
            get { return 1; }
        }

        /// <inheritdoc />
        public void Generate(MazeGrid grid, MazeRandom random)
        {
            bool[] visited = new bool[grid.RoomCount];
            List<Vector2Int> frontier = new List<Vector2Int>(grid.RoomCount);
            Vector2Int[] neighbours = new Vector2Int[MazeDirections.Count];
            Vector2Int[] candidates = new Vector2Int[MazeDirections.Count];

            Vector2Int start = new Vector2Int(random.NextIndex(grid.RoomCountX), random.NextIndex(grid.RoomCountY));
            visited[start.y * grid.RoomCountX + start.x] = true;
            grid.CarveRoom(start.x, start.y);

            int startNeighbourCount = grid.CollectRoomNeighbours(start.x, start.y, neighbours);
            for (int i = 0; i < startNeighbourCount; i++)
            {
                frontier.Add(neighbours[i]);
            }

            while (frontier.Count > 0)
            {
                int pick = random.NextIndex(frontier.Count);
                Vector2Int room = frontier[pick];
                frontier[pick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);

                if (visited[room.y * grid.RoomCountX + room.x])
                {
                    continue;
                }

                int neighbourCount = grid.CollectRoomNeighbours(room.x, room.y, neighbours);

                // Pick one of the already carved neighbours to connect to.
                int candidateCount = 0;
                for (int i = 0; i < neighbourCount; i++)
                {
                    if (visited[neighbours[i].y * grid.RoomCountX + neighbours[i].x])
                    {
                        candidates[candidateCount++] = neighbours[i];
                    }
                }

                if (candidateCount == 0)
                {
                    continue; // defensive: cannot happen for a connected room lattice
                }

                Vector2Int parent = candidates[random.NextIndex(candidateCount)];
                MazeDirection direction = MazeDirections.FromOffset(parent.x - room.x, parent.y - room.y);
                grid.CarveCorridor(room.x, room.y, direction);
                visited[room.y * grid.RoomCountX + room.x] = true;

                for (int i = 0; i < neighbourCount; i++)
                {
                    Vector2Int neighbour = neighbours[i];
                    if (!visited[neighbour.y * grid.RoomCountX + neighbour.x])
                    {
                        frontier.Add(neighbour);
                    }
                }
            }

            // Safety net: a room that was never reached would leave a hole in the maze.
            EnsureEveryRoomCarved(grid);
        }

        /// <summary>
        /// Defensive pass shared by every algorithm: carves a room that was missed and connects it
        /// to an already carved neighbour so the maze always stays connected. On the room lattice
        /// this never has to do any work - it only turns a hypothetical hole into a valid maze.
        /// </summary>
        internal static void EnsureEveryRoomCarved(MazeGrid grid)
        {
            Vector2Int[] neighbours = new Vector2Int[MazeDirections.Count];
            bool progress = true;
            while (progress)
            {
                progress = false;
                for (int y = 0; y < grid.RoomCountY; y++)
                {
                    for (int x = 0; x < grid.RoomCountX; x++)
                    {
                        if (grid.IsRoomCarved(x, y))
                        {
                            continue;
                        }

                        int count = grid.CollectRoomNeighbours(x, y, neighbours);
                        for (int i = 0; i < count; i++)
                        {
                            Vector2Int neighbour = neighbours[i];
                            if (grid.IsRoomCarved(neighbour.x, neighbour.y))
                            {
                                grid.CarveCorridor(x, y, MazeDirections.FromOffset(neighbour.x - x, neighbour.y - y));
                                progress = true;
                                break;
                            }
                        }
                    }
                }
            }

            for (int y = 0; y < grid.RoomCountY; y++)
            {
                for (int x = 0; x < grid.RoomCountX; x++)
                {
                    grid.CarveRoom(x, y);
                }
            }
        }
    }
}
