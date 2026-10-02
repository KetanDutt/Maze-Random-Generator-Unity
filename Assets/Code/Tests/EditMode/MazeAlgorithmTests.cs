using System.Collections.Generic;
using Maze.Algorithms;
using Maze.Analysis;
using Maze.Core;
using Maze.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Maze.Tests
{
    /// <summary>
    /// Structural tests for every algorithm. These are the invariants that make a maze usable:
    /// every room carved, fully connected, no ambiguous 2x2 blocks and no stranded wall row/column.
    /// </summary>
    public sealed class MazeAlgorithmTests
    {
        private static IEnumerable<TestCaseData> AlgorithmCases()
        {
            int[] sizes = { 7, 21, 41 };
            int[] seeds = { 0, 1, 4242 };
            foreach (MazeAlgorithmKind algorithm in System.Enum.GetValues(typeof(MazeAlgorithmKind)))
            {
                foreach (int size in sizes)
                {
                    foreach (int seed in seeds)
                    {
                        yield return new TestCaseData(algorithm, size, seed)
                            .SetName("Generate_" + algorithm + "_" + size + "x" + size + "_seed" + seed);
                    }
                }
            }
        }

        [TestCaseSource(nameof(AlgorithmCases))]
        public void GenerateProducesValidMaze(MazeAlgorithmKind algorithm, int size, int seed)
        {
            MazeGrid grid = Generate(algorithm, size, size, seed);

            // 1. Every room is carved.
            foreach (Vector2Int room in grid.EnumerateRooms())
            {
                Assert.IsTrue(grid.IsRoomCarved(room.x, room.y),
                    algorithm + ": room " + room + " was never carved (seed " + seed + ")");
            }

            // 2. The maze is a tree: rooms + (rooms - 1) corridors is the only way every connection
            //    is a keep (Kruskal) or a fresh carve (Prim/DFS).
            Assert.AreEqual(2 * grid.RoomCount - 1, grid.PassageCount,
                algorithm + ": expected a perfect maze (seed " + seed + ")");

            // 3. Fully connected.
            Assert.IsTrue(MazePathfinder.IsFullyConnected(grid),
                algorithm + ": maze is not fully connected (seed " + seed + ")");

            // 4. No 2x2 open block - the maze would look like a room instead of corridors.
            for (int y = 0; y < grid.Height - 1; y++)
            {
                for (int x = 0; x < grid.Width - 1; x++)
                {
                    bool block = grid.IsPassage(x, y) && grid.IsPassage(x + 1, y) &&
                                 grid.IsPassage(x, y + 1) && grid.IsPassage(x + 1, y + 1);
                    Assert.IsFalse(block, algorithm + ": 2x2 passage block at " + x + "," + y);
                }
            }

            // 5. No interior row or column consists of walls only. This is the bug of the original
            //    implementation that the room lattice fixes.
            for (int x = grid.Padding; x < grid.Width - grid.Padding; x++)
            {
                bool allWalls = true;
                for (int y = grid.Padding; y < grid.Height - grid.Padding && allWalls; y++)
                {
                    allWalls = grid.IsWall(x, y);
                }

                Assert.IsFalse(allWalls, algorithm + ": stranded wall column " + x + " (seed " + seed + ")");
            }

            for (int y = grid.Padding; y < grid.Height - grid.Padding; y++)
            {
                bool allWalls = true;
                for (int x = grid.Padding; x < grid.Width - grid.Padding && allWalls; x++)
                {
                    allWalls = grid.IsWall(x, y);
                }

                Assert.IsFalse(allWalls, algorithm + ": stranded wall row " + y + " (seed " + seed + ")");
            }
        }

        [Test]
        public void SameSeedProducesIdenticalMazes()
        {
            foreach (MazeAlgorithmKind algorithm in System.Enum.GetValues(typeof(MazeAlgorithmKind)))
            {
                MazeGrid first = Generate(algorithm, 41, 41, 777);
                MazeGrid second = Generate(algorithm, 41, 41, 777);
                Assert.AreEqual(first.ComputeFingerprint(), second.ComputeFingerprint(),
                    algorithm + ": the same seed produced a different maze");
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentMazes()
        {
            foreach (MazeAlgorithmKind algorithm in System.Enum.GetValues(typeof(MazeAlgorithmKind)))
            {
                HashSet<ulong> fingerprints = new HashSet<ulong>();
                for (int seed = 0; seed < 8; seed++)
                {
                    fingerprints.Add(Generate(algorithm, 41, 41, seed).ComputeFingerprint());
                }

                Assert.Greater(fingerprints.Count, 6, algorithm + ": seeds are not producing distinct mazes");
            }
        }

        [Test]
        public void AlgorithmsProduceDifferentMazes()
        {
            HashSet<ulong> fingerprints = new HashSet<ulong>();
            foreach (MazeAlgorithmKind algorithm in System.Enum.GetValues(typeof(MazeAlgorithmKind)))
            {
                fingerprints.Add(Generate(algorithm, 41, 41, 2024).ComputeFingerprint());
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(MazeAlgorithmKind)).Length, fingerprints.Count,
                "two algorithms produced the exact same maze");
        }

        [Test]
        public void RegistryCoversEveryEnumValue()
        {
            foreach (MazeAlgorithmKind algorithm in System.Enum.GetValues(typeof(MazeAlgorithmKind)))
            {
                IMazeAlgorithm implementation = MazeAlgorithmRegistry.Get(algorithm);
                Assert.IsNotNull(implementation);
                Assert.IsNotEmpty(implementation.Id);
                Assert.IsNotEmpty(implementation.DisplayName);
                Assert.IsNotEmpty(implementation.Description);

                MazeAlgorithmKind parsed;
                Assert.IsTrue(MazeAlgorithmRegistry.TryParse(implementation.Id, out parsed));
                Assert.AreEqual(algorithm, parsed);
            }
        }

        private static MazeGrid Generate(MazeAlgorithmKind algorithm, int width, int height, int seed)
        {
            MazeGrid grid = new MazeGrid(width, height);
            MazeAlgorithmRegistry.Get(algorithm).Generate(grid, new MazeRandom(seed));
            Assert.AreEqual(0, grid.Openings.Count, "algorithms must not create openings on their own");
            return grid;
        }
    }

    /// <summary>Tests for the openings (entrances/exits) and the braiding post processing.</summary>
    public sealed class MazeOpeningAndBraidTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RandomOpeningsAreCutCorrectly(int count)
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = 31,
                MazeHeight = 31,
                OpeningMode = MazeOpeningMode.Random,
                RandomOpeningCount = count,
                BraidFactor = 0f,
            };

            MazeGenerationOutput output = core.Generate(settings, 12);
            MazeGrid grid = output.Grid;

            Assert.AreEqual(count, grid.Openings.Count);
            foreach (MazeOpening opening in grid.Openings)
            {
                Vector2Int border = opening.BorderCell;
                bool onBorder = border.x == 0 || border.y == 0 || border.x == grid.Width - 1 ||
                                border.y == grid.Height - 1;
                Assert.IsTrue(onBorder, "opening cell " + border + " is not on the maze border");
                Assert.IsTrue(grid.IsPassage(border.x, border.y));

                int passageNeighbours = 0;
                foreach (MazeDirection direction in MazeDirections.Values)
                {
                    Vector2Int offset = direction.ToOffset();
                    if (grid.IsPassage(border.x + offset.x, border.y + offset.y))
                    {
                        passageNeighbours++;
                    }
                }

                Assert.AreEqual(1, passageNeighbours, "an opening must be exactly one cell wide");
            }

            Assert.IsTrue(MazePathfinder.IsFullyConnected(grid), "openings must not disconnect the maze");
        }

        [Test]
        public void FixedOpeningsAreOnTheRequestedSides()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = 31,
                MazeHeight = 31,
                OpeningMode = MazeOpeningMode.FixedEntranceAndExit,
                EntranceSide = MazeDirection.North,
                ExitSide = MazeDirection.South,
                BraidFactor = 0f,
            };

            MazeGrid grid = core.Generate(settings, 5).Grid;
            Assert.AreEqual(2, grid.Openings.Count);
            Assert.AreEqual(MazeDirection.North, grid.Openings[0].Side);
            Assert.AreEqual(MazeDirection.South, grid.Openings[1].Side);
            Assert.AreEqual(grid.Width / 2, grid.Openings[0].BorderCell.x, "fixed openings use the middle");

            List<Vector2Int> path = MazePathfinder.FindPath(grid, grid.Openings[0].BorderCell,
                grid.Openings[1].BorderCell);
            Assert.IsNotNull(path, "the entrance must be connected to the exit");
            Assert.Greater(path.Count, 1);
        }

        [Test]
        public void ClosedMazeHasNoOpenings()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGrid grid = core.Generate(new MazeGeneratorSettings
            {
                MazeWidth = 21,
                MazeHeight = 21,
                OpeningMode = MazeOpeningMode.None,
            }, 3).Grid;

            Assert.AreEqual(0, grid.Openings.Count);
            for (int x = 0; x < grid.Width; x++)
            {
                Assert.IsTrue(grid.IsWall(x, 0));
                Assert.IsTrue(grid.IsWall(x, grid.Height - 1));
            }
        }

        [Test]
        public void BraidingRemovesDeadEndsAndKeepsTheMazeConnected()
        {
            foreach (MazeAlgorithmKind algorithm in System.Enum.GetValues(typeof(MazeAlgorithmKind)))
            {
                MazeGrid perfect = Generate(algorithm, 41, 41, 99, 0f);
                MazeGrid braided = Generate(algorithm, 41, 41, 99, 1f);

                int perfectDeadEnds = MazePathfinder.CountDeadEnds(perfect);
                Assert.Greater(perfectDeadEnds, 0, algorithm + ": a perfect maze should have dead ends");
                Assert.AreEqual(0, MazePathfinder.CountDeadEnds(braided),
                    algorithm + ": braid factor 1 must remove every dead end");
                Assert.IsTrue(MazePathfinder.IsFullyConnected(braided));

                MazeGrid half = Generate(algorithm, 41, 41, 99, 0.5f);
                Assert.Less(MazePathfinder.CountDeadEnds(half), perfectDeadEnds,
                    algorithm + ": partial braiding should remove some dead ends");
            }
        }

        [Test]
        public void BraidFactorZeroIsANoOp()
        {
            MazeGrid plain = Generate(MazeAlgorithmKind.RandomizedPrim, 31, 31, 8, 0f);
            MazeGrid explicitZero = Generate(MazeAlgorithmKind.RandomizedPrim, 31, 31, 8, 0f);
            Assert.AreEqual(plain.ComputeFingerprint(), explicitZero.ComputeFingerprint());
        }

        private static MazeGrid Generate(MazeAlgorithmKind algorithm, int width, int height, int seed,
            float braidFactor)
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            return core.Generate(new MazeGeneratorSettings
            {
                MazeWidth = width,
                MazeHeight = height,
                Algorithm = algorithm,
                BraidFactor = braidFactor,
                OpeningMode = MazeOpeningMode.None,
            }, seed).Grid;
        }
    }
}
