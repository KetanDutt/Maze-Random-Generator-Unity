using System.Collections.Generic;
using Maze.Core;
using NUnit.Framework;
using UnityEngine;

namespace Maze.Tests
{
    /// <summary>Tests for the deterministic random generator and the grid model.</summary>
    public sealed class MazeRandomTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            MazeRandom first = new MazeRandom(1234);
            MazeRandom second = new MazeRandom(1234);

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(first.NextUInt(), second.NextUInt(), "step " + i);
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentSequences()
        {
            MazeRandom first = new MazeRandom(1);
            MazeRandom second = new MazeRandom(2);

            bool differs = false;
            for (int i = 0; i < 50 && !differs; i++)
            {
                differs = first.NextUInt() != second.NextUInt();
            }

            Assert.IsTrue(differs, "seeds 1 and 2 produced the same sequence");
        }

        [Test]
        public void RangeStaysInBounds()
        {
            MazeRandom random = new MazeRandom(7);
            for (int i = 0; i < 10000; i++)
            {
                int value = random.Range(-5, 13);
                Assert.GreaterOrEqual(value, -5);
                Assert.Less(value, 13);
            }
        }

        [Test]
        public void NextIndexHandlesDegenerateCounts()
        {
            MazeRandom random = new MazeRandom(7);
            Assert.AreEqual(0, random.NextIndex(0));
            Assert.AreEqual(0, random.NextIndex(1));
        }

        [Test]
        public void ChanceHonoursTheExtremes()
        {
            MazeRandom random = new MazeRandom(3);
            Assert.IsFalse(random.Chance(0f));
            Assert.IsTrue(random.Chance(1f));
        }

        [Test]
        public void ShuffleOnlyReorders()
        {
            List<int> values = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                values.Add(i);
            }

            new MazeRandom(11).Shuffle(values);

            Assert.AreEqual(100, values.Count);
            HashSet<int> unique = new HashSet<int>(values);
            Assert.AreEqual(100, unique.Count, "shuffle duplicated or dropped items");
        }
    }

    /// <summary>Tests for the maze grid: geometry, mutation and hashing.</summary>
    public sealed class MazeGridTests
    {
        [TestCase(21, 1, 21)]
        [TestCase(20, 1, 21)]
        [TestCase(5, 1, 5)]
        [TestCase(3, 1, 5)]
        [TestCase(10000, 1, MazeGrid.MaximumDimension)]
        [TestCase(30, 2, 31)]
        public void NormalizeDimensionReturnsOddSizes(int requested, int padding, int expected)
        {
            Assert.AreEqual(expected, MazeGrid.NormalizeDimension(requested, padding));
        }

        [Test]
        public void NormalizedInteriorIsAlwaysOddAndLargeEnough()
        {
            for (int requested = 1; requested < 120; requested++)
            {
                for (int padding = 1; padding <= 3; padding++)
                {
                    int normalized = MazeGrid.NormalizeDimension(requested, padding);
                    Assert.AreEqual(0, (normalized - 2 * padding) % 2, "interior parity for " + requested);
                    Assert.GreaterOrEqual(normalized - 2 * padding, MazeGrid.MinimumInteriorSize);
                }
            }
        }

        [Test]
        public void InvalidSizesAreRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MazeGrid(4, 4, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MazeGrid(21, 21, 0));
        }

        [Test]
        public void FreshGridIsSolid()
        {
            MazeGrid grid = new MazeGrid(21, 21);
            Assert.AreEqual(21 * 21, grid.WallCount);
            Assert.AreEqual(0, grid.PassageCount);
            Assert.IsTrue(grid.IsWall(0, 0));
            Assert.IsFalse(grid.IsPassage(10, 10));
        }

        [Test]
        public void OutOfBoundsCountsAsWall()
        {
            MazeGrid grid = new MazeGrid(21, 21);
            Assert.IsTrue(grid.IsWall(-1, 5));
            Assert.IsTrue(grid.IsWall(21, 5));
            Assert.IsFalse(grid.IsPassage(-1, 5));
            Assert.IsFalse(grid.InBounds(-1, 5));
        }

        [Test]
        public void RoomLatticeMatchesThePadding()
        {
            for (int padding = 1; padding <= 3; padding++)
            {
                int size = MazeGrid.NormalizeDimension(25, padding);
                MazeGrid grid = new MazeGrid(size, size, padding);
                Assert.AreEqual((size - 2 * padding + 1) / 2, grid.RoomCountX);
                Assert.AreEqual(new Vector2Int(padding, padding), grid.CellOfRoom(0, 0));
                Assert.IsTrue(grid.IsRoomCell(padding, padding));
                Assert.IsFalse(grid.IsRoomCell(padding + 1, padding));
                Assert.AreEqual(new Vector2Int(0, 0), grid.RoomOfCell(padding, padding));
            }
        }

        [Test]
        public void CarveCorridorCarvesBothRoomsAndTheCellInBetween()
        {
            MazeGrid grid = new MazeGrid(21, 21);
            grid.CarveCorridor(2, 3, MazeDirection.East);

            Assert.IsTrue(grid.IsPassage(4, 6), "origin room");
            Assert.IsTrue(grid.IsPassage(5, 6), "corridor cell");
            Assert.IsTrue(grid.IsPassage(6, 6), "destination room");
            Assert.AreEqual(1, grid.RoomDegree(2, 3));
            Assert.AreEqual(1, grid.RoomDegree(3, 3));
            Assert.IsTrue(grid.IsCorridorCarved(3, 3, MazeDirection.West));
        }

        [Test]
        public void CarveCorridorIgnoresOutOfBoundsDirections()
        {
            MazeGrid grid = new MazeGrid(21, 21);
            grid.CarveCorridor(0, 0, MazeDirection.West);
            grid.CarveCorridor(grid.RoomCountX - 1, 0, MazeDirection.East);
            grid.CarveCorridor(0, grid.RoomCountY - 1, MazeDirection.North);

            Assert.AreEqual(0, grid.PassageCount, "corridors outside the room lattice must be ignored");
            Assert.AreEqual(0, grid.RoomDegree(0, 0));
        }

        [Test]
        public void FingerprintDependsOnTheCells()
        {
            MazeGrid first = new MazeGrid(21, 21);
            MazeGrid second = new MazeGrid(21, 21);
            Assert.AreEqual(first.ComputeFingerprint(), second.ComputeFingerprint());

            first.Carve(5, 5);
            Assert.AreNotEqual(first.ComputeFingerprint(), second.ComputeFingerprint());

            second.Carve(5, 5);
            Assert.AreEqual(first.ComputeFingerprint(), second.ComputeFingerprint());
        }

        [Test]
        public void CloneIsIndependent()
        {
            MazeGrid grid = new MazeGrid(21, 21);
            grid.Carve(3, 3);
            MazeGrid clone = grid.Clone();

            Assert.AreEqual(grid.ComputeFingerprint(), clone.ComputeFingerprint());
            clone.Carve(9, 9);
            Assert.IsTrue(grid.IsWall(9, 9));
            Assert.IsTrue(clone.IsPassage(9, 9));
        }

        [Test]
        public void AsciiRendersWallsAndPassages()
        {
            MazeGrid grid = new MazeGrid(7, 7);
            grid.Carve(3, 3);
            string ascii = grid.ToAscii();

            string[] lines = ascii.Split('\n');
            Assert.AreEqual(7, lines.Length);
            Assert.AreEqual("#######", lines[0]);
            Assert.AreEqual("###.###", lines[3]);
        }
    }

    /// <summary>Tests for the settings object: sanitising, clamping and seeding.</summary>
    public sealed class MazeSettingsTests
    {
        [Test]
        public void SanitizeMakesSizesOddAndInRange()
        {
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = 20,
                MazeHeight = 3,
                Padding = 9,
                CellSize = 1000f,
                WallHeight = 0f,
                BraidFactor = 5f,
                RandomOpeningCount = 12,
            };

            settings.Sanitize();

            Assert.AreEqual(20, settings.MazeWidth, "the requested size is kept for transparency");
            Assert.AreEqual(21, settings.Width, "the effective size is rounded to an odd interior");
            Assert.AreEqual(3, settings.MazeHeight);
            Assert.AreEqual(5, settings.Height);
            Assert.IsTrue(settings.SizeWasAdjusted);
            Assert.AreEqual(4, settings.Padding);
            Assert.AreEqual(50f, settings.CellSize);
            Assert.AreEqual(1f, settings.WallHeight);
            Assert.AreEqual(1f, settings.BraidFactor);
            Assert.AreEqual(4, settings.RandomOpeningCount);
        }

        [Test]
        public void SanitizeSeparatesEntranceAndExit()
        {
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                OpeningMode = MazeOpeningMode.FixedEntranceAndExit,
                EntranceSide = MazeDirection.North,
                ExitSide = MazeDirection.North,
            };

            settings.Sanitize();
            Assert.AreNotEqual(settings.EntranceSide, settings.ExitSide);
        }

        [Test]
        public void ResolveSeedIsStableForFixedSeeds()
        {
            MazeGeneratorSettings settings = new MazeGeneratorSettings { UseRandomSeed = false, Seed = 99 };
            Assert.AreEqual(99, settings.ResolveSeed());
            Assert.AreEqual(99, settings.ResolveSeed());
        }

        [Test]
        public void CreatedSeedsAreRandomEnough()
        {
            HashSet<int> seeds = new HashSet<int>();
            for (int i = 0; i < 500; i++)
            {
                seeds.Add(MazeRandom.CreateSeed());
            }

            Assert.Greater(seeds.Count, 400, "random seeds are colliding too often");
        }

        [Test]
        public void DirectionHelpersAreConsistent()
        {
            foreach (MazeDirection direction in MazeDirections.Values)
            {
                Assert.AreEqual(direction, direction.Opposite().Opposite());
                Assert.AreEqual(direction, MazeDirections.FromOffset(direction.ToOffset().x, direction.ToOffset().y));

                MazeDirection parsed;
                Assert.IsTrue(MazeDirections.TryParse(direction.ToDisplayName(), out parsed));
                Assert.AreEqual(direction, parsed);
            }

            MazeDirection north;
            Assert.IsTrue(MazeDirections.TryParse("n", out north));
            Assert.AreEqual(MazeDirection.North, north);
            Assert.IsTrue(MazeDirections.TryParse("west", out north));
            Assert.AreEqual(MazeDirection.West, north);
            Assert.IsFalse(MazeDirections.TryParse("sideways", out north));
            Assert.IsFalse(MazeDirections.TryParse(null, out north));
        }
    }
}
