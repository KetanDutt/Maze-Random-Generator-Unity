using System.Collections.Generic;
using Maze.Core;
using UnityEngine;

namespace Maze.Algorithms
{
    /// <summary>
    /// Depth first search (recursive backtracker): walks as far as possible before backtracking,
    /// which produces long, winding corridors and few dead ends.
    /// </summary>
    public sealed class DepthFirstAlgorithm : IMazeAlgorithm
    {
        /// <inheritdoc />
        public string Id
        {
            get { return "dfs"; }
        }

        /// <inheritdoc />
        public string DisplayName
        {
            get { return "Depth First"; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Recursive backtracker: long winding corridors, few dead ends, high difficulty."; }
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
            List<Vector2Int> stack = new List<Vector2Int>(grid.RoomCount);
            Vector2Int[] neighbours = new Vector2Int[MazeDirections.Count];
            Vector2Int[] open = new Vector2Int[MazeDirections.Count];

            Vector2Int start = new Vector2Int(random.NextIndex(grid.RoomCountX), random.NextIndex(grid.RoomCountY));
            visited[start.y * grid.RoomCountX + start.x] = true;
            grid.CarveRoom(start.x, start.y);
            stack.Add(start);

            while (stack.Count > 0)
            {
                Vector2Int room = stack[stack.Count - 1];
                int neighbourCount = grid.CollectRoomNeighbours(room.x, room.y, neighbours);

                int openCount = 0;
                for (int i = 0; i < neighbourCount; i++)
                {
                    if (!visited[neighbours[i].y * grid.RoomCountX + neighbours[i].x])
                    {
                        open[openCount++] = neighbours[i];
                    }
                }

                if (openCount == 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                Vector2Int next = open[random.NextIndex(openCount)];
                grid.CarveCorridor(room.x, room.y,
                    MazeDirections.FromOffset(next.x - room.x, next.y - room.y));
                visited[next.y * grid.RoomCountX + next.x] = true;
                stack.Add(next);
            }

            RandomizedPrimAlgorithm.EnsureEveryRoomCarved(grid);
        }
    }
}
