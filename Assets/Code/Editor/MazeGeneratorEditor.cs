using System.IO;
using Maze.Core;
using Maze.Generation;
using Maze.Tools;
using UnityEditor;
using UnityEngine;

namespace Maze.EditorTools
{
    /// <summary>Inspector for <see cref="MazeGenerator"/> plus the scene view helpers.</summary>
    [CustomEditor(typeof(MazeGenerator))]
    public sealed class MazeGeneratorEditor : Editor
    {
        private readonly Color _cellColor = new Color(0.20f, 0.60f, 1f, 0.12f);
        private readonly Color _spawnColor = new Color(0.15f, 0.95f, 0.85f);
        private readonly Color _exitColor = new Color(1f, 0.30f, 0.55f);

        private bool _showStatistics = true;
        private bool _showSeedTools = true;
        private int _seedField;

        private static MazeGenerator Generator
        {
            get { return Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<MazeGenerator>() : null; }
        }

        /// <summary>Currently highlighted grid cell (toggled by clicking in the scene view).</summary>
        private static Vector2Int HighlightCell { get; set; }

        /// <summary>Currently highlighted room (toggled by alt clicking in the scene view).</summary>
        private static Vector2Int HighlightRoom { get; set; }

        private void OnEnable()
        {
            MazeGenerator generator = (MazeGenerator)target;
            _seedField = generator.Settings.Seed;
            HighlightCell = new Vector2Int(-1, -1);
            HighlightRoom = new Vector2Int(-1, -1);
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            MazeGenerator generator = (MazeGenerator)target;
            serializedObject.Update();

            EditorGUILayout.Space(2f);
            DrawButtons(generator);
            EditorGUILayout.Space(4f);

            DrawPropertiesExcluding(serializedObject, "m_Script", "_settings");
            EditorGUILayout.Space(6f);

            SerializedProperty settings = serializedObject.FindProperty("_settings");
            if (settings != null)
            {
                EditorGUILayout.LabelField("Maze Settings", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(settings, GUIContent.none, true);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            DrawSizeWarning(generator);
            DrawSeedTools(generator);
            DrawStatistics(generator);

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("Play mode: the maze regenerates on every Generate call.", MessageType.Info);
            }
        }

        private void DrawButtons(MazeGenerator generator)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.35f, 0.85f, 0.55f);
                if (GUILayout.Button("Generate", GUILayout.Height(26f)))
                {
                    generator.Generate();
                }

                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
                if (GUILayout.Button("Regenerate", GUILayout.Height(26f)))
                {
                    generator.Regenerate();
                }

                GUI.backgroundColor = new Color(1f, 0.6f, 0.45f);
                if (GUILayout.Button("Clear", GUILayout.Height(26f)))
                {
                    generator.Clear();
                }

                GUI.backgroundColor = Color.white;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Random seed"))
                {
                    generator.GenerateWithNewSeed();
                }

                if (GUILayout.Button("Copy seed"))
                {
                    EditorGUIUtility.systemCopyBuffer = generator.LastSeed.ToString();
                }

                if (GUILayout.Button("Copy ASCII"))
                {
                    if (generator.Result != null)
                    {
                        EditorGUIUtility.systemCopyBuffer = MazeAsciiExporter.ToAscii(generator.Result.Grid);
                    }
                }
            }

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export JSON..."))
                {
                    ExportJson(generator);
                }

                if (GUILayout.Button("Export PNG..."))
                {
                    ExportPng(generator);
                }
            }
        }

        private void DrawSizeWarning(MazeGenerator generator)
        {
            MazeGeneratorSettings settings = generator.Settings;
            bool adjusted = settings.MazeWidth != settings.Width || settings.MazeHeight != settings.Height;
            if (!adjusted)
            {
                return;
            }

            EditorGUILayout.HelpBox("Size adjusted to " + settings.Width + " x " + settings.Height +
                                    " (the maze works on an odd sized room lattice).", MessageType.Warning);
        }

        private void DrawSeedTools(MazeGenerator generator)
        {
            _showSeedTools = EditorGUILayout.Foldout(_showSeedTools, "Seed", true);
            if (!_showSeedTools)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _seedField = EditorGUILayout.IntField("Seed", _seedField);
                if (GUILayout.Button("Apply", GUILayout.Width(60f)))
                {
                    generator.GenerateWithSeed(_seedField);
                }
            }

            if (generator.Result != null)
            {
                EditorGUILayout.LabelField("Current seed", generator.LastSeed.ToString());
                EditorGUILayout.LabelField("Signature", generator.Result.Grid.ComputeFingerprint().ToString("X16"));
            }
        }

        private void DrawStatistics(MazeGenerator generator)
        {
            _showStatistics = EditorGUILayout.Foldout(_showStatistics, "Statistics", true);
            if (!_showStatistics)
            {
                return;
            }

            MazeResult result = generator.Result;
            if (result == null)
            {
                EditorGUILayout.HelpBox("Click Generate to create a maze (also works in edit mode).", MessageType.None);
                return;
            }

            MazeStatistics statistics = result.Statistics;
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Size", statistics.Width + " x " + statistics.Height + " cells");
            EditorGUILayout.LabelField("Rooms", statistics.RoomCount.ToString());
            EditorGUILayout.LabelField("Walls / passages", statistics.WallCount + " / " + statistics.PassageCount);
            EditorGUILayout.LabelField("Dead ends", statistics.DeadEndCount.ToString());
            EditorGUILayout.LabelField("Junctions", statistics.JunctionCount.ToString());
            EditorGUILayout.LabelField("Openings", statistics.OpeningCount.ToString());
            EditorGUILayout.LabelField("Solution path", statistics.SolutionPathLength + " cells");
            EditorGUILayout.LabelField("Depth", statistics.Depth + " cells");
            EditorGUILayout.LabelField("Structure", statistics.IsPerfect ? "perfect (tree)" : "contains loops");
            EditorGUILayout.LabelField("Connectivity", statistics.IsFullyConnected ? "fully connected" : "BROKEN");
            EditorGUILayout.LabelField("Generation", result.GenerationMilliseconds.ToString("0.0") + " ms");
            EditorGUILayout.LabelField("Mesh", result.MeshData.TriangleCount + " triangles, " +
                                               result.MeshData.VertexCount + " vertices");

            if (!statistics.IsFullyConnected)
            {
                EditorGUILayout.HelpBox("This maze is not fully connected. Please report the seed " +
                                        statistics.Seed + ".", MessageType.Error);
            }

            EditorGUI.indentLevel--;

            if (GUILayout.Button("Mark spawn / exit in the scene view"))
            {
                SceneView.RepaintAll();
            }
        }

        // ---------------------------------------------------------------------
        // Scene view
        // ---------------------------------------------------------------------

        private void OnSceneGUI()
        {
            MazeGenerator generator = Generator;
            if (generator == null || generator.Result == null)
            {
                return;
            }

            Event current = Event.current;
            if (current != null && !current.alt)
            {
                if (current.type == EventType.MouseDown && current.button == 1)
                {
                    // Right click highlights the room under the cursor; using the input keeps the
                    // default scene navigation controls untouched.
                    Vector2Int room = RoomUnderMouse(generator);
                    HighlightRoom = HighlightRoom == room ? new Vector2Int(-1, -1) : room;
                    HighlightCell = new Vector2Int(-1, -1);
                    SceneView.RepaintAll();
                }
                else if (current.type == EventType.MouseMove)
                {
                    Vector2Int cell = CellUnderMouse(generator);
                    if (cell != HighlightCell && generator.Result.Grid.InBounds(cell.x, cell.y))
                    {
                        HighlightCell = cell;
                        SceneView.RepaintAll();
                    }
                }
            }

            DrawHighlight(generator);
        }

        private void DrawHighlight(MazeGenerator generator)
        {
            MazeResult result = generator.Result;
            Transform root = generator.transform;
            Handles.matrix = Matrix4x4.TRS(root.position, root.rotation, Vector3.one);

            if (result.Grid.InBounds(HighlightCell.x, HighlightCell.y))
            {
                Handles.color = _cellColor;
                DrawCell(result, HighlightCell, result.WallHeight * 0.4f);
            }

            if (result.Grid.InRoomBounds(HighlightRoom.x, HighlightRoom.y))
            {
                Handles.color = _spawnColor;
                DrawCell(result, result.Grid.CellOfRoom(HighlightRoom.x, HighlightRoom.y), result.WallHeight * 1.1f);
            }

            Handles.color = _spawnColor;
            Handles.Label(result.SpawnPosition + Vector3.up * (result.WallHeight + 1f), "Spawn");
            Handles.color = _exitColor;
            Handles.Label(result.ExitPosition + Vector3.up * (result.WallHeight + 1f), "Exit");

            Handles.matrix = Matrix4x4.identity;
        }

        private static void DrawCell(MazeResult result, Vector2Int cell, float height)
        {
            Vector3 center = result.CellToWorld(cell) + Vector3.up * height * 0.5f;
            Vector3 size = new Vector3(result.CellSize, height, result.CellSize);
            Handles.DrawWireCube(center, size);
        }

        private static Vector2Int CellUnderMouse(MazeGenerator generator)
        {
            MazeResult result = generator.Result;
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            Plane plane = new Plane(generator.transform.up, generator.transform.position);

            float distance;
            if (!plane.Raycast(ray, out distance))
            {
                return new Vector2Int(-1, -1);
            }

            Vector3 world = ray.GetPoint(distance);
            return result.WorldToCell(generator.transform.InverseTransformPoint(world));
        }

        private static Vector2Int RoomUnderMouse(MazeGenerator generator)
        {
            Vector2Int cell = CellUnderMouse(generator);
            return cell.x < 0 ? cell : generator.Result.Grid.RoomOfCell(cell.x, cell.y);
        }

        // ---------------------------------------------------------------------
        // Export helpers
        // ---------------------------------------------------------------------

        private static void ExportJson(MazeGenerator generator)
        {
            if (generator.Result == null)
            {
                EditorUtility.DisplayDialog("Export JSON", "Generate a maze first.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject("Export maze JSON", "maze-seed-" + generator.LastSeed,
                "json", "Choose where to save the maze document.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, MazeJsonExport.ToJson(generator.Result, generator.Settings));
            AssetDatabase.Refresh();
            Debug.Log("[Maze] Exported " + path);
        }

        private static void ExportPng(MazeGenerator generator)
        {
            if (generator.Result == null)
            {
                EditorUtility.DisplayDialog("Export PNG", "Generate a maze first.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject("Export maze preview", "maze-seed-" + generator.LastSeed,
                "png", "Choose where to save the preview.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            MazeGrid grid = generator.Result.Grid;
            int scale = Mathf.Max(1, Mathf.Min(12, 512 / Mathf.Max(grid.Width, grid.Height)));
            Texture2D texture = new Texture2D(grid.Width * scale, grid.Height * scale, TextureFormat.RGBA32, false);
            Color wallColor = new Color(0.16f, 0.19f, 0.26f);
            Color passageColor = new Color(0.92f, 0.94f, 0.97f);
            Color[] pixels = new Color[texture.width * texture.height];

            for (int y = 0; y < texture.height; y++)
            {
                int cellY = y / scale;
                for (int x = 0; x < texture.width; x++)
                {
                    int cellX = x / scale;
                    pixels[y * texture.width + x] = grid.IsWall(cellX, cellY) ? wallColor : passageColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.Refresh();
            Debug.Log("[Maze] Exported " + path);
        }
    }
}
