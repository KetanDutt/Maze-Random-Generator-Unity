using Maze.Core;

namespace Maze.Algorithms
{
    /// <summary>
    /// A maze carving algorithm. Implementations must not touch Unity APIs so that they stay
    /// testable and portable; they only mutate the <see cref="MazeGrid"/> using the supplied
    /// deterministic <see cref="MazeRandom"/>.
    /// </summary>
    public interface IMazeAlgorithm
    {
        /// <summary>Stable identifier used by save files and command line tools (for example <c>prim</c>).</summary>
        string Id { get; }

        /// <summary>Name shown in the UI.</summary>
        string DisplayName { get; }

        /// <summary>One line description of the resulting maze character.</summary>
        string Description { get; }

        /// <summary>Smallest number of rooms the algorithm needs (some need at least two).</summary>
        int MinimumRoomCount { get; }

        /// <summary>Carves <paramref name="grid"/> into a maze. Every room becomes a passage.</summary>
        void Generate(MazeGrid grid, MazeRandom random);
    }
}
