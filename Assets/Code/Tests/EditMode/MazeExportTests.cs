using Maze.Core;
using Maze.Generation;
using Maze.Tools;
using NUnit.Framework;
using UnityEngine;

namespace Maze.Tests
{
    /// <summary>Tests for the ASCII and JSON export/import round trips.</summary>
    public sealed class MazeExportTests
    {
        [Test]
        public void AsciiContainsWallsAndPassages()
        {
            MazeGrid grid = GenerateGrid(21, 3);
            string ascii = MazeAsciiExporter.ToAscii(grid);

            StringAssert.Contains("#", ascii);
            StringAssert.Contains(".", ascii);
            Assert.AreEqual(grid.Height, ascii.Split('\n').Length);
        }

        [Test]
        public void AsciiCanMarkTheSolution()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = 21,
                MazeHeight = 21,
                OpeningMode = MazeOpeningMode.FixedEntranceAndExit,
            };

            MazeGenerationOutput output = core.Generate(settings, 12);
            System.Collections.Generic.List<Vector2Int> path = Maze.Analysis.MazePathfinder.FindPath(output.Grid,
                output.Grid.Openings[0].BorderCell, output.Grid.Openings[1].BorderCell);

            string ascii = MazeAsciiExporter.ToAscii(output.Grid, path);
            StringAssert.Contains("S", ascii);
            StringAssert.Contains("E", ascii);
            StringAssert.Contains("o", ascii);
        }

        [Test]
        public void JsonRoundTripKeepsTheSettings()
        {
            MazeGeneratorSettings original = new MazeGeneratorSettings
            {
                MazeWidth = 41,
                MazeHeight = 21,
                Algorithm = MazeAlgorithmKind.DepthFirst,
                UseRandomSeed = false,
                Seed = 987654,
                BraidFactor = 0.35f,
                OpeningMode = MazeOpeningMode.Random,
                RandomOpeningCount = 3,
                EntranceSide = MazeDirection.West,
                ExitSide = MazeDirection.East,
                CellSize = 3f,
                WallHeight = 7f,
                GenerateFloor = false,
            };
            original.Sanitize();

            string json = MazeJsonExport.SettingsToJson(original);

            MazeGeneratorSettings parsed;
            Assert.IsTrue(MazeJsonExport.TryParseSettings(json, out parsed));
            Assert.AreEqual(original.MazeWidth, parsed.MazeWidth);
            Assert.AreEqual(original.MazeHeight, parsed.MazeHeight);
            Assert.AreEqual(original.Algorithm, parsed.Algorithm);
            Assert.AreEqual(original.Seed, parsed.Seed);
            Assert.AreEqual(original.BraidFactor, parsed.BraidFactor, 0.0001f);
            Assert.AreEqual(original.OpeningMode, parsed.OpeningMode);
            Assert.AreEqual(original.RandomOpeningCount, parsed.RandomOpeningCount);
            Assert.AreEqual(original.EntranceSide, parsed.EntranceSide);
            Assert.AreEqual(original.ExitSide, parsed.ExitSide);
            Assert.AreEqual(original.CellSize, parsed.CellSize, 0.0001f);
            Assert.AreEqual(original.WallHeight, parsed.WallHeight, 0.0001f);
            Assert.AreEqual(original.GenerateFloor, parsed.GenerateFloor);
        }

        [Test]
        public void JsonDocumentCarriesTheGrid()
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings { MazeWidth = 21, MazeHeight = 21 };
            MazeGenerationOutput output = core.Generate(settings, 21);
            MazeResult result = MazeBaker.Bake(output.Grid, settings, output.AlgorithmId, output.Seed,
                output.Milliseconds, true);

            string json = MazeJsonExport.ToJson(result, settings);
            StringAssert.Contains("\"version\": 1", json);
            StringAssert.Contains("\"seed\": 21", json);

            int seed;
            string algorithm;
            int width;
            int height;
            Assert.IsTrue(MazeJsonExport.TryParseIdentity(json, out seed, out algorithm, out width, out height));
            Assert.AreEqual(21, seed);
            Assert.AreEqual("prim", algorithm);
            Assert.AreEqual(21, width);
            Assert.AreEqual(21, height);
        }

        [Test]
        public void GridFromRowsRebuildsTheSameMaze()
        {
            MazeGrid grid = GenerateGrid(21, 8);
            string[] rows = grid.ToAscii().Split('\n');

            MazeGrid rebuilt = MazeJsonExport.GridFromRows(rows, grid.Padding);
            Assert.IsNotNull(rebuilt);
            Assert.AreEqual(grid.ComputeFingerprint(), rebuilt.ComputeFingerprint());
        }

        [Test]
        public void MalformedDocumentsAreRejected()
        {
            MazeGeneratorSettings settings;
            Assert.IsFalse(MazeJsonExport.TryParseSettings(null, out settings));
            Assert.IsFalse(MazeJsonExport.TryParseSettings("{ \"version\": 1 }", out settings));
            Assert.IsNull(MazeJsonExport.GridFromRows(new[] { "###", "#" }, 1));
            Assert.IsNull(MazeJsonExport.GridFromRows(null, 1));
        }

        private static MazeGrid GenerateGrid(int size, int seed)
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            return core.Generate(new MazeGeneratorSettings
            {
                MazeWidth = size,
                MazeHeight = size,
                OpeningMode = MazeOpeningMode.None,
            }, seed).Grid;
        }
    }
}
