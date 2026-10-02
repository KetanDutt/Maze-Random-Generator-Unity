using System.Collections.Generic;
using Maze.Core;
using UnityEngine;

namespace Maze.Analysis
{
    /// <summary>
    /// Graph utilities for a carved <see cref="MazeGrid"/>: shortest paths, reachability and the
    /// statistics the UI shows (dead ends, junctions, maze depth). Everything is a plain breadth
    /// first search over passage cells, i.e. O(cells) per query.
    /// </summary>
    public static class MazePathfinder
    {
        private static readonly Vector2Int[] NeighbourOffsets =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
        };

        /// <summary>
        /// Shortest path between two cells, or <c>null</c> when they are not connected. Both cells
        /// must be passages.
        /// </summary>
        public static List<Vector2Int> FindPath(MazeGrid grid, Vector2Int start, Vector2Int goal)
        {
            if (grid == null || !grid.IsPassage(start.x, start.y) || !grid.IsPassage(goal.x, goal.y))
            {
                return null;
            }

            int cellCount = grid.Width * grid.Height;
            int[] previous = new int[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                previous[i] = -1;
            }

            int startIndex = start.y * grid.Width + start.x;
            int goalIndex = goal.y * grid.Width + goal.x;
            previous[startIndex] = startIndex;

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(startIndex);
            bool found = false;

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                if (index == goalIndex)
                {
                    found = true;
                    break;
                }

                int x = index % grid.Width;
                int y = index / grid.Width;
                for (int i = 0; i < NeighbourOffsets.Length; i++)
                {
                    int nx = x + NeighbourOffsets[i].x;
                    int ny = y + NeighbourOffsets[i].y;
                    if (!grid.IsPassage(nx, ny))
                    {
                        continue;
                    }

                    int neighbourIndex = ny * grid.Width + nx;
                    if (previous[neighbourIndex] != -1)
                    {
                        continue;
                    }

                    previous[neighbourIndex] = index;
                    queue.Enqueue(neighbourIndex);
                }
            }

            if (!found)
            {
                return null;
            }

            List<Vector2Int> path = new List<Vector2Int>();
            int cursor = goalIndex;
            while (cursor != startIndex)
            {
                path.Add(new Vector2Int(cursor % grid.Width, cursor / grid.Width));
                cursor = previous[cursor];
            }

            path.Add(start);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Breadth first search from <paramref name="start"/>; returns the number of steps to the
        /// farthest reachable cell (the "depth" of the maze from that point).
        /// </summary>
        /// <param name="farthest">The cell that is farthest away, or <paramref name="start"/> when nothing is reachable.</param>
        public static int MeasureDepth(MazeGrid grid, Vector2Int start, out Vector2Int farthest)
        {
            farthest = start;
            if (grid == null || !grid.IsPassage(start.x, start.y))
            {
                return 0;
            }

            int cellCount = grid.Width * grid.Height;
            int[] distance = new int[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                distance[i] = -1;
            }

            int startIndex = start.y * grid.Width + start.x;
            distance[startIndex] = 0;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(startIndex);

            int bestIndex = startIndex;
            int bestDistance = 0;

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int currentDistance = distance[index];
                if (currentDistance > bestDistance)
                {
                    bestDistance = currentDistance;
                    bestIndex = index;
                }

                int x = index % grid.Width;
                int y = index / grid.Width;
                for (int i = 0; i < NeighbourOffsets.Length; i++)
                {
                    int nx = x + NeighbourOffsets[i].x;
                    int ny = y + NeighbourOffsets[i].y;
                    if (!grid.IsPassage(nx, ny))
                    {
                        continue;
                    }

                    int neighbourIndex = ny * grid.Width + nx;
                    if (distance[neighbourIndex] != -1)
                    {
                        continue;
                    }

                    distance[neighbourIndex] = currentDistance + 1;
                    queue.Enqueue(neighbourIndex);
                }
            }

            farthest = new Vector2Int(bestIndex % grid.Width, bestIndex / grid.Width);
            return bestDistance;
        }

        /// <summary>Number of passage cells reachable from <paramref name="start"/> (inclusive).</summary>
        public static int CountReachable(MazeGrid grid, Vector2Int start)
        {
            if (grid == null || !grid.IsPassage(start.x, start.y))
            {
                return 0;
            }

            bool[] visited = new bool[grid.Width * grid.Height];
            int startIndex = start.y * grid.Width + start.x;
            visited[startIndex] = true;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(startIndex);
            int count = 0;

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                count++;
                int x = index % grid.Width;
                int y = index / grid.Width;
                for (int i = 0; i < NeighbourOffsets.Length; i++)
                {
                    int nx = x + NeighbourOffsets[i].x;
                    int ny = y + NeighbourOffsets[i].y;
                    if (!grid.IsPassage(nx, ny))
                    {
                        continue;
                    }

                    int neighbourIndex = ny * grid.Width + nx;
                    if (visited[neighbourIndex])
                    {
                        continue;
                    }

                    visited[neighbourIndex] = true;
                    queue.Enqueue(neighbourIndex);
                }
            }

            return count;
        }

        /// <summary><c>true</c> when every passage cell can be reached from any other passage cell.</summary>
        public static bool IsFullyConnected(MazeGrid grid)
        {
            if (grid == null)
            {
                return false;
            }

            Vector2Int start = FindFirstPassage(grid);
            if (start.x < 0)
            {
                return true; // no passages at all
            }

            return CountReachable(grid, start) == grid.PassageCount;
        }

        /// <summary>Number of rooms that have exactly one corridor (dead ends).</summary>
        public static int CountDeadEnds(MazeGrid grid)
        {
            int deadEnds = 0;
            foreach (Vector2Int room in grid.EnumerateRooms())
            {
                int connections = grid.RoomDegree(room.x, room.y);
                if (grid.IsOpeningRoom(room.x, room.y))
                {
                    connections++;
                }

                if (connections == 1)
                {
                    deadEnds++;
                }
            }

            return deadEnds;
        }

        /// <summary>Number of rooms with three or four corridors (decision points).</summary>
        public static int CountJunctions(MazeGrid grid)
        {
            int junctions = 0;
            foreach (Vector2Int room in grid.EnumerateRooms())
            {
                if (grid.RoomDegree(room.x, room.y) >= 3)
                {
                    junctions++;
                }
            }

            return junctions;
        }

        /// <summary>Returns the first passage cell in scan order, or <c>(-1, -1)</c> when there is none.</summary>
        public static Vector2Int FindFirstPassage(MazeGrid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsPassage(x, y))
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }

            return new Vector2Int(-1, -1);
        }
    }
}
