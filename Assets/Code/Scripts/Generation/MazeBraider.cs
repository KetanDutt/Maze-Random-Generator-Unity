using System.Collections.Generic;
using Maze.Core;
using UnityEngine;

namespace Maze.Generation
{
    /// <summary>
    /// Post processing that removes dead ends ("braiding"). Every braided room gets an additional
    /// corridor, which turns the maze into a graph with loops: easier to navigate, and the classic
    /// answer to game mazes that feel like a single solution corridor.
    /// </summary>
    public static class MazeBraider
    {
        /// <summary>
        /// Opens extra corridors for roughly <paramref name="factor"/> of the dead ends.
        /// Returns the number of corridors that were opened.
        /// </summary>
        /// <param name="factor">0 keeps the maze perfect, 1 removes every dead end.</param>
        public static int Braid(MazeGrid grid, float factor, MazeRandom random)
        {
            if (grid == null || factor <= 0f)
            {
                return 0;
            }

            factor = Mathf.Clamp01(factor);

            List<Vector2Int> rooms = new List<Vector2Int>(grid.RoomCount);
            foreach (Vector2Int room in grid.EnumerateRooms())
            {
                rooms.Add(room);
            }

            // Randomised order so that the maze does not get braided in scan line order.
            random.Shuffle(rooms);

            MazeDirection[] candidates = new MazeDirection[MazeDirections.Count];
            int opened = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                Vector2Int room = rooms[i];
                if (grid.RoomDegree(room.x, room.y) != 1)
                {
                    continue;
                }

                // Entrance and exit rooms keep their single connection, otherwise the opening would
                // no longer be a dead end and the objective of the maze would lose its point.
                if (grid.IsOpeningRoom(room.x, room.y))
                {
                    continue;
                }

                if (!random.Chance(factor))
                {
                    continue;
                }

                int candidateCount = 0;
                for (int d = 0; d < MazeDirections.Count; d++)
                {
                    MazeDirection direction = MazeDirections.Values[d];
                    Vector2Int offset = direction.ToOffset();
                    if (grid.InRoomBounds(room.x + offset.x, room.y + offset.y) &&
                        !grid.IsCorridorCarved(room.x, room.y, direction))
                    {
                        candidates[candidateCount++] = direction;
                    }
                }

                if (candidateCount == 0)
                {
                    continue;
                }

                grid.CarveCorridor(room.x, room.y, candidates[random.NextIndex(candidateCount)]);
                opened++;
            }

            return opened;
        }
    }
}
