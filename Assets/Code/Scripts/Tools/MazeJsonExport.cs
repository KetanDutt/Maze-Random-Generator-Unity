using System;
using System.Text;
using Maze.Algorithms;
using Maze.Core;
using Maze.Generation;
using UnityEngine;

namespace Maze.Tools
{
    /// <summary>
    /// Serialises a maze to JSON and parses it back. The format is intentionally simple and versioned
    /// so seeds and mazes can be shared between machines, imported from a bug report or committed as
    /// a test fixture:
    /// <code>
    /// { "version": 1, "seed": 42, "algorithm": "prim", "width": 21, "height": 21,
    ///   "settings": { ... }, "rows": [ "#.#", "..." ] }
    /// </code>
    /// </summary>
    /// <remarks>
    /// Unity's <see cref="JsonUtility"/> cannot serialise multidimensional arrays, so the grid is
    /// exported as an array of strings, one per row (top row first, <c>#</c> = wall).
    /// </remarks>
    public static class MazeJsonExport
    {
        /// <summary>Version of the document format.</summary>
        public const int CurrentVersion = 1;

        [Serializable]
        private class SettingsDocument
        {
            public int mazeWidth;
            public int mazeHeight;
            public string algorithm;
            public bool useRandomSeed;
            public int seed;
            public float braidFactor;
            public string openingMode;
            public int randomOpeningCount;
            public string entranceSide;
            public string exitSide;
            public int padding;
            public float cellSize;
            public float wallHeight;
            public bool generateFloor;
        }

        [Serializable]
        private class MazeDocument
        {
            public int version;
            public int seed;
            public string algorithm;
            public int width;
            public int height;
            public int padding;
            public int wallCount;
            public int passageCount;
            public int deadEndCount;
            public int solutionLength;
            public bool perfect;
            public bool fullyConnected;
            public string ascii;
            public string[] rows;
            public SettingsDocument settings;
        }

        /// <summary>Serialises a generated maze, optionally including the settings that produced it.</summary>
        public static string ToJson(MazeResult result, MazeGeneratorSettings settings = null)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            MazeGrid grid = result.Grid;
            MazeStatistics statistics = result.Statistics;

            MazeDocument document = new MazeDocument
            {
                version = CurrentVersion,
                seed = statistics.Seed,
                algorithm = statistics.AlgorithmId,
                width = grid.Width,
                height = grid.Height,
                padding = grid.Padding,
                wallCount = statistics.WallCount,
                passageCount = statistics.PassageCount,
                deadEndCount = statistics.DeadEndCount,
                solutionLength = statistics.SolutionPathLength,
                perfect = statistics.IsPerfect,
                fullyConnected = statistics.IsFullyConnected,
                ascii = grid.ToAscii(),
                rows = ToRows(grid),
                settings = settings != null ? ToDocument(settings) : null,
            };

            return JsonUtility.ToJson(document, true);
        }

        /// <summary>Serialises the settings (useful to store a shareable configuration).</summary>
        public static string SettingsToJson(MazeGeneratorSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            return JsonUtility.ToJson(ToDocument(settings), true);
        }

        /// <summary>Reads the settings of a previously exported document.</summary>
        public static bool TryParseSettings(string json, out MazeGeneratorSettings settings)
        {
            settings = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            MazeDocument document;
            try
            {
                document = JsonUtility.FromJson<MazeDocument>(json);
            }
            catch (Exception)
            {
                return false;
            }

            if (document == null || document.settings == null)
            {
                return false;
            }

            SettingsDocument source = document.settings;
            MazeGeneratorSettings parsed = new MazeGeneratorSettings
            {
                MazeWidth = source.mazeWidth,
                MazeHeight = source.mazeHeight,
                UseRandomSeed = source.useRandomSeed,
                Seed = source.seed,
                BraidFactor = source.braidFactor,
                RandomOpeningCount = source.randomOpeningCount,
                Padding = source.padding,
                CellSize = source.cellSize,
                WallHeight = source.wallHeight,
                GenerateFloor = source.generateFloor,
            };

            MazeAlgorithmKind algorithm;
            if (MazeAlgorithmRegistry.TryParse(source.algorithm, out algorithm))
            {
                parsed.Algorithm = algorithm;
            }

            MazeOpeningMode openingMode;
            if (Enum.TryParse(source.openingMode, true, out openingMode))
            {
                parsed.OpeningMode = openingMode;
            }

            MazeDirection direction;
            if (MazeDirections.TryParse(source.entranceSide, out direction))
            {
                parsed.EntranceSide = direction;
            }

            if (MazeDirections.TryParse(source.exitSide, out direction))
            {
                parsed.ExitSide = direction;
            }

            parsed.Sanitize();
            settings = parsed;
            return true;
        }

        /// <summary>Reads the seed and algorithm of a document without rebuilding the grid.</summary>
        public static bool TryParseIdentity(string json, out int seed, out string algorithm, out int width, out int height)
        {
            seed = 0;
            algorithm = string.Empty;
            width = 0;
            height = 0;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                MazeDocument document = JsonUtility.FromJson<MazeDocument>(json);
                if (document == null)
                {
                    return false;
                }

                seed = document.seed;
                algorithm = document.algorithm;
                width = document.width;
                height = document.height;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Rebuilds a grid from exported rows.</summary>
        public static MazeGrid GridFromRows(string[] rows, int padding)
        {
            if (rows == null || rows.Length == 0)
            {
                return null;
            }

            int height = rows.Length;
            int width = rows[0].Length;
            for (int i = 1; i < rows.Length; i++)
            {
                if (rows[i] == null || rows[i].Length != width)
                {
                    return null; // malformed document
                }
            }

            MazeGrid grid;
            try
            {
                grid = new MazeGrid(width, height, padding);
            }
            catch (ArgumentException)
            {
                return null; // the rows do not describe a valid lattice
            }

            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row; // the first exported row is the highest y
                string line = rows[row];
                for (int x = 0; x < width && x < line.Length; x++)
                {
                    if (line[x] == '#')
                    {
                        grid.SetWall(x, y);
                    }
                    else
                    {
                        grid.Carve(x, y);
                    }
                }
            }

            return grid;
        }

        private static string[] ToRows(MazeGrid grid)
        {
            string[] rows = new string[grid.Height];
            StringBuilder builder = new StringBuilder(grid.Width);
            for (int row = 0; row < grid.Height; row++)
            {
                int y = grid.Height - 1 - row;
                builder.Length = 0;
                for (int x = 0; x < grid.Width; x++)
                {
                    builder.Append(grid.IsWall(x, y) ? '#' : '.');
                }

                rows[row] = builder.ToString();
            }

            return rows;
        }

        private static SettingsDocument ToDocument(MazeGeneratorSettings settings)
        {
            return new SettingsDocument
            {
                mazeWidth = settings.MazeWidth,
                mazeHeight = settings.MazeHeight,
                algorithm = MazeAlgorithmRegistry.Get(settings.Algorithm).Id,
                useRandomSeed = settings.UseRandomSeed,
                seed = settings.Seed,
                braidFactor = settings.BraidFactor,
                openingMode = settings.OpeningMode.ToString(),
                randomOpeningCount = settings.RandomOpeningCount,
                entranceSide = settings.EntranceSide.ToDisplayName(),
                exitSide = settings.ExitSide.ToDisplayName(),
                padding = settings.Padding,
                cellSize = settings.CellSize,
                wallHeight = settings.WallHeight,
                generateFloor = settings.GenerateFloor,
            };
        }
    }
}
