using System.Collections.Generic;
using Maze.Core;
using UnityEngine;

namespace Maze.Generation
{
    /// <summary>
    /// Cuts entrances and exits into the outer wall of a maze. This is the feature the original
    /// implementation was missing entirely (its mazes were always sealed).
    /// </summary>
    public static class MazeOpeningCutter
    {
        /// <summary>Cuts the openings described by <paramref name="settings"/> and returns them.</summary>
        public static List<MazeOpening> Cut(MazeGrid grid, MazeGeneratorSettings settings, MazeRandom random)
        {
            List<MazeOpening> openings = new List<MazeOpening>(4);
            if (grid == null || settings == null || settings.OpeningMode == MazeOpeningMode.None)
            {
                return openings;
            }

            if (settings.OpeningMode == MazeOpeningMode.FixedEntranceAndExit)
            {
                openings.Add(CutSide(grid, settings.EntranceSide, true, random));
                if (settings.ExitSide != settings.EntranceSide)
                {
                    openings.Add(CutSide(grid, settings.ExitSide, true, random));
                }

                return openings;
            }

            // Random mode: pick distinct sides and a random room on each of them.
            List<MazeDirection> sides = new List<MazeDirection>(MazeDirections.Values);
            random.Shuffle(sides);
            int count = Mathf.Clamp(settings.RandomOpeningCount, 0, MazeDirections.Count);
            for (int i = 0; i < count; i++)
            {
                openings.Add(CutSide(grid, sides[i], false, random));
            }

            return openings;
        }

        /// <summary>Cuts a single opening on <paramref name="side"/>.</summary>
        private static MazeOpening CutSide(MazeGrid grid, MazeDirection side, bool middle, MazeRandom random)
        {
            int roomX;
            int roomY;

            if (side == MazeDirection.North || side == MazeDirection.South)
            {
                int span = grid.RoomCountX;
                roomX = middle ? span / 2 : random.NextIndex(span);
                roomY = side == MazeDirection.North ? grid.RoomCountY - 1 : 0;
            }
            else
            {
                int span = grid.RoomCountY;
                roomX = side == MazeDirection.East ? grid.RoomCountX - 1 : 0;
                roomY = middle ? span / 2 : random.NextIndex(span);
            }

            Vector2Int offset = side.ToOffset();
            Vector2Int roomCell = grid.CellOfRoom(roomX, roomY);
            Vector2Int cell = roomCell;

            // Carve the room, then walk outwards through the padding until the grid ends. The last
            // carved cell is the border cell - the actual hole in the outer wall.
            while (true)
            {
                grid.Carve(cell.x, cell.y);
                int nextX = cell.x + offset.x;
                int nextY = cell.y + offset.y;
                if (!grid.InBounds(nextX, nextY))
                {
                    break;
                }

                cell = new Vector2Int(nextX, nextY);
            }

            MazeOpening opening = new MazeOpening(side, roomCell, cell);
            grid.RegisterOpening(opening);
            return opening;
        }
    }
}
