using System.Text;
using Maze.Analysis;
using Maze.Core;
using UnityEngine;

namespace Maze.Generation
{
    /// <summary>
    /// A snapshot of everything worth knowing about a generated maze. Shown in the inspector, in
    /// the in game HUD and used by the tests to prove the structural invariants.
    /// </summary>
    public sealed class MazeStatistics
    {
        private MazeStatistics()
        {
        }

        /// <summary>Identifier of the algorithm that produced the maze.</summary>
        public string AlgorithmId { get; private set; }

        /// <summary>Seed of the maze.</summary>
        public int Seed { get; private set; }

        /// <summary>Grid width in cells.</summary>
        public int Width { get; private set; }

        /// <summary>Grid height in cells.</summary>
        public int Height { get; private set; }

        /// <summary>Number of rooms (the room lattice size).</summary>
        public int RoomCount { get; private set; }

        /// <summary>Number of wall cells.</summary>
        public int WallCount { get; private set; }

        /// <summary>Number of passage cells.</summary>
        public int PassageCount { get; private set; }

        /// <summary>Number of corridor cells (passage cells that are not rooms).</summary>
        public int CorridorCount { get; private set; }

        /// <summary>Rooms with a single connection.</summary>
        public int DeadEndCount { get; private set; }

        /// <summary>Rooms with three or four connections.</summary>
        public int JunctionCount { get; private set; }

        /// <summary>Number of openings cut into the outer wall.</summary>
        public int OpeningCount { get; private set; }

        /// <summary>Length in cells of the entrance/exit solution path (0 without two openings).</summary>
        public int SolutionPathLength { get; private set; }

        /// <summary>Farthest number of steps from the entrance (or from the first passage).</summary>
        public int Depth { get; private set; }

        /// <summary>
        /// <c>true</c> when the maze is a tree: exactly one path between any two cells, no loops.
        /// </summary>
        public bool IsPerfect { get; private set; }

        /// <summary><c>true</c> when every passage cell is reachable from every other one.</summary>
        public bool IsFullyConnected { get; private set; }

        /// <summary>Number of rooms that is reachable, equal to <see cref="RoomCount"/> for a valid maze.</summary>
        public int ReachableRoomCount { get; private set; }

        /// <summary>Computes the statistics of <paramref name="grid"/>.</summary>
        public static MazeStatistics Compute(MazeGrid grid, string algorithmId, int seed, int solutionPathLength)
        {
            MazeStatistics statistics = new MazeStatistics
            {
                AlgorithmId = algorithmId ?? string.Empty,
                Seed = seed,
                Width = grid.Width,
                Height = grid.Height,
                RoomCount = grid.RoomCount,
                WallCount = grid.WallCount,
                PassageCount = grid.PassageCount,
                OpeningCount = grid.Openings.Count,
                DeadEndCount = MazePathfinder.CountDeadEnds(grid),
                JunctionCount = MazePathfinder.CountJunctions(grid),
                SolutionPathLength = solutionPathLength,
            };

            statistics.CorridorCount = statistics.PassageCount - statistics.RoomCount;
            statistics.IsPerfect = statistics.CorridorCount == statistics.RoomCount - 1;
            statistics.IsFullyConnected = MazePathfinder.IsFullyConnected(grid);

            Vector2Int start = grid.Openings.Count > 0
                ? grid.Openings[0].BorderCell
                : MazePathfinder.FindFirstPassage(grid);
            Vector2Int farthest;
            statistics.Depth = MazePathfinder.MeasureDepth(grid, start, out farthest);
            statistics.ReachableRoomCount = statistics.IsFullyConnected ? statistics.RoomCount : 0;

            return statistics;
        }

        /// <summary>Multi line summary used by the inspector, the HUD and the console export.</summary>
        public string ToSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Algorithm: ").Append(AlgorithmId).Append('\n');
            builder.Append("Seed: ").Append(Seed).Append('\n');
            builder.Append("Size: ").Append(Width).Append('x').Append(Height)
                .Append(" (").Append(RoomCount).Append(" rooms)\n");
            builder.Append("Walls: ").Append(WallCount).Append("   Passages: ").Append(PassageCount)
                .Append("   Corridors: ").Append(CorridorCount).Append('\n');
            builder.Append("Dead ends: ").Append(DeadEndCount).Append("   Junctions: ").Append(JunctionCount)
                .Append('\n');
            builder.Append("Openings: ").Append(OpeningCount).Append("   Solution: ").Append(SolutionPathLength)
                .Append(" cells   Depth: ").Append(Depth).Append('\n');
            builder.Append(IsPerfect ? "Perfect maze (tree)" : "Contains loops").Append("   ");
            builder.Append(IsFullyConnected ? "Fully connected" : "NOT fully connected");
            return builder.ToString();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "MazeStatistics(" + Width + "x" + Height + ", " + RoomCount + " rooms, " + DeadEndCount +
                   " dead ends, perfect=" + IsPerfect + ", connected=" + IsFullyConnected + ")";
        }
    }
}
