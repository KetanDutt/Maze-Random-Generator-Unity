using System.Collections.Generic;
using Maze.Generation;
using Maze.Rendering;
using UnityEngine;

namespace Maze.Presentation
{
    /// <summary>
    /// Draws the shortest entrance/exit route (the "solution") and marks the entrance and the exit
    /// with floor discs. The path is computed by the generator, this component only renders it.
    /// </summary>
    [AddComponentMenu("Maze/Presentation/Maze Path Renderer")]
    public sealed class MazePathRenderer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Maze whose solution is drawn. Found automatically when left empty.")]
        private MazeGenerator _generator;

        [SerializeField, Range(0.02f, 1f), Tooltip("Line width as a fraction of the cell size.")]
        private float _widthFactor = 0.18f;

        [SerializeField, Tooltip("Also place a disc on the floor at the entrance and the exit.")]
        private bool _showMarkers = true;

        [SerializeField, Range(0.05f, 1f), Tooltip("Marker radius as a fraction of the cell size.")]
        private float _markerRadiusFactor = 0.3f;

        [SerializeField, Tooltip("Start with the path visible.")]
        private bool _visibleOnStart;

        private LineRenderer _line;
        private Material _lineMaterial;
        private Transform _markerRoot;
        private readonly List<GameObject> _markers = new List<GameObject>(2);
        private readonly List<Material> _materials = new List<Material>();

        /// <summary>Whether the solution path is currently shown.</summary>
        public bool IsVisible
        {
            get { return _line != null && _line.enabled; }
        }

        /// <summary>Shows or hides the solution path (and the markers).</summary>
        public void SetVisible(bool visible)
        {
            if (_line == null)
            {
                return;
            }

            _line.enabled = visible;
            if (_markerRoot != null)
            {
                _markerRoot.gameObject.SetActive(visible && _showMarkers);
            }
        }

        private void Awake()
        {
            if (_generator == null)
            {
                _generator = FindObjectOfType<MazeGenerator>();
            }
        }

        private void OnEnable()
        {
            if (_generator != null)
            {
                _generator.Generated += OnGenerated;
                _generator.Cleared += OnCleared;
                if (_generator.HasResult)
                {
                    OnGenerated(_generator.Result);
                }
            }
        }

        private void OnDisable()
        {
            if (_generator != null)
            {
                _generator.Generated -= OnGenerated;
                _generator.Cleared -= OnCleared;
            }
        }

        private void OnDestroy()
        {
            ReleaseAssets();
        }

        private void OnGenerated(MazeResult result)
        {
            Build(result);
            SetVisible(_visibleOnStart);
        }

        private void OnCleared()
        {
            if (_line != null)
            {
                _line.positionCount = 0;
            }

            for (int i = 0; i < _markers.Count; i++)
            {
                _markers[i].SetActive(false);
            }
        }

        // ---------------------------------------------------------------------
        // Building
        // ---------------------------------------------------------------------

        private void Build(MazeResult result)
        {
            ReleaseAssets();

            IReadOnlyList<Vector3> path = result.SolutionPath;
            if (path == null || path.Count < 2)
            {
                return;
            }

            MazePaletteDefinition palette = MazePalettes.Get(_generator != null ? _generator.Palette : MazePalette.Slate);
            _lineMaterial = MazeMaterialFactory.CreatePathMaterial("Maze Solution", palette.PathColor, palette.EmissivePath);
            if (_lineMaterial != null)
            {
                _materials.Add(_lineMaterial);
            }

            GameObject lineObject = new GameObject("Solution Path");
            lineObject.transform.SetParent(transform, false);
            lineObject.hideFlags = HideFlags.DontSave;
            _line = lineObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Stretch;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.widthMultiplier = Mathf.Max(0.05f, result.CellSize * _widthFactor);
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.sharedMaterial = _lineMaterial;
            _line.positionCount = path.Count;

            for (int i = 0; i < path.Count; i++)
            {
                _line.SetPosition(i, path[i]);
            }

            if (_showMarkers)
            {
                BuildMarkers(result, palette);
            }
        }

        private void BuildMarkers(MazeResult result, MazePaletteDefinition palette)
        {
            GameObject root = new GameObject("Solution Markers");
            root.transform.SetParent(transform, false);
            root.hideFlags = HideFlags.DontSave;
            _markerRoot = root.transform;

            float radius = Mathf.Max(0.1f, result.CellSize * _markerRadiusFactor);
            AddMarker(result.SpawnPosition, radius, palette.AccentColor, "Entrance");
            AddMarker(result.ExitPosition, radius, palette.PathColor, "Exit");
        }

        private void AddMarker(Vector3 localPosition, float radius, Color color, string name)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(_markerRoot, false);
            marker.transform.localPosition = localPosition + Vector3.up * 0.02f;
            marker.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            marker.hideFlags = HideFlags.DontSave;

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyObject(collider);
            }

            Material material = MazeMaterialFactory.CreatePathMaterial(name + " Marker", color, false);
            if (material != null)
            {
                _materials.Add(material);
                marker.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            _markers.Add(marker);
        }

        private void ReleaseAssets()
        {
            if (_line != null)
            {
                DestroyObject(_line.gameObject);
                _line = null;
            }

            if (_markerRoot != null)
            {
                DestroyObject(_markerRoot.gameObject);
                _markerRoot = null;
            }

            _markers.Clear();

            for (int i = 0; i < _materials.Count; i++)
            {
                DestroyObject(_materials[i]);
            }

            _materials.Clear();
        }

        /// <summary>Destroys a runtime object in play mode and an immediate object in edit mode.</summary>
        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
