using System;
using System.Diagnostics;
using Maze.Algorithms;
using Maze.Core;

namespace Maze.Generation
{
    /// <summary>The raw output of a generation run, before anything is turned into scene objects.</summary>
    public sealed class MazeGenerationOutput
    {
        internal MazeGenerationOutput(MazeGrid grid, int seed, string algorithmId, double milliseconds,
            bool sizeWasAdjusted)
        {
            Grid = grid;
            Seed = seed;
            AlgorithmId = algorithmId;
            Milliseconds = milliseconds;
            SizeWasAdjusted = sizeWasAdjusted;
        }

        /// <summary>The carved grid.</summary>
        public MazeGrid Grid { get; }

        /// <summary>The seed that produced this maze.</summary>
        public int Seed { get; }

        /// <summary>Identifier of the algorithm that produced this maze.</summary>
        public string AlgorithmId { get; }

        /// <summary>Wall clock time of the algorithm itself (openings and braiding included).</summary>
        public double Milliseconds { get; }

        /// <summary>
        /// <c>true</c> when the requested width/height had to be adjusted (made odd or clamped).
        /// </summary>
        public bool SizeWasAdjusted { get; }
    }

    /// <summary>
    /// The generation pipeline: validate the settings, carve the maze with the selected algorithm,
    /// then optionally cut the openings and braid the dead ends. It has no dependencies on the
    /// Unity scene, which makes it usable from tests, editor tools and batch processes.
    /// </summary>
    public sealed class MazeGeneratorCore
    {
        /// <summary>Runs the full pipeline and returns the grid plus generation metadata.</summary>
        /// <exception cref="ArgumentNullException">When <paramref name="settings"/> is null.</exception>
        public MazeGenerationOutput Generate(MazeGeneratorSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            int requestedWidth = settings.MazeWidth;
            int requestedHeight = settings.MazeHeight;
            settings.Sanitize();
            int seed = settings.ResolveSeed();
            return Generate(settings, seed, requestedWidth, requestedHeight);
        }

        /// <summary>
        /// Runs the pipeline with a fixed seed (the <see cref="MazeGeneratorSettings.UseRandomSeed"/>
        /// flag is ignored). Used by tools, tests and the "share this seed" workflow.
        /// </summary>
        public MazeGenerationOutput Generate(MazeGeneratorSettings settings, int seed)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            int requestedWidth = settings.MazeWidth;
            int requestedHeight = settings.MazeHeight;
            settings.Sanitize();
            return Generate(settings, seed, requestedWidth, requestedHeight);
        }

        private MazeGenerationOutput Generate(MazeGeneratorSettings settings, int seed, int requestedWidth,
            int requestedHeight)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            MazeGrid grid = new MazeGrid(settings.Width, settings.Height, settings.Padding);
            MazeRandom random = new MazeRandom(seed);
            IMazeAlgorithm algorithm = MazeAlgorithmRegistry.Get(settings.Algorithm);

            if (grid.RoomCount < algorithm.MinimumRoomCount)
            {
                throw new InvalidOperationException("The '" + algorithm.DisplayName +
                                                    "' algorithm needs at least " + algorithm.MinimumRoomCount +
                                                    " rooms.");
            }

            algorithm.Generate(grid, random);
            MazeOpeningCutter.Cut(grid, settings, random);
            MazeBraider.Braid(grid, settings.BraidFactor, random);

            stopwatch.Stop();

            bool sizeWasAdjusted = settings.SizeWasAdjusted ||
                                   requestedWidth != settings.Width || requestedHeight != settings.Height;

            return new MazeGenerationOutput(grid, seed, algorithm.Id, stopwatch.Elapsed.TotalMilliseconds, sizeWasAdjusted);
        }
    }
}
