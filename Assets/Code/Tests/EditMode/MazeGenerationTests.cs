using Maze.Analysis;
using Maze.Core;
using Maze.Generation;
using Maze.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Maze.Tests
{
    /// <summary>Tests for the generation pipeline, the baked result and its statistics.</summary>
    public sealed class MazeGeneratorCoreTests
    {
        [Test]
        public void GenerateClampsAndRoundsTheRequestedSize()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = 20,
                MazeHeight = 30,
                Padding = 1,
            };

            MazeGenerationOutput output = core.Generate(settings, 1);

            Assert.AreEqual(21, output.Grid.Width);
            Assert.AreEqual(31, output.Grid.Height);
            Assert.IsTrue(output.SizeWasAdjusted, "the result should report the adjusted size");
            Assert.IsTrue(settings.SizeWasAdjusted, "the settings should report the adjusted size");
            Assert.AreEqual(20, settings.MazeWidth, "the requested size is kept, Width is the effective one");
            Assert.AreEqual(21, settings.Width);
        }

        [Test]
        public void GenerateIsDeterministicForASeed()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings { MazeWidth = 31, MazeHeight = 31 };

            MazeGrid first = core.Generate(settings, 31415).Grid;
            MazeGrid second = core.Generate(settings, 31415).Grid;

            Assert.AreEqual(first.ComputeFingerprint(), second.ComputeFingerprint());
            Assert.AreEqual(31415, core.Generate(settings, 31415).Seed);
        }

        [Test]
        public void GenerateReportsTimingAndAlgorithm()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGenerationOutput output = core.Generate(new MazeGeneratorSettings { MazeWidth = 21, MazeHeight = 21 },
                7);

            Assert.AreEqual("prim", output.AlgorithmId);
            Assert.GreaterOrEqual(output.Milliseconds, 0.0);
            Assert.AreEqual(output.Grid.Width * output.Grid.Height, output.Grid.WallCount + output.Grid.PassageCount);
        }

        [Test]
        public void NullSettingsAreRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => new MazeGeneratorCore().Generate(null));
        }

        [Test]
        public void BakerProducesSpawnExitAndSolution()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = 31,
                MazeHeight = 31,
                CellSize = 5f,
                WallHeight = 4f,
                OpeningMode = MazeOpeningMode.FixedEntranceAndExit,
            };

            MazeGenerationOutput output = core.Generate(settings, 42);
            MazeResult result = MazeBaker.Bake(output.Grid, settings, output.AlgorithmId, output.Seed,
                output.Milliseconds, true);

            Assert.IsNotNull(result.Grid);
            Assert.IsNotNull(result.MeshData.Mesh);
            Assert.IsTrue(result.HasOpenings);
            Assert.IsTrue(result.HasSolution, "a maze with two openings must have a solution path");
            Assert.Greater(result.SolutionPath.Count, 1);

            // The spawn sits in the entrance room's cell.
            Vector2Int spawnCell = result.WorldToCell(result.SpawnPosition);
            Assert.IsTrue(result.Grid.IsPassage(spawnCell.x, spawnCell.y), "the spawn is not inside a passage");

            // The exit is a different room and reachable.
            Vector2Int exitCell = result.WorldToCell(result.ExitPosition);
            Assert.AreNotEqual(spawnCell, exitCell);
            Assert.IsNotNull(MazePathfinder.FindPath(result.Grid, spawnCell, exitCell));

            // The statistics agree with the grid.
            Assert.AreEqual(result.Grid.Width, result.Statistics.Width);
            Assert.AreEqual(result.Grid.WallCount, result.Statistics.WallCount);
            Assert.AreEqual(result.Grid.PassageCount, result.Statistics.PassageCount);
            Assert.IsTrue(result.Statistics.IsFullyConnected);
            Assert.AreEqual(result.SolutionPath.Count, result.Statistics.SolutionPathLength);
        }

        [Test]
        public void BakerHandlesAClosedMaze()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = 21,
                MazeHeight = 21,
                OpeningMode = MazeOpeningMode.None,
            };

            MazeGenerationOutput output = core.Generate(settings, 5);
            MazeResult result = MazeBaker.Bake(output.Grid, settings, output.AlgorithmId, output.Seed,
                output.Milliseconds, false);

            Assert.IsFalse(result.HasOpenings);
            Assert.IsTrue(result.HasSolution, "even a closed maze gets a route to the farthest room");
            Assert.IsFalse(result.MeshData.HasFloor);
        }

        [Test]
        public void StatisticsDescribeAPerfectMaze()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = 21,
                MazeHeight = 21,
                BraidFactor = 0f,
                OpeningMode = MazeOpeningMode.None,
            };

            MazeGenerationOutput output = core.Generate(settings, 3);
            MazeStatistics statistics = MazeStatistics.Compute(output.Grid, output.AlgorithmId, output.Seed, 0);

            Assert.IsTrue(statistics.IsPerfect, "braid factor 0 must produce a tree");
            Assert.IsTrue(statistics.IsFullyConnected);
            Assert.AreEqual(output.Grid.RoomCount - 1, statistics.CorridorCount);
            Assert.Greater(statistics.DeadEndCount, 0);
            Assert.Greater(statistics.JunctionCount, 0);
            StringAssert.Contains("Perfect maze", statistics.ToSummary());
        }

        [Test]
        public void ReferenceForTheOriginalLimitation()
        {
            // The original implementation picked the start cell with an even index and therefore
            // stranded a whole wall row/column. The room lattice always uses padding + even offsets,
            // so every lane is reachable: run a large sample and require zero stranded lanes.
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings { MazeWidth = 21, MazeHeight = 21 };

            for (int seed = 0; seed < 50; seed++)
            {
                MazeGrid grid = core.Generate(settings, seed).Grid;
                for (int x = grid.Padding; x < grid.Width - grid.Padding; x++)
                {
                    bool stranded = true;
                    for (int y = grid.Padding; y < grid.Height - grid.Padding && stranded; y++)
                    {
                        stranded = grid.IsWall(x, y);
                    }

                    Assert.IsFalse(stranded, "seed " + seed + " stranded column " + x);
                }
            }
        }
    }

    /// <summary>Tests for the mesh builder: budget, index format, submeshes and face winding.</summary>
    public sealed class MazeMeshBuilderTests
    {
        [Test]
        public void MeshHasWallAndFloorSubmeshes()
        {
            MazeGrid grid = BuildSimpleGrid();
            MazeMeshData data = MazeMeshBuilder.Build(grid, 5f, 5f, true, Vector3.zero);

            Assert.IsNotNull(data.Mesh);
            Assert.AreEqual(2, data.Mesh.subMeshCount);
            Assert.Greater(data.WallQuadCount, 0);
            Assert.AreEqual(grid.PassageCount, data.FloorQuadCount);
            Assert.AreEqual(data.WallQuadCount * 2 + data.FloorQuadCount * 2, data.TriangleCount);
            Assert.IsTrue(data.HasFloor);
        }

        [Test]
        public void MeshWithoutFloorHasOneSubmesh()
        {
            MazeGrid grid = BuildSimpleGrid();
            MazeMeshData data = MazeMeshBuilder.Build(grid, 5f, 5f, false, Vector3.zero);

            Assert.AreEqual(1, data.Mesh.subMeshCount);
            Assert.AreEqual(0, data.FloorQuadCount);
            Assert.IsFalse(data.HasFloor);
        }

        [Test]
        public void FacesAreCulled()
        {
            // A 7x7 grid: any wall cell that is surrounded by walls must not emit side faces.
            MazeGrid grid = new MazeGrid(7, 7);
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    grid.SetWall(x, y);
                }
            }

            MazeMeshData data = MazeMeshBuilder.Build(grid, 5f, 5f, false, Vector3.zero);

            // Only the top faces of the 49 cells plus the side faces of the border ring.
            int borderSideFaces = 4 * 7;
            Assert.AreEqual(49 + borderSideFaces, data.WallQuadCount);
            Assert.AreEqual(data.WallQuadCount * 4, data.VertexCount);
        }

        [Test]
        public void LargeMeshesUseA32BitIndexBuffer()
        {
            MazeGrid grid = new MazeGrid(MazeGrid.MaximumDimension, MazeGrid.MaximumDimension);
            MazeMeshData data = MazeMeshBuilder.Build(grid, 5f, 5f, true, Vector3.zero);

            Assert.Greater(data.VertexCount, 65000);
            Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt32, data.Mesh.indexFormat);
        }

        [Test]
        public void WindingFacesOutwards()
        {
            MazeGrid grid = BuildSimpleGrid();
            float cellSize = 5f;
            float wallHeight = 4f;
            MazeMeshData data = MazeMeshBuilder.Build(grid, cellSize, wallHeight, false, Vector3.zero);

            Vector3[] vertices = data.Mesh.vertices;
            int[] triangles = data.Mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Vector3 centroid = (a + b + c) / 3f;

                if (Mathf.Abs(a.y - wallHeight) < 0.001f && Mathf.Abs(b.y - wallHeight) < 0.001f &&
                    Mathf.Abs(c.y - wallHeight) < 0.001f &&
                    Mathf.Abs(Mathf.Abs(normal.y) - 1f) < 0.001f)
                {
                    Assert.Greater(normal.y, 0f, "the top face is not facing up");
                    continue;
                }

                // A side face must look at the passage (or the outside), never into a wall.
                Vector3 expected = ExpectedSideDirection(grid, new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z),
                    new Vector3(c.x, 0f, c.z), normal, centroid, cellSize);
                Assert.Greater(Vector3.Dot(normal, expected), 0.9f,
                    "side face at " + centroid + " points " + normal + " instead of " + expected);
            }
        }

        /// <summary>Determines which way a side face at that position has to look.</summary>
        private static Vector3 ExpectedSideDirection(MazeGrid grid, Vector3 a, Vector3 b, Vector3 c, Vector3 normal,
            Vector3 centroid, float cellSize)
        {
            bool alongX = Mathf.Abs(normal.x) > Mathf.Abs(normal.z);
            float plane = alongX ? a.x : a.z;
            int lowIndex = Mathf.FloorToInt(plane / cellSize);
            int highIndex = lowIndex + 1;

            if (alongX)
            {
                int z = Mathf.RoundToInt(centroid.z / cellSize);
                bool lowIsWall = grid.IsWall(lowIndex, z);
                bool highIsWall = grid.IsWall(highIndex, z);
                Assert.IsTrue(lowIsWall ^ highIsWall,
                    "the face at " + plane + " is not between a wall and a passage");
                return lowIsWall ? Vector3.right : Vector3.left;
            }

            int x = Mathf.RoundToInt(centroid.x / cellSize);
            bool lowIsWallZ = grid.IsWall(x, lowIndex);
            bool highIsWallZ = grid.IsWall(x, highIndex);
            Assert.IsTrue(lowIsWallZ ^ highIsWallZ,
                "the face at " + plane + " is not between a wall and a passage");
            return lowIsWallZ ? Vector3.forward : Vector3.back;
        }

        [Test]
        public void EstimateMatchesTheBuiltMesh()
        {
            MazeGrid grid = BuildSimpleGrid();
            MazeMeshData data = MazeMeshBuilder.Build(grid, 5f, 5f, true, Vector3.zero);
            Assert.AreEqual(data.TriangleCount, MazeMeshBuilder.EstimateTriangleCount(grid, true));
        }

        private static MazeGrid BuildSimpleGrid()
        {
            MazeGrid grid = new MazeGrid(21, 21);
            for (int y = 0; y < grid.RoomCountY; y++)
            {
                for (int x = 0; x < grid.RoomCountX; x++)
                {
                    grid.CarveRoom(x, y);
                    if (x + 1 < grid.RoomCountX)
                    {
                        grid.CarveCorridor(x, y, MazeDirection.East);
                    }
                }
            }

            return grid;
        }
    }
}
