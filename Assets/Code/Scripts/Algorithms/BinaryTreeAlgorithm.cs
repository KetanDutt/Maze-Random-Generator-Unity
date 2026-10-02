using Maze.Core;

namespace Maze.Algorithms
{
    /// <summary>
    /// Binary tree algorithm: every room is connected either north or east with a 50/50 chance.
    /// The fastest generator and the easiest maze - the north east corner is always a dead end and
    /// the whole maze is strongly biased towards the two other corners.
    /// </summary>
    public sealed class BinaryTreeAlgorithm : IMazeAlgorithm
    {
        /// <inheritdoc />
        public string Id
        {
            get { return "binary-tree"; }
        }

        /// <inheritdoc />
        public string DisplayName
        {
            get { return "Binary Tree"; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Fastest generator, strongly biased towards the south west corner. Great for large mazes."; }
        }

        /// <inheritdoc />
        public int MinimumRoomCount
        {
            get { return 1; }
        }

        /// <inheritdoc />
        public void Generate(MazeGrid grid, MazeRandom random)
        {
            for (int y = 0; y < grid.RoomCountY; y++)
            {
                for (int x = 0; x < grid.RoomCountX; x++)
                {
                    grid.CarveRoom(x, y);
                }
            }

            for (int y = 0; y < grid.RoomCountY; y++)
            {
                for (int x = 0; x < grid.RoomCountX; x++)
                {
                    bool canGoNorth = y + 1 < grid.RoomCountY;
                    bool canGoEast = x + 1 < grid.RoomCountX;

                    if (canGoNorth && canGoEast)
                    {
                        grid.CarveCorridor(x, y, random.Chance(0.5f) ? MazeDirection.North : MazeDirection.East);
                    }
                    else if (canGoNorth)
                    {
                        grid.CarveCorridor(x, y, MazeDirection.North);
                    }
                    else if (canGoEast)
                    {
                        grid.CarveCorridor(x, y, MazeDirection.East);
                    }
                }
            }
        }
    }
}
