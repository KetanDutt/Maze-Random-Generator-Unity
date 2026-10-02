using System.Collections.Generic;
using Maze.Algorithms;
using Maze.Core;
using Maze.Gameplay;
using Maze.Generation;
using Maze.Presentation;
using Maze.Rendering;
using UnityEngine;

namespace Maze.UI
{
    /// <summary>
    /// The complete user interface of the demo: a live statistics panel, an authoring panel
    /// (size, algorithm, seed, braiding, openings), a legend and a minimap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The UI is drawn with IMGUI on purpose: the project must work out of the box in an empty
    /// Unity project without UI prefabs, canvases, fonts or event systems. Everything is generated
    /// from runtime textures, so there are no scene assets to keep in sync.
    /// </para>
    /// <para>
    /// Press <c>F1</c> to toggle the panels and <c>Tab</c> to toggle the statistics overlay.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Maze/UI/Maze Hud")]
    public sealed class MazeHud : MonoBehaviour
    {
        private enum Panel
        {
            Stats,
            Authoring,
            Legend,
        }

        [Header("References")]
        [SerializeField]
        [Tooltip("Generator controlled by the authoring panel. Found automatically when left empty.")]
        private MazeGenerator _generator;

        [SerializeField]
        [Tooltip("Objective tracker shown in the statistics panel. Found automatically when left empty.")]
        private MazeObjectiveTracker _objective;

        [SerializeField]
        [Tooltip("Minimap drawn in the corner. Found automatically when left empty.")]
        private MazeMinimap _minimap;

        [SerializeField]
        [Tooltip("Camera rig controlled by the '1/2/3' hotkeys. Found automatically when left empty.")]
        private MazeCameraRig _cameraRig;

        [Header("Behaviour")]
        [SerializeField, Tooltip("Show the panels when the scene starts.")]
        private bool _visibleOnStart = true;

        [SerializeField, Tooltip("Show the statistics overlay.")]
        private bool _showStats = true;

        [SerializeField, Tooltip("Show the minimap in the top right corner.")]
        private bool _showMinimap = true;

        [SerializeField, Tooltip("Show the keyboard legend.")]
        private bool _showLegend = true;

        [SerializeField, Range(100, 260), Tooltip("Base width of the panels in pixels.")]
        private int _panelWidth = 232;

        [SerializeField, Range(0.75f, 2f), Tooltip("UI scale for high resolution displays.")]
        private float _uiScale = 1f;

        [SerializeField, Tooltip("Show the seed of the current maze in the centre when it changes.")]
        private bool _announceSeed = true;

        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _toggleStyle;
        private Texture2D _panelTexture;
        private Texture2D _accentTexture;
        private Vector2 _statsScroll;
        private Vector2 _authoringScroll;
        private string _seedField = string.Empty;
        private string _sizeField = string.Empty;
        private float _seedAnnounceTimer;
        private float _stylesScale = -1f;
        private bool _settingsChanged;
        private float _lastRegenerateTime;
        private bool _seedFieldFocused;
        private const string SeedFieldName = "MazeSeedField";
        private int _announcedSeed;
        private bool _announcedSizeAdjusted;

        private static readonly Color PanelColor = new Color(0.043f, 0.055f, 0.078f, 0.90f);
        private static readonly Color AccentColor = new Color(0.09f, 0.76f, 0.84f, 1f);
        private static readonly Color TextColor = new Color(0.89f, 0.92f, 0.96f);
        private static readonly Color DimTextColor = new Color(0.58f, 0.65f, 0.75f);
        private static readonly Color WarnColor = new Color(1f, 0.72f, 0.28f);

        // ---------------------------------------------------------------------
        // Unity messages
        // ---------------------------------------------------------------------

        private void Awake()
        {
            if (_generator == null)
            {
                _generator = FindObjectOfType<MazeGenerator>();
            }

            if (_objective == null)
            {
                _objective = FindObjectOfType<MazeObjectiveTracker>();
            }

            if (_minimap == null)
            {
                _minimap = FindObjectOfType<MazeMinimap>();
            }

            if (_cameraRig == null)
            {
                _cameraRig = FindObjectOfType<MazeCameraRig>();
            }
        }

        private void OnEnable()
        {
            if (_generator != null)
            {
                _generator.Generated += OnMazeGenerated;
            }
        }

        private void OnDisable()
        {
            if (_generator != null)
            {
                _generator.Generated -= OnMazeGenerated;
            }
        }

        private void OnDestroy()
        {
            DestroyAsset(_panelTexture);
            DestroyAsset(_accentTexture);
        }

        /// <summary>Destroys a runtime asset in play mode and an immediate asset in edit mode.</summary>
        private static void DestroyAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(asset);
            }
            else
            {
                Object.DestroyImmediate(asset);
            }
        }

        private void Update()
        {
            HandleHotkeys();

            if (_seedAnnounceTimer > 0f)
            {
                _seedAnnounceTimer -= Time.unscaledDeltaTime;
            }

            // Slider changes are applied outside the IMGUI pass (and throttled) so that dragging a
            // slider regenerates a few times per second instead of once per frame.
            if (_settingsChanged && Time.unscaledTime - _lastRegenerateTime > 0.2f && _generator != null)
            {
                _settingsChanged = false;
                _lastRegenerateTime = Time.unscaledTime;
                _generator.Generate();
            }
        }

        /// <summary>Marks the settings as changed; the maze is regenerated on the next update.</summary>
        private void MarkSettingsChanged()
        {
            _settingsChanged = true;
        }

        private void OnGUI()
        {
            if (!_visibleOnStart && !_showMinimap && _seedAnnounceTimer <= 0f)
            {
                MazeInputGateway.PointerOverUi = false;
                MazeInputGateway.UiBlocksGameplay = false;
                return;
            }

            EnsureStyles();

            if (_visibleOnStart)
            {
                float scale = Mathf.Max(0.75f, _uiScale);
                float width = _panelWidth * scale;

                Rect statsRect = new Rect(12f, 12f, width, _showStats ? 250f * scale : 26f * scale);
                Rect authoringRect = new Rect(12f, statsRect.yMax + 8f, width, 330f * scale);
                Rect legendRect = new Rect(12f, authoringRect.yMax + 8f, width, _showLegend ? 150f * scale : 26f * scale);

                DrawStats(statsRect);

                // The authoring panel needs the mouse, so the player controller must not look around
                // while the pointer is over it.
                bool overAuthoring = authoringRect.Contains(Event.current.mousePosition);
                MazeInputGateway.PointerOverUi = overAuthoring || legendRect.Contains(Event.current.mousePosition);
                MazeInputGateway.UiBlocksGameplay = overAuthoring;

                DrawAuthoring(authoringRect);
                DrawLegend(legendRect);
            }
            else
            {
                MazeInputGateway.PointerOverUi = false;
                MazeInputGateway.UiBlocksGameplay = false;
            }

            if (_showMinimap)
            {
                DrawMinimap();
            }

            if (_announceSeed && _seedAnnounceTimer > 0f)
            {
                DrawSeedAnnouncement();
            }
        }

        // ---------------------------------------------------------------------
        // Panels
        // ---------------------------------------------------------------------

        private void DrawStats(Rect rect)
        {
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label("Maze Generator", _headerStyle);

            if (!_showStats)
            {
                GUILayout.EndArea();
                return;
            }

            _statsScroll = GUILayout.BeginScrollView(_statsScroll);
            MazeResult result = _generator != null ? _generator.Result : null;
            if (result == null)
            {
                GUILayout.Label("No maze generated yet.", _labelStyle);
            }
            else
            {
                MazeStatistics stats = result.Statistics;
                Row("Size", stats.Width + " x " + stats.Height + "  (" + stats.RoomCount + " rooms)");
                Row("Seed", stats.Seed.ToString());
                Row("Algorithm", MazeAlgorithmRegistry.GetDisplayName(_generator.Settings.Algorithm));
                Row("Walls / passages", stats.WallCount + " / " + stats.PassageCount);
                Row("Dead ends", stats.DeadEndCount + (stats.IsPerfect ? "  (perfect maze)" : "  (loops)"));
                Row("Junctions", stats.JunctionCount.ToString());
                Row("Openings", stats.OpeningCount.ToString());
                Row("Solution", stats.SolutionPathLength > 0 ? stats.SolutionPathLength + " cells" : "-");
                Row("Depth", stats.Depth + " cells");

                if (_objective != null && _objective.HasObjective)
                {
                    string state = _objective.IsReached
                        ? "reached (" + _objective.CompletedCount + " total)"
                        : "searching";
                    Row("Objective", state);
                    Row("Distance", _objective.Distance.ToString("0.0") + " of " +
                                    _objective.StraightLineDistance.ToString("0.0") + " straight line");
                }

                Row("Generation", result.GenerationMilliseconds.ToString("0.0") + " ms  (" +
                                  result.MeshData.TriangleCount + " triangles)");
                Row("Objects", CountSceneObjects().ToString() + "  (" + result.Grid.Openings.Count + " openings)");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawAuthoring(Rect rect)
        {
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label("Maze settings", _headerStyle);

            if (_generator == null)
            {
                GUILayout.Label("No MazeGenerator found in the scene.", _labelStyle);
                GUILayout.EndArea();
                return;
            }

            MazeGeneratorSettings settings = _generator.Settings;
            _authoringScroll = GUILayout.BeginScrollView(_authoringScroll);

            // --- size ---------------------------------------------------------
            GUILayout.Label("Size (cells, kept odd)", _titleStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("W", _labelStyle, GUILayout.Width(14f));
            int width = Slider(settings.MazeWidth, MazeGeneratorSettings.MinimumSize, 101);
            GUILayout.Label("H", _labelStyle, GUILayout.Width(14f));
            int height = Slider(settings.MazeHeight, MazeGeneratorSettings.MinimumSize, 101);
            GUILayout.EndHorizontal();

            if (width != settings.MazeWidth || height != settings.MazeHeight)
            {
                settings.MazeWidth = width;
                settings.MazeHeight = height;
                MarkSettingsChanged();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("21", _buttonStyle))
            {
                ApplySize(21, 21);
            }

            if (GUILayout.Button("41", _buttonStyle))
            {
                ApplySize(41, 41);
            }

            if (GUILayout.Button("81", _buttonStyle))
            {
                ApplySize(81, 81);
            }

            if (GUILayout.Button("Square", _buttonStyle))
            {
                int side = Mathf.Max(settings.MazeWidth, settings.MazeHeight);
                ApplySize(side, side);
            }

            GUILayout.EndHorizontal();

            // --- algorithm ----------------------------------------------------
            GUILayout.Space(4f);
            GUILayout.Label("Algorithm", _titleStyle);
            IReadOnlyList<IMazeAlgorithm> algorithms = MazeAlgorithmRegistry.All;
            for (int i = 0; i < algorithms.Count; i++)
            {
                MazeAlgorithmKind kind = (MazeAlgorithmKind)i;
                bool selected = settings.Algorithm == kind;
                if (GUILayout.Toggle(selected, " " + algorithms[i].DisplayName, _toggleStyle) != selected)
                {
                    if (!selected)
                    {
                        _generator.SetAlgorithm(kind);
                        MarkSizeAdjusted();
                    }
                }
            }

            GUILayout.Label(MazeAlgorithmRegistry.GetDescription(settings.Algorithm), _smallStyle);

            // --- seed ---------------------------------------------------------
            GUILayout.Space(4f);
            GUILayout.Label("Seed", _titleStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Random", _buttonStyle))
            {
                _generator.GenerateWithNewSeed();
                SyncSeedField();
            }

            string seedText = settings.UseRandomSeed ? "random each time" : settings.Seed.ToString();
            GUILayout.Label(seedText, _smallStyle, GUILayout.Width(90f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(SeedFieldName);
            _seedField = GUILayout.TextField(_seedField, _toggleStyle);
            _seedFieldFocused = GUI.GetNameOfFocusedControl() == SeedFieldName;
            if (GUILayout.Button("Use", _buttonStyle, GUILayout.Width(46f)))
            {
                int seed;
                if (int.TryParse(_seedField, out seed))
                {
                    _generator.GenerateWithSeed(seed);
                }
            }

            if (GUILayout.Button("Copy", _buttonStyle, GUILayout.Width(50f)))
            {
                GUIUtility.systemCopyBuffer = _generator.LastSeed.ToString();
            }

            GUILayout.EndHorizontal();

            if (GUILayout.Button("Regenerate", _buttonStyle))
            {
                _settingsChanged = false;
                _lastRegenerateTime = Time.unscaledTime;
                _generator.Regenerate();
            }

            // --- braiding -------------------------------------------------------
            GUILayout.Space(4f);
            GUILayout.Label("Loops (braiding): " + settings.BraidFactor.ToString("0.00"), _titleStyle);
            float braid = GUILayout.HorizontalSlider(settings.BraidFactor, 0f, 1f);
            if (!Mathf.Approximately(braid, settings.BraidFactor))
            {
                settings.BraidFactor = braid;
                MarkSettingsChanged();
            }

            GUILayout.Label("0 = perfect maze (no loops), 1 = no dead ends", _smallStyle);

            // --- openings -------------------------------------------------------
            GUILayout.Space(4f);
            GUILayout.Label("Entrances / exits", _titleStyle);
            if (Toggle(settings.OpeningMode == MazeOpeningMode.None, " Closed maze (no openings)"))
            {
                settings.OpeningMode = MazeOpeningMode.None;
                _generator.Generate();
            }

            if (Toggle(settings.OpeningMode == MazeOpeningMode.FixedEntranceAndExit, " Entrance + exit (opposite sides)"))
            {
                settings.OpeningMode = MazeOpeningMode.FixedEntranceAndExit;
                _generator.Generate();
            }

            if (Toggle(settings.OpeningMode == MazeOpeningMode.Random, " Random openings"))
            {
                settings.OpeningMode = MazeOpeningMode.Random;
                _generator.Generate();
            }

            if (settings.OpeningMode == MazeOpeningMode.Random)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Count", _labelStyle, GUILayout.Width(44f));
                int count = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.RandomOpeningCount, 1f, 4f));
                if (count != settings.RandomOpeningCount)
                {
                    settings.RandomOpeningCount = count;
                    _generator.Generate();
                }

                GUILayout.Label(count.ToString(), _smallStyle, GUILayout.Width(16f));
                GUILayout.EndHorizontal();
            }

            // --- look -----------------------------------------------------------
            GUILayout.Space(4f);
            GUILayout.Label("Look", _titleStyle);
            GUILayout.Label("Palette", _labelStyle);
            MazePalette[] palettes = (MazePalette[])System.Enum.GetValues(typeof(MazePalette));
            float halfWidth = _panelWidth * Mathf.Max(0.75f, _uiScale) * 0.42f;
            for (int i = 0; i < palettes.Length; i++)
            {
                if (i % 2 == 0)
                {
                    GUILayout.BeginHorizontal();
                }

                MazePalette palette = palettes[i];
                bool selected = _generator.Palette == palette;
                if (GUILayout.Toggle(selected, MazePalettes.GetName(palette), _buttonStyle,
                        GUILayout.Width(halfWidth)) != selected && !selected)
                {
                    _generator.ApplyPalette(palette);
                }

                if (i % 2 == 1 || i == palettes.Length - 1)
                {
                    GUILayout.EndHorizontal();
                }
            }

            bool floor = Toggle(settings.GenerateFloor, " Floor plane");
            if (floor != settings.GenerateFloor)
            {
                settings.GenerateFloor = floor;
                _generator.Generate();
            }

            GUILayout.Space(4f);
            GUILayout.Label("Presentation", _titleStyle);
            if (_objective != null && _objective.PathRenderer != null)
            {
                bool current = _objective.PathRenderer.IsVisible;
                bool showPath = Toggle(current, " Show solution path (P)");
                if (showPath != current)
                {
                    _objective.SetHintVisible(showPath);
                }
            }

            if (_cameraRig != null)
            {
                GUILayout.Label("Camera (1/2/3)", _labelStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("First person", _buttonStyle, GUILayout.Width(halfWidth)))
                {
                    _cameraRig.SetMode(MazeCameraMode.FirstPerson);
                }

                if (GUILayout.Button("Orbit", _buttonStyle, GUILayout.Width(halfWidth)))
                {
                    _cameraRig.SetMode(MazeCameraMode.Orbit);
                }

                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Free fly", _buttonStyle, GUILayout.Width(halfWidth)))
                {
                    _cameraRig.SetMode(MazeCameraMode.FreeFly);
                }

                if (GUILayout.Button("Frame maze", _buttonStyle, GUILayout.Width(halfWidth)) && _generator.Result != null)
                {
                    _cameraRig.FrameMaze(_generator.Result);
                    _cameraRig.SetMode(MazeCameraMode.Orbit);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.Label("F1 panels  -  TAB stats  -  1/2/3 camera  -  P path", _smallStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawLegend(Rect rect)
        {
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Controls", _headerStyle);
            if (GUILayout.Button(_showLegend ? "v" : "^", _buttonStyle, GUILayout.Width(22f)))
            {
                _showLegend = !_showLegend;
            }

            GUILayout.EndHorizontal();

            if (_showLegend)
            {
                Row("Move", "W A S D / arrows");
                Row("Sprint", "Shift");
                Row("Jump", "Space");
                Row("Look", "Mouse (click to lock)");
                Row("Solution", "P");
                Row("Camera", "1 first person / 2 orbit / 3 fly");
                Row("Regenerate", "R");
                Row("Toggle UI", "F1 (stats: TAB)");
            }

            GUILayout.EndArea();
        }

        private void DrawMinimap()
        {
            MazeMinimap minimap = _minimap;
            if (minimap == null || minimap.Texture == null)
            {
                return;
            }

            float size = 178f * Mathf.Max(0.75f, _uiScale);
            float aspect = minimap.Texture.height <= 0
                ? 1f
                : minimap.Texture.width / (float)minimap.Texture.height;
            float width = size * Mathf.Clamp(aspect, 0.6f, 1.8f);

            Rect rect = new Rect(Screen.width - width - 16f, 16f, width, size);
            GUI.DrawTexture(rect, minimap.Texture, ScaleMode.StretchToFill, false);
            GUI.Box(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), GUIContent.none, _panelStyle);

            bool over = rect.Contains(Event.current.mousePosition);
            MazeInputGateway.PointerOverUi |= over;

            GUILayout.BeginArea(new Rect(rect.x, rect.yMax + 4f, rect.width, 24f));
            GUILayout.Label("Minimap (M to hide)", _smallStyle);
            GUILayout.EndArea();
        }

        private void DrawSeedAnnouncement()
        {
            string text = "Seed " + _announcedSeed;
            if (_announcedSizeAdjusted)
            {
                text += "   (size adjusted to odd numbers)";
            }

            GUIStyle style = _titleStyle;
            Vector2 size = style.CalcSize(new GUIContent(text));
            Rect rect = new Rect((Screen.width - size.x) * 0.5f - 12f, Screen.height - 96f, size.x + 24f, size.y + 10f);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_seedAnnounceTimer));
            GUI.DrawTexture(rect, _panelTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        // ---------------------------------------------------------------------
        // Interaction
        // ---------------------------------------------------------------------

        private void HandleHotkeys()
        {
            // Never steal keystrokes while the seed field has keyboard focus.
            if (_seedFieldFocused && GUIUtility.keyboardControl != 0)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                _visibleOnStart = !_visibleOnStart;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _showStats = !_showStats;
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                _showMinimap = !_showMinimap;
            }

            if (Input.GetKeyDown(KeyCode.P) && _objective != null)
            {
                _objective.ToggleHint();
            }

            if (Input.GetKeyDown(KeyCode.R) && _generator != null)
            {
                _generator.Generate();
                SyncSeedField();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) && _cameraRig != null)
            {
                _cameraRig.SetMode(MazeCameraMode.FirstPerson);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) && _cameraRig != null)
            {
                _cameraRig.SetMode(MazeCameraMode.Orbit);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3) && _cameraRig != null)
            {
                _cameraRig.SetMode(MazeCameraMode.FreeFly);
            }
        }

        private void OnMazeGenerated(MazeResult result)
        {
            SyncSeedField();
            _announcedSeed = result.Statistics.Seed;
            _announcedSizeAdjusted = result.Grid.Width != _generator.Settings.MazeWidth ||
                                     result.Grid.Height != _generator.Settings.MazeHeight;
            if (_announceSeed)
            {
                _seedAnnounceTimer = 1.6f;
            }
        }

        private void SyncSeedField()
        {
            if (_generator != null)
            {
                _seedField = _generator.LastSeed.ToString();
            }
        }

        private void ApplySize(int width, int height)
        {
            if (_generator == null)
            {
                return;
            }

            _settingsChanged = false;
            _lastRegenerateTime = Time.unscaledTime;
            _generator.SetSize(width, height);
            MarkSizeAdjusted();
        }

        private void MarkSizeAdjusted()
        {
            _announcedSizeAdjusted = _generator != null &&
                                     (_generator.Result == null ||
                                      _generator.Result.Grid.Width != _generator.Settings.MazeWidth ||
                                      _generator.Result.Grid.Height != _generator.Settings.MazeHeight);
        }

        private int CountSceneObjects()
        {
            if (_generator == null || _generator.Result == null)
            {
                return 0;
            }

            return _generator.RenderMode == MazeRenderMode.Mesh
                ? _generator.Result.MeshData.TriangleCount
                : _generator.PooledWallCount;
        }

        // ---------------------------------------------------------------------
        // IMGUI helpers
        // ---------------------------------------------------------------------

        private void Row(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(118f));
            GUILayout.Label(value, _valueStyle);
            GUILayout.EndHorizontal();
        }

        private int Slider(int value, int min, int max)
        {
            float result = GUILayout.HorizontalSlider(value, min, max);
            int rounded = Mathf.Clamp(Mathf.RoundToInt(result), min, max);
            GUILayout.Label(rounded.ToString(), _smallStyle, GUILayout.Width(26f));
            return rounded;
        }

        private bool Toggle(bool value, string text)
        {
            bool result = GUILayout.Toggle(value, text, _toggleStyle);
            return result;
        }

        private void EnsureStyles()
        {
            float scale = Mathf.Max(0.75f, _uiScale);
            if (_panelStyle != null && Mathf.Approximately(_stylesScale, scale))
            {
                return;
            }

            _stylesScale = scale;

            if (_panelTexture == null)
            {
                _panelTexture = MazeMaterialFactory.CreateSolidTexture(PanelColor, "Maze HUD Panel");
                _accentTexture = MazeMaterialFactory.CreateSolidTexture(AccentColor, "Maze HUD Accent");
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(Mathf.RoundToInt(10f * scale), Mathf.RoundToInt(10f * scale),
                    Mathf.RoundToInt(8f * scale), Mathf.RoundToInt(8f * scale)),
                alignment = TextAnchor.UpperLeft,
            };
            _panelStyle.normal.background = _panelTexture;
            _panelStyle.normal.textColor = TextColor;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = TextColor },
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = AccentColor },
                wordWrap = false,
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                normal = { textColor = DimTextColor },
                wordWrap = false,
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                normal = { textColor = TextColor },
                wordWrap = false,
            };

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(10f * scale),
                normal = { textColor = DimTextColor },
                wordWrap = true,
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                padding = new RectOffset(Mathf.RoundToInt(6f * scale), Mathf.RoundToInt(6f * scale),
                    Mathf.RoundToInt(2f * scale), Mathf.RoundToInt(2f * scale)),
                normal = { textColor = TextColor },
                hover = { textColor = AccentColor },
                active = { textColor = AccentColor },
            };

            _toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                normal = { textColor = TextColor },
                onNormal = { textColor = AccentColor },
                wordWrap = false,
            };
        }

        /// <summary>Shows or hides the whole UI.</summary>
        public void SetVisible(bool visible)
        {
            _visibleOnStart = visible;
            if (!visible)
            {
                MazeInputGateway.PointerOverUi = false;
                MazeInputGateway.UiBlocksGameplay = false;
            }
        }

        /// <summary>Shows or hides the minimap.</summary>
        public void SetMinimapVisible(bool visible)
        {
            _showMinimap = visible;
        }

        private void OnValidate()
        {
            _panelWidth = Mathf.Clamp(_panelWidth, 100, 260);
            _uiScale = Mathf.Clamp(_uiScale, 0.75f, 2f);
        }
    }
}
