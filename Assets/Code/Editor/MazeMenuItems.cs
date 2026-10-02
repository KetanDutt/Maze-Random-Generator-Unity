using System.IO;
using Maze.Core;
using Maze.Generation;
using Maze.Tools;
using UnityEditor;
using UnityEngine;

namespace Maze.EditorTools
{
    /// <summary>
    /// Menu entries and editor utilities: create a generator, regenerate the selected maze,
    /// export/import seed documents and sanity check the whole project.
    /// </summary>
    public static class MazeMenuItems
    {
        private const string Root = "Tools/Maze/";
        private const string LastDirectoryKey = "Maze.LastExportDirectory";

        /// <summary>Creates an empty maze generator object in the active scene.</summary>
        [MenuItem("GameObject/Maze/Maze Generator", false, 10)]
        public static void CreateMazeGenerator(MenuCommand command)
        {
            GameObject existing = Selection.activeGameObject;
            if (existing != null && existing.GetComponent<MazeGenerator>() != null)
            {
                EditorUtility.DisplayDialog("Maze Generator", "The selected object already has a MazeGenerator.",
                    "OK");
                return;
            }

            GameObject gameObject = new GameObject("Maze Generator");
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Maze Generator");
            gameObject.AddComponent<MazeGenerator>();
            Selection.activeGameObject = gameObject;
        }

        /// <summary>Creates a complete demo rig (generator, player, camera, HUD, minimap).</summary>
        [MenuItem(Root + "Create Demo Rig", false, 20)]
        public static void CreateDemoRig()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Maze", "Stop play mode before creating a demo rig.", "OK");
                return;
            }

            if (Object.FindObjectOfType<MazeGenerator>() != null)
            {
                bool create = EditorUtility.DisplayDialog("Maze",
                    "The scene already contains a MazeGenerator. Create another rig anyway?", "Create", "Cancel");
                if (!create)
                {
                    return;
                }
            }

            MazeSceneBuilder.BuildDemoRig();
        }

        [MenuItem(Root + "Generate Selected", false, 40)]
        public static void GenerateSelected()
        {
            MazeGenerator generator = FindSelected();
            if (generator == null)
            {
                return;
            }

            Undo.RecordObject(generator, "Generate Maze");
            generator.Generate();
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }

        [MenuItem(Root + "Regenerate Selected", false, 41)]
        public static void RegenerateSelected()
        {
            MazeGenerator generator = FindSelected();
            if (generator == null)
            {
                return;
            }

            Undo.RecordObject(generator, "Regenerate Maze");
            generator.Regenerate();
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }

        [MenuItem(Root + "Clear Selected", false, 42)]
        public static void ClearSelected()
        {
            MazeGenerator generator = FindSelected();
            if (generator == null)
            {
                return;
            }

            generator.Clear();
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }

        // ---------------------------------------------------------------------
        // Seed documents
        // ---------------------------------------------------------------------

        [MenuItem(Root + "Export Selected Maze (JSON)...", false, 60)]
        public static void ExportSelected()
        {
            MazeGenerator generator = FindSelected();
            if (generator == null || generator.Result == null)
            {
                EditorUtility.DisplayDialog("Export maze", "Select a MazeGenerator with a generated maze.", "OK");
                return;
            }

            string directory = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
            string path = EditorUtility.SaveFilePanel("Export maze JSON", directory,
                "maze-seed-" + generator.LastSeed, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, MazeJsonExport.ToJson(generator.Result, generator.Settings));
            EditorPrefs.SetString(LastDirectoryKey, Path.GetDirectoryName(path) ?? directory);
            Debug.Log("[Maze] Exported " + path);
        }

        [MenuItem(Root + "Import Maze (JSON)...", false, 61)]
        public static void ImportMaze()
        {
            MazeGenerator generator = FindSelected();
            if (generator == null)
            {
                EditorUtility.DisplayDialog("Import maze", "Select a MazeGenerator first.", "OK");
                return;
            }

            string directory = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
            string path = EditorUtility.OpenFilePanel("Import maze JSON", directory, "json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            string json = File.ReadAllText(path);
            MazeGeneratorSettings settings;
            if (!MazeJsonExport.TryParseSettings(json, out settings))
            {
                // Fall back to the seed + algorithm that the document carries.
                int seed;
                string algorithm;
                int width;
                int height;
                if (MazeJsonExport.TryParseIdentity(json, out seed, out algorithm, out width, out height))
                {
                    MazeAlgorithmKind kind;
                    if (MazeAlgorithmRegistry.TryParse(algorithm, out kind))
                    {
                        generator.Settings.Algorithm = kind;
                    }

                    generator.Settings.MazeWidth = width;
                    generator.Settings.MazeHeight = height;
                    generator.GenerateWithSeed(seed);
                    EditorPrefs.SetString(LastDirectoryKey, Path.GetDirectoryName(path) ?? directory);
                    Debug.Log("[Maze] Imported seed " + seed + " from " + path);
                    return;
                }

                EditorUtility.DisplayDialog("Import maze", "That file is not a maze document.", "OK");
                return;
            }

            CopySettings(settings, generator.Settings);
            generator.GenerateWithSeed(settings.Seed);
            EditorPrefs.SetString(LastDirectoryKey, Path.GetDirectoryName(path) ?? directory);
            Debug.Log("[Maze] Imported settings from " + path);
        }

        // ---------------------------------------------------------------------
        // Validation
        // ---------------------------------------------------------------------

        /// <summary>
        /// Runs the whole generator over a matrix of sizes, algorithms, seeds and options and reports
        /// every broken invariant. Used by CI and by the EditMode tests.
        /// </summary>
        [MenuItem(Root + "Validate Generator (all algorithms)", false, 80)]
        public static void ValidateGenerator()
        {
            int failures = 0;
            int checks = 0;
            MazeAlgorithmKind[] algorithms = (MazeAlgorithmKind[])System.Enum.GetValues(typeof(MazeAlgorithmKind));
            int[] sizes = { 7, 21, 41, 101 };
            float[] braidFactors = { 0f, 0.5f, 1f };
            MazeOpeningMode[] openingModes =
            {
                MazeOpeningMode.None,
                MazeOpeningMode.Random,
                MazeOpeningMode.FixedEntranceAndExit,
            };

            MazeGeneratorCore core = new MazeGeneratorCore();
            for (int a = 0; a < algorithms.Length; a++)
            {
                for (int s = 0; s < sizes.Length; s++)
                {
                    for (int b = 0; b < braidFactors.Length; b++)
                    {
                        for (int o = 0; o < openingModes.Length; o++)
                        {
                            for (int seed = 0; seed < 3; seed++)
                            {
                                MazeGeneratorSettings settings = new MazeGeneratorSettings
                                {
                                    MazeWidth = sizes[s],
                                    MazeHeight = sizes[s],
                                    Algorithm = algorithms[a],
                                    BraidFactor = braidFactors[b],
                                    OpeningMode = openingModes[o],
                                    UseRandomSeed = false,
                                    Seed = seed,
                                };

                                MazeGenerationOutput output = core.Generate(settings, seed);
                                MazeGrid grid = output.Grid;
                                checks++;

                                bool ok = true;
                                for (int y = 0; y < grid.RoomCountY && ok; y++)
                                {
                                    for (int x = 0; x < grid.RoomCountX && ok; x++)
                                    {
                                        ok = grid.IsRoomCarved(x, y);
                                    }
                                }

                                ok &= Maze.Analysis.MazePathfinder.IsFullyConnected(grid);

                                if (!ok)
                                {
                                    failures++;
                                    Debug.LogError("[Maze] Validation failed: " + algorithms[a] + " " + sizes[s] +
                                                   "x" + sizes[s] + " seed " + seed + " braid " + braidFactors[b] +
                                                   " openings " + openingModes[o]);
                                }
                            }
                        }
                    }
                }
            }

            string message = failures == 0
                ? checks + " configurations validated, no problems found."
                : failures + " of " + checks + " configurations FAILED (see the console).";
            Debug.Log("[Maze] " + message);
            EditorUtility.DisplayDialog("Validate Generator", message, "OK");
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static MazeGenerator FindSelected()
        {
            GameObject selected = Selection.activeGameObject;
            MazeGenerator generator = selected != null ? selected.GetComponent<MazeGenerator>() : null;
            if (generator == null)
            {
                EditorUtility.DisplayDialog("Maze", "Select a GameObject with a MazeGenerator component.", "OK");
            }

            return generator;
        }

        private static void CopySettings(MazeGeneratorSettings source, MazeGeneratorSettings target)
        {
            target.MazeWidth = source.MazeWidth;
            target.MazeHeight = source.MazeHeight;
            target.Algorithm = source.Algorithm;
            target.UseRandomSeed = source.UseRandomSeed;
            target.Seed = source.Seed;
            target.BraidFactor = source.BraidFactor;
            target.OpeningMode = source.OpeningMode;
            target.RandomOpeningCount = source.RandomOpeningCount;
            target.EntranceSide = source.EntranceSide;
            target.ExitSide = source.ExitSide;
            target.Padding = source.Padding;
            target.CellSize = source.CellSize;
            target.WallHeight = source.WallHeight;
            target.GenerateFloor = source.GenerateFloor;
            target.Sanitize();
        }
    }
}
