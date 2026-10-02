using Maze.Gameplay;
using Maze.Generation;
using Maze.Rendering;
using UnityEngine;

namespace Maze.Presentation
{
    /// <summary>
    /// Renders a top down view of the current maze into a <see cref="RenderTexture"/> so the HUD can
    /// show a minimap. It rasterises the grid into a texture instead of rendering the scene again,
    /// so the cost stays the same for any maze size.
    /// </summary>
    /// <remarks>
    /// The minimap lives on its own layer and has its own camera, which means the rest of the scene
    /// is neither rendered by it nor affected by it.
    /// </remarks>
    [AddComponentMenu("Maze/Presentation/Maze Minimap")]
    public sealed class MazeMinimap : MonoBehaviour
    {
        /// <summary>Layer the minimap camera renders; created by the project's TagManager.</summary>
        public const string MinimapLayerName = "Minimap";

        [SerializeField]
        [Tooltip("Maze that is displayed. Found automatically when left empty.")]
        private MazeGenerator _generator;

        [SerializeField]
        [Tooltip("Player marker. Found automatically when left empty.")]
        private MazePlayerController _player;

        [SerializeField, Range(0.5f, 16f), Tooltip("Pixels per cell in the minimap texture.")]
        private float _pixelsPerCell = 4f;

        [SerializeField, Range(64, 2048), Tooltip("Upper limit of the texture size.")]
        private int _maximumTextureSize = 512;

        [SerializeField, Range(0f, 1f), Tooltip("Extra space around the maze inside the view.")]
        private float _viewMargin = 0.06f;

        [SerializeField, Range(1f, 2000f), Tooltip("How far the minimap camera sits above the map plane.")]
        private float _cameraDistance = 300f;

        [SerializeField, Range(0.5f, 6f), Tooltip("Size of the player marker, measured in cells.")]
        private float _markerSizeCells = 2.2f;

        [SerializeField, Tooltip("Rotate the minimap so the player always faces up.")]
        private bool _rotateWithPlayer = true;

        [SerializeField, Range(0f, 1f), Tooltip("Smoothing applied to the minimap camera. 0 = instant.")]
        private float _smoothing = 0.12f;

        [Header("Colours")]
        [SerializeField] private Color _wallColor = new Color(0.35f, 0.42f, 0.58f);
        [SerializeField] private Color _passageColor = new Color(0.08f, 0.10f, 0.15f);
        [SerializeField] private Color _solutionColor = new Color(1f, 0.24f, 0.51f, 0.85f);
        [SerializeField] private Color _playerColor = new Color(0.29f, 0.98f, 0.86f);
        [SerializeField] private Color _goalColor = new Color(1f, 0.65f, 0.15f);
        [SerializeField] private Color _backgroundColor = new Color(0.06f, 0.07f, 0.11f, 1f);

        private RenderTexture _texture;
        private Texture2D _mapTexture;
        private Mesh _quadMesh;
        private Mesh _markerMesh;
        private Camera _camera;
        private Transform _cameraTransform;
        private Transform _space;
        private MeshFilter _mapFilter;
        private MeshRenderer _mapRenderer;
        private Transform _playerMarker;
        private Transform _goalMarker;
        private Material _mapMaterial;
        private Material _playerMaterial;
        private Material _goalMaterial;
        private int _minimapLayer;
        private float _cameraAngle;
        private Vector3 _cameraPosition;
        private bool _hasCameraTarget;

        /// <summary>The rendered minimap, or <c>null</c> before the first generation.</summary>
        public RenderTexture Texture
        {
            get { return _texture; }
        }

        /// <summary>Whether the minimap rotates with the player.</summary>
        public bool RotateWithPlayer
        {
            get { return _rotateWithPlayer; }
            set { _rotateWithPlayer = value; }
        }

        // ---------------------------------------------------------------------
        // Unity messages
        // ---------------------------------------------------------------------

        private void Awake()
        {
            if (_generator == null)
            {
                _generator = FindObjectOfType<MazeGenerator>();
            }

            if (_player == null)
            {
                _player = FindObjectOfType<MazePlayerController>();
            }

            _minimapLayer = LayerMask.NameToLayer(MinimapLayerName);
            if (_minimapLayer < 0)
            {
                _minimapLayer = 31;
                Debug.LogWarning("[Maze] Layer '" + MinimapLayerName +
                                 "' is missing from the project; the minimap falls back to layer 31.", this);
            }
        }

        private void OnEnable()
        {
            if (_generator == null)
            {
                return;
            }

            _generator.Generated += OnGenerated;
            if (_generator.HasResult)
            {
                OnGenerated(_generator.Result);
            }
        }

        private void OnDisable()
        {
            if (_generator != null)
            {
                _generator.Generated -= OnGenerated;
            }
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            DestroyTexture(_mapTexture);
            _mapTexture = null;
            DestroyMesh(_quadMesh);
            _quadMesh = null;
            DestroyMesh(_markerMesh);
            _markerMesh = null;
            DestroyMaterial(_mapMaterial);
            DestroyMaterial(_playerMaterial);
            DestroyMaterial(_goalMaterial);
        }

        private void LateUpdate()
        {
            if (_generator == null || !_generator.HasResult || _camera == null || _mapFilter == null)
            {
                return;
            }

            MazeResult result = _generator.Result;
            Transform root = _generator.transform;

            Vector3 focusWorld = _player != null ? _player.transform.position : root.TransformPoint(result.WorldCenter);
            Vector3 focusLocal = root.InverseTransformPoint(focusWorld);

            float forwardX = 0f;
            float forwardZ = 1f;
            if (_player == null)
            {
                forwardX = 0.24f; // slight tilt so the maze does not look axis aligned
            }
            else if (_rotateWithPlayer)
            {
                Vector3 forward = _player.transform.forward;
                forwardX = forward.x;
                forwardZ = forward.z;
            }

            float targetAngle = Mathf.Atan2(-forwardX, forwardZ) * Mathf.Rad2Deg;
            Vector3 targetPosition = new Vector3(focusLocal.x, focusLocal.z, -_cameraDistance);

            if (!_hasCameraTarget)
            {
                _cameraPosition = targetPosition;
                _cameraAngle = targetAngle;
                _hasCameraTarget = true;
            }
            else
            {
                float t = _smoothing <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, _smoothing));
                _cameraPosition = Vector3.Lerp(_cameraPosition, targetPosition, t);
                _cameraAngle = Mathf.LerpAngle(_cameraAngle, targetAngle, t);
            }

            _cameraTransform.localPosition = _cameraPosition;
            _cameraTransform.localRotation = Quaternion.Euler(0f, 0f, _cameraAngle);

            UpdateViewSize(result);
            UpdatePlayerMarker(result, root);
            UpdateGoalMarker(result);
        }

        /// <summary>
        /// Keeps the whole maze inside the view even while the map is rotated: the rotated bounding
        /// box of the rectangle decides the required orthographic size.
        /// </summary>
        private void UpdateViewSize(MazeResult result)
        {
            float halfWidth = result.Grid.Width * result.CellSize * 0.5f;
            float halfHeight = result.Grid.Height * result.CellSize * 0.5f;
            float radians = _cameraAngle * Mathf.Deg2Rad;
            float sin = Mathf.Abs(Mathf.Sin(radians));
            float cos = Mathf.Abs(Mathf.Cos(radians));

            float projectedHalfHeight = halfWidth * sin + halfHeight * cos;
            float projectedHalfWidth = halfWidth * cos + halfHeight * sin;
            float aspect = _camera.aspect > 0.01f ? _camera.aspect : 1f;

            _camera.orthographicSize = Mathf.Max(projectedHalfHeight, projectedHalfWidth / aspect) *
                                       (1f + _viewMargin * 2f);
        }

        // ---------------------------------------------------------------------
        // Building
        // ---------------------------------------------------------------------

        private void OnGenerated(MazeResult result)
        {
            if (result == null)
            {
                return;
            }

            EnsureInfrastructure();
            SyncSpace(result);

            MazeGrid grid = result.Grid;
            int textureWidth = Mathf.Clamp(Mathf.RoundToInt(grid.Width * _pixelsPerCell), 32, _maximumTextureSize);
            int textureHeight = Mathf.Clamp(Mathf.RoundToInt(grid.Height * _pixelsPerCell), 32, _maximumTextureSize);

            ReleaseTexture();
            _texture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "Maze Minimap",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            _texture.Create();

            _camera.targetTexture = _texture;
            _camera.backgroundColor = _backgroundColor;
            _camera.aspect = textureWidth / (float)textureHeight;

            RebuildMapTexture(result, textureWidth, textureHeight);
            RebuildQuad(result);
            _hasCameraTarget = false;
        }

        /// <summary>Creates the camera, the minimap layer and the marker objects once.</summary>
        private void EnsureInfrastructure()
        {
            if (_space == null)
            {
                GameObject space = new GameObject("Minimap Space");
                space.transform.SetParent(transform, false);
                space.layer = _minimapLayer;
                space.hideFlags = HideFlags.DontSave;
                _space = space.transform;
            }

            if (_camera == null)
            {
                GameObject cameraObject = new GameObject("Minimap Camera");
                cameraObject.transform.SetParent(_space, false);
                cameraObject.layer = _minimapLayer;
                cameraObject.hideFlags = HideFlags.DontSave;

                _camera = cameraObject.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = _backgroundColor;
                _camera.cullingMask = 1 << _minimapLayer;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = _cameraDistance * 4f;
                _camera.allowHDR = false;
                _camera.allowMSAA = false;
                _camera.depth = 10f;
                _camera.useOcclusionCulling = false;
                _cameraTransform = cameraObject.transform;
            }

            if (_mapFilter == null)
            {
                GameObject quad = new GameObject("Minimap Map");
                quad.transform.SetParent(_space, false);
                quad.layer = _minimapLayer;
                quad.hideFlags = HideFlags.DontSave;
                _mapFilter = quad.AddComponent<MeshFilter>();
                _mapRenderer = quad.AddComponent<MeshRenderer>();
                _mapRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mapRenderer.receiveShadows = false;
            }

            if (_playerMarker == null)
            {
                _playerMarker = CreateMarker("Minimap Player", _playerColor, out _playerMaterial);
            }

            if (_goalMarker == null)
            {
                _goalMarker = CreateMarker("Minimap Goal", _goalColor, out _goalMaterial);
            }
        }

        private Transform CreateMarker(string name, Color color, out Material material)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(_space, false);
            marker.layer = _minimapLayer;
            marker.hideFlags = HideFlags.DontSave;

            // One marker mesh is shared by the player and the goal marker.
            if (_markerMesh == null)
            {
                _markerMesh = new Mesh { name = "Minimap Marker", hideFlags = HideFlags.DontSave };
                _markerMesh.vertices = new[]
                {
                    new Vector3(-0.5f, -1f, 0f),
                    new Vector3(0.5f, -1f, 0f),
                    new Vector3(0.5f, 1f, 0f),
                    new Vector3(-0.5f, 1f, 0f),
                };
                _markerMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _markerMesh.RecalculateBounds();
            }

            marker.AddComponent<MeshFilter>().sharedMesh = _markerMesh;

            MeshRenderer renderer = marker.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            material = MazeMaterialFactory.CreateUnlit(name, color, null);
            renderer.sharedMaterial = material;

            return marker.transform;
        }

        /// <summary>Aligns the minimap space with the maze so grid coordinates can be used directly.</summary>
        private void SyncSpace(MazeResult result)
        {
            Transform root = _generator.transform;
            if (root == transform)
            {
                _space.localPosition = Vector3.zero;
                _space.localRotation = Quaternion.identity;
                _space.localScale = Vector3.one;
                return;
            }

            _space.position = root.position;
            _space.rotation = root.rotation;
        }

        private void RebuildMapTexture(MazeResult result, int textureWidth, int textureHeight)
        {
            MazeGrid grid = result.Grid;
            DestroyTexture(_mapTexture);
            _mapTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                name = "Maze Minimap Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            Color[] pixels = new Color[textureWidth * textureHeight];

            for (int py = 0; py < textureHeight; py++)
            {
                int cellY = Mathf.Clamp((int)((py + 0.5f) / textureHeight * grid.Height), 0, grid.Height - 1);
                int rowStart = py * textureWidth;
                for (int px = 0; px < textureWidth; px++)
                {
                    int cellX = Mathf.Clamp((int)((px + 0.5f) / textureWidth * grid.Width), 0, grid.Width - 1);
                    pixels[rowStart + px] = grid.IsWall(cellX, cellY) ? _wallColor : _passageColor;
                }
            }

            if (result.HasSolution)
            {
                DrawPath(pixels, result, textureWidth, textureHeight);
            }

            _mapTexture.SetPixels(pixels);
            _mapTexture.Apply(false, false);

            if (_mapMaterial == null)
            {
                _mapMaterial = MazeMaterialFactory.CreateUnlit("Maze Minimap Map", Color.white, _mapTexture);
                _mapRenderer.sharedMaterial = _mapMaterial;
            }
            else
            {
                MazeMaterialFactory.SetTexture(_mapMaterial, _mapTexture);
                MazeMaterialFactory.SetColor(_mapMaterial, Color.white);
            }
        }

        private void DrawPath(Color[] pixels, MazeResult result, int textureWidth, int textureHeight)
        {
            MazeGrid grid = result.Grid;
            Vector3 origin = result.Origin;
            float cellSize = result.CellSize;
            float pixelsPerCellX = textureWidth / (float)grid.Width;
            float pixelsPerCellY = textureHeight / (float)grid.Height;

            for (int i = 0; i < result.SolutionPath.Count; i++)
            {
                Vector3 point = result.SolutionPath[i];
                int cellX = Mathf.RoundToInt((point.x - origin.x) / cellSize);
                int cellY = Mathf.RoundToInt((point.z - origin.z) / cellSize);
                if (cellX < 0 || cellY < 0 || cellX >= grid.Width || cellY >= grid.Height)
                {
                    continue;
                }

                int pxStart = Mathf.FloorToInt(cellX * pixelsPerCellX);
                int pxEnd = Mathf.Max(pxStart, Mathf.CeilToInt((cellX + 1) * pixelsPerCellX) - 1);
                int pyStart = Mathf.FloorToInt(cellY * pixelsPerCellY);
                int pyEnd = Mathf.Max(pyStart, Mathf.CeilToInt((cellY + 1) * pixelsPerCellY) - 1);

                for (int py = pyStart; py <= pyEnd && py < textureHeight; py++)
                {
                    if (py < 0)
                    {
                        continue;
                    }

                    int rowStart = py * textureWidth;
                    for (int px = pxStart; px <= pxEnd && px < textureWidth; px++)
                    {
                        if (px < 0)
                        {
                            continue;
                        }

                        pixels[rowStart + px] = _solutionColor;
                    }
                }
            }
        }

        private void RebuildQuad(MazeResult result)
        {
            MazeGrid grid = result.Grid;
            float width = grid.Width * result.CellSize;
            float height = grid.Height * result.CellSize;
            float left = result.Origin.x - result.CellSize * 0.5f;
            float bottom = result.Origin.z - result.CellSize * 0.5f;

            DestroyMesh(_quadMesh);
            _quadMesh = new Mesh { name = "Minimap Quad", hideFlags = HideFlags.DontSave };
            _quadMesh.vertices = new[]
            {
                new Vector3(left, bottom, 0f),
                new Vector3(left + width, bottom, 0f),
                new Vector3(left + width, bottom + height, 0f),
                new Vector3(left, bottom + height, 0f),
            };
            _quadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            _quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quadMesh.RecalculateBounds();

            _mapFilter.sharedMesh = _quadMesh;
        }

        private void UpdatePlayerMarker(MazeResult result, Transform root)
        {
            if (_playerMarker == null)
            {
                return;
            }

            Vector3 worldPosition = _player != null ? _player.transform.position : root.TransformPoint(result.SpawnPosition);
            Vector3 local = root.InverseTransformPoint(worldPosition);
            _playerMarker.localPosition = new Vector3(local.x, local.z, -1f);

            Vector3 forward = _player != null
                ? root.InverseTransformDirection(_player.transform.forward)
                : Vector3.forward;
            float angle = Mathf.Atan2(-forward.x, forward.z) * Mathf.Rad2Deg;
            _playerMarker.localRotation = Quaternion.Euler(0f, 0f, _rotateWithPlayer ? angle - _cameraAngle : angle);

            float size = result.CellSize * _markerSizeCells;
            _playerMarker.localScale = new Vector3(size * 0.5f, size, 1f);
        }

        private void UpdateGoalMarker(MazeResult result)
        {
            if (_goalMarker == null)
            {
                return;
            }

            _goalMarker.localPosition = new Vector3(result.ExitPosition.x, result.ExitPosition.z, -1.5f);
            _goalMarker.localRotation = Quaternion.identity;
            float size = result.CellSize * 1.4f;
            _goalMarker.localScale = new Vector3(size, size, 1f);
        }

        // ---------------------------------------------------------------------
        // Asset lifetime
        // ---------------------------------------------------------------------

        private void ReleaseTexture()
        {
            if (_texture == null)
            {
                return;
            }

            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            _texture.Release();
            DestroyAsset(_texture);
            _texture = null;
        }

        private void DestroyMesh(UnityEngine.Object mesh)
        {
            DestroyAsset(mesh);
        }

        private void DestroyTexture(UnityEngine.Object texture)
        {
            DestroyAsset(texture);
        }

        private void DestroyMaterial(Material material)
        {
            DestroyAsset(material);
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
    }
}
