using System.Text;
using Maze.Core;

namespace Maze.Tools
{
    /// <summary>
    /// Renders a maze as ASCII art. Useful for unit tests, for logs and for pasting a maze into a
    /// bug report without shipping a screenshot.
    /// </summary>
    public static class MazeAsciiExporter
    {
        /// <summary>Renders the whole maze with walls as <c>#</c> and passages as <c>.</c>.</summary>
        public static string ToAscii(MazeGrid grid)
        {
            return grid == null ? string.Empty : grid.ToAscii();
        }

        /// <summary>Renders the maze with the solution path drawn as <c>o</c>.</summary>
        public static string ToAscii(MazeGrid grid, System.Collections.Generic.IList<UnityEngine.Vector2Int> path)
        {
            if (grid == null)
            {
                return string.Empty;
            }

            if (path == null || path.Count == 0)
            {
                return grid.ToAscii();
            }

            char[,] buffer = new char[grid.Width, grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    buffer[x, y] = grid.IsWall(x, y) ? '#' : '.';
                }
            }

            for (int i = 0; i < path.Count; i++)
            {
                UnityEngine.Vector2Int cell = path[i];
                if (grid.InBounds(cell.x, cell.y))
                {
                    buffer[cell.x, cell.y] = i == 0 ? 'S' : i == path.Count - 1 ? 'E' : 'o';
                }
            }

            StringBuilder builder = new StringBuilder((grid.Width + 1) * grid.Height);
            for (int y = grid.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    builder.Append(buffer[x, y]);
                }

                if (y > 0)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }
    }
}
