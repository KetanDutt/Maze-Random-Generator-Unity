using System;
using System.Collections.Generic;
using Maze.Core;
using Maze.Generation;
using Maze.Rendering;
using UnityEngine;
using UnityEngine.Events;

namespace Maze
{
    /// <summary>How the generated maze is turned into scene objects.</summary>
    public enum MazeRenderMode
    {
        /// <summary>A single mesh (plus an optional floor) - one draw call, recommended.</summary>
        Mesh = 0,

        /// <summary>One instance of <see cref="MazeGenerator.WallPrefab"/> per wall cell.</summary>
        Prefabs = 1,
    }

    /// <summary>
    /// The main component of the project: generates a random maze and turns it into scene objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generation is split in two halves. <see cref="MazeGeneratorCore"/> carves the logical
    /// <see cref="MazeGrid"/> (pure C#, no Unity objects, fully deterministic for a given seed) and
    /// this component bakes it into a mesh or into prefab instances and raises events.
    /// </para>
    /// <para>
    /// Everything the component creates at runtime is destroyed when a new maze is generated or the
    /// component is destroyed, so regenerating in a loop does not leak meshes or materials.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Maze/Maze Generator")]
    [DisallowMultipleComponent]
    public sealed class MazeGenerator : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Serialised state
        // ---------------------------------------------------------------------

        [SerializeField]
        [Tooltip("Structure of the maze: size, algorithm, seed, braiding and openings.")]
        private MazeGeneratorSettings _settings = new MazeGeneratorSettings();

        [SerializeField]
        [Tooltip("Mesh mode bakes everything into one mesh, prefab mode instantiates one wall object per cell.")]
        private MazeRenderMode _renderMode = MazeRenderMode.Mesh;

        [SerializeField]
        [Tooltip("Wall prefab used in prefab mode (and as a fallback). The bundled prefab is a 5 unit cube.")]
        private GameObject _wallPrefab;

        [SerializeField]
        [Tooltip("Optional material override for the walls. Leave empty to let the palette build one.")]
        private Material _wallMaterial;

        [SerializeField]
        [Tooltip("Optional material override for the floor.")]
        private Material _floorMaterial;

        [SerializeField]
        [Tooltip("Colour preset applied to the generated maze.")]
        private MazePalette _palette = MazePalette.Slate;

        [SerializeField]
        [Tooltip("Cell size the wall prefab was modelled for; prefab instances are scaled to the cell size.")]
        private float _referencePrefabCellSize = 5f;

        [SerializeField]
        [Tooltip("Generate a maze automatically in Start().")]
        private bool _generateOnStart = true;

        [SerializeField]
        [Tooltip("Centre the maze on this transform instead of growing into +X/+Z.")]
        private bool _centerOnOrigin = true;

        [SerializeField]
        [Tooltip("Regenerate automatically every N seconds to create an attract mode (0 = off).")]
        private float _autoRegenerateSeconds;

        [SerializeField]
        [Tooltip("Log the statistics of every generated maze to the console.")]
        private bool _logStatistics;

        [SerializeField]
        [Tooltip("Raised after every successful generation. Hook up UI or gameplay logic here.")]
        private UnityEvent _onMazeGenerated = new UnityEvent();

        // ---------------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------------

        private readonly MazeGeneratorCore _core = new MazeGeneratorCore();
        private readonly List<GameObject> _wallPool = new List<GameObject>();
        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

        private Transform _meshContainer;
        private Transform _prefabContainer;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private int _lastSeed;
        private float _autoRegenerateTimer;

        // ---------------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------------

        /// <summary>Raised after a maze was generated and applied to the scene.</summary>
        public event Action<MazeResult> Generated;

        /// <summary>Raised when the generated objects are removed again.</summary>
        public event Action Cleared;

        // ---------------------------------------------------------------------
        // Properties
        // ---------------------------------------------------------------------

        /// <summary>The current maze, or <c>null</c> when nothing has been generated yet.</summary>
        public MazeResult Result { get; private set; }

        /// <summary><c>true</c> when a maze is currently in the scene.</summary>
        public bool HasResult
        {
            get { return Result != null; }
        }

        /// <summary>Editable settings; call <see cref="Generate"/> or <see cref="Regenerate"/> afterwards.</summary>
        public MazeGeneratorSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new MazeGeneratorSettings();
                }

                return _settings;
            }
        }

        /// <summary>Statistics of the current maze (or <c>null</c>).</summary>
        public MazeStatistics Statistics
        {
            get { return Result != null ? Result.Statistics : null; }
        }

        /// <summary>Seed that produced the current maze (0 before the first generation).</summary>
        public int LastSeed
        {
            get { return _lastSeed; }
        }

        /// <summary>How the maze is turned into scene objects.</summary>
        public MazeRenderMode RenderMode
        {
            get { return _renderMode; }
            set { _renderMode = value; }
        }

        /// <summary>Colour preset applied to the generated maze.</summary>
        public MazePalette Palette
        {
            get { return _palette; }
            set { ApplyPalette(value); }
        }

        /// <summary>Wall prefab used by prefab mode.</summary>
        public GameObject WallPrefab
        {
            get { return _wallPrefab; }
            set { _wallPrefab = value; }
        }

        /// <summary>Number of wall objects that are currently pooled in prefab mode.</summary>
        public int PooledWallCount
        {
            get { return _wallPool.Count; }
        }

        // ---------------------------------------------------------------------
        // Unity messages
        // ---------------------------------------------------------------------

        private void Start()
        {
            if (_generateOnStart)
            {
                Generate();
            }
        }

        private void Update()
        {
            if (_autoRegenerateSeconds <= 0f)
            {
                return;
            }

            _autoRegenerateTimer += Time.deltaTime;
            if (_autoRegenerateTimer >= _autoRegenerateSeconds)
            {
                _autoRegenerateTimer = 0f;
                Generate();
            }
        }

        private void OnValidate()
        {
            Settings.Sanitize();
            _referencePrefabCellSize = Mathf.Max(0.01f, _referencePrefabCellSize);
            _autoRegenerateSeconds = Mathf.Max(0f, _autoRegenerateSeconds);
        }

        private void OnDestroy()
        {
            Clear();
        }

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Generates a new maze from the current settings. A fresh seed is drawn automatically when
        /// <see cref="MazeGeneratorSettings.UseRandomSeed"/> is enabled.
        /// </summary>
        public MazeResult Generate()
        {
            return Generate(Settings.ResolveSeed());
        }

        /// <summary>Generates a maze with an explicit seed.</summary>
        public MazeResult Generate(int seed)
        {
            Settings.Sanitize();
            MazeGenerationOutput output = _core.Generate(Settings, seed);
            _lastSeed = output.Seed;

            MazeResult result = BakeResult(output);
            ApplyResult(result);

            if (_logStatistics)
            {
                Debug.Log("[Maze] " + result.Statistics.ToSummary(), this);
            }

            UnityEvent onGenerated = _onMazeGenerated;
            if (onGenerated != null)
            {
                onGenerated.Invoke();
            }

            Action<MazeResult> handler = Generated;
            if (handler != null)
            {
                handler(result);
            }

            return result;
        }

        /// <summary>Generates the same maze again (same seed and settings).</summary>
        public MazeResult Regenerate()
        {
            return Generate(_lastSeed);
        }

        /// <summary>Removes the generated objects and runtime assets from the scene.</summary>
        public void Clear()
        {
            DisposeCurrentResult();

            if (_meshContainer != null)
            {
                DestroyObject(_meshContainer.gameObject);
                _meshContainer = null;
            }

            if (_prefabContainer != null)
            {
                DestroyObject(_prefabContainer.gameObject);
                _prefabContainer = null;
            }

            _meshFilter = null;
            _meshRenderer = null;
            _meshCollider = null;
            _wallPool.Clear();
            Result = null;

            Action handler = Cleared;
            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>Applies a palette and refreshes the maze that is currently in the scene.</summary>
        public void ApplyPalette(MazePalette palette)
        {
            _palette = palette;
            RefreshAppearance();
        }

        /// <summary>Re-applies the palette and the material overrides to the current maze.</summary>
        public void RefreshAppearance()
        {
            if (Result == null)
            {
                return;
            }

            ApplyMaterials(Result);

            for (int i = 0; i < _wallPool.Count; i++)
            {
                ApplyWallMaterialToRenderers(_wallPool[i], _wallMaterial);
            }
        }

        /// <summary>Changes the algorithm, regenerates and returns the result.</summary>
        public MazeResult SetAlgorithm(MazeAlgorithmKind algorithm)
        {
            Settings.Algorithm = algorithm;
            return Generate();
        }

        /// <summary>Changes the maze size (kept odd automatically), regenerates and returns the result.</summary>
        public MazeResult SetSize(int width, int height)
        {
            Settings.MazeWidth = width;
            Settings.MazeHeight = height;
            return Generate();
        }

        /// <summary>Sets the braid factor, regenerates and returns the result.</summary>
        public MazeResult SetBraidFactor(float factor)
        {
            Settings.BraidFactor = factor;
            return Generate();
        }

        /// <summary>Uses a random seed from now on and generates a new maze.</summary>
        public MazeResult GenerateWithNewSeed()
        {
            Settings.UseRandomSeed = true;
            return Generate();
        }

        /// <summary>Uses a fixed seed from now on and generates a new maze.</summary>
        public MazeResult GenerateWithSeed(int seed)
        {
            Settings.UseRandomSeed = false;
            Settings.Seed = seed;
            return Generate(seed);
        }

        /// <summary><c>true</c> when the world position is inside a passage cell of the current maze.</summary>
        public bool IsWalkable(Vector3 worldPosition)
        {
            if (Result == null)
            {
                return false;
            }

            Vector3 local = transform.InverseTransformPoint(worldPosition);
            Vector2Int cell = Result.WorldToCell(local);
            return Result.Grid.IsPassage(cell.x, cell.y);
        }

        /// <summary>Converts a world position into the grid cell of the current maze.</summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            if (Result == null)
            {
                return new Vector2Int(-1, -1);
            }

            return Result.WorldToCell(transform.InverseTransformPoint(worldPosition));
        }

        // ---------------------------------------------------------------------
        // Baking / scene application
        // ---------------------------------------------------------------------

        private MazeResult BakeResult(MazeGenerationOutput output)
        {
            bool includeFloor = Settings.GenerateFloor && _renderMode == MazeRenderMode.Mesh;
            return MazeBaker.Bake(output.Grid, Settings, output.AlgorithmId, output.Seed, output.Milliseconds,
                includeFloor, _centerOnOrigin);
        }

        private void ApplyResult(MazeResult result)
        {
            // The previous mesh has to go, otherwise regenerating in a loop leaks one mesh per frame.
            DisposeCurrentResult();

            if (_renderMode == MazeRenderMode.Mesh || _wallPrefab == null)
            {
                if (_renderMode == MazeRenderMode.Prefabs && _wallPrefab == null)
                {
                    Debug.LogWarning("[Maze] Prefab mode needs a wall prefab - falling back to mesh mode.", this);
                }

                ApplyMeshResult(result);
            }
            else
            {
                ApplyPrefabResult(result);
            }

            Result = result;
        }

        private void ApplyMeshResult(MazeResult result)
        {
            EnsureMeshContainer();
            EnsurePrefabContainer();
            _prefabContainer.gameObject.SetActive(false);
            _meshContainer.gameObject.SetActive(true);

            Mesh mesh = result.MeshData.Mesh;
            if (mesh == null)
            {
                Debug.LogError("[Maze] No mesh was generated - the maze is empty.", this);
                return;
            }

            if (!Application.isPlaying)
            {
                // Preview generation: keep the mesh out of the saved scene.
                mesh.hideFlags = HideFlags.DontSave;
            }

            _meshFilter.sharedMesh = mesh;
            _meshCollider.sharedMesh = null; // required before swapping, otherwise Unity keeps the old collider
            _meshCollider.sharedMesh = mesh;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            ApplyMaterials(result);
            HideGeneratedObjectsIfNotPlaying(_meshContainer.gameObject);
        }

        private void ApplyMaterials(MazeResult result)
        {
            MazePaletteDefinition palette = MazePalettes.Get(_palette);
            List<Material> previousMaterials = new List<Material>(_runtimeMaterials);
            _runtimeMaterials.Clear();

            Material wall = _wallMaterial;
            Material floor = _floorMaterial;

            if (!palette.KeepsTexture || wall == null)
            {
                wall = MazeMaterialFactory.Create("Maze Wall", palette.WallColor, null, palette.WallSmoothness);
                if (wall != null)
                {
                    _runtimeMaterials.Add(wall);
                }
            }

            if (!palette.KeepsTexture || floor == null)
            {
                floor = MazeMaterialFactory.Create("Maze Floor", palette.FloorColor, null, palette.FloorSmoothness);
                if (floor != null)
                {
                    _runtimeMaterials.Add(floor);
                }
            }

            if (_meshRenderer != null)
            {
                if (result.MeshData.HasFloor && floor != null)
                {
                    _meshRenderer.sharedMaterials = new[] { wall, floor };
                }
                else
                {
                    _meshRenderer.sharedMaterials = new[] { wall };
                }
            }

            for (int i = 0; i < previousMaterials.Count; i++)
            {
                DestroyObject(previousMaterials[i]);
            }
        }

        private void ApplyPrefabResult(MazeResult result)
        {
            EnsureMeshContainer();
            EnsurePrefabContainer();
            _meshContainer.gameObject.SetActive(false);
            _prefabContainer.gameObject.SetActive(true);

            _meshRenderer.sharedMaterial = null;
            _meshCollider.sharedMesh = null;

            MazeGrid grid = result.Grid;
            float scale = _referencePrefabCellSize > 0f ? result.CellSize / _referencePrefabCellSize : 1f;
            int instanceIndex = 0;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!grid.IsWall(x, y))
                    {
                        continue;
                    }

                    GameObject instance;
                    if (instanceIndex < _wallPool.Count)
                    {
                        instance = _wallPool[instanceIndex];
                    }
                    else
                    {
                        instance = Instantiate(_wallPrefab, _prefabContainer);
                        instance.name = "Wall " + instanceIndex;
                        _wallPool.Add(instance);
                    }

                    Transform instanceTransform = instance.transform;
                    instanceTransform.localPosition = result.CellToWorld(new Vector2Int(x, y));
                    instanceTransform.localRotation = Quaternion.identity;
                    instanceTransform.localScale = Vector3.one * scale;
                    if (!instance.activeSelf)
                    {
                        instance.SetActive(true);
                    }

                    ApplyWallMaterialToRenderers(instance, _wallMaterial);
                    instanceIndex++;
                }
            }

            // Park the unused instances of the pool.
            for (int i = instanceIndex; i < _wallPool.Count; i++)
            {
                if (_wallPool[i].activeSelf)
                {
                    _wallPool[i].SetActive(false);
                }
            }

            HideGeneratedObjectsIfNotPlaying(_prefabContainer.gameObject);
        }

        private void ApplyWallMaterialToRenderers(GameObject instance, Material overrideMaterial)
        {
            if (instance == null)
            {
                return;
            }

            MazePaletteDefinition palette = MazePalettes.Get(_palette);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

            if (overrideMaterial != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = overrideMaterial;
                }

                return;
            }

            // Tint the prefab renderers through a property block so the prefab asset itself is never
            // modified. The texture palette keeps the original colours (white tint).
            Color tint = palette.KeepsTexture ? Color.white : palette.WallColor;
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_Color", tint);
                _propertyBlock.SetColor("_BaseColor", tint);
                renderers[i].SetPropertyBlock(_propertyBlock);
            }
        }

        // ---------------------------------------------------------------------
        // Lifetime helpers
        // ---------------------------------------------------------------------

        private void EnsureMeshContainer()
        {
            if (_meshContainer != null)
            {
                return;
            }

            GameObject container = new GameObject("Maze Mesh");
            container.transform.SetParent(transform, false);
            _meshContainer = container.transform;
            _meshFilter = container.AddComponent<MeshFilter>();
            _meshRenderer = container.AddComponent<MeshRenderer>();
            _meshCollider = container.AddComponent<MeshCollider>();
            _meshCollider.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
                                           MeshColliderCookingOptions.WeldColocatedVertices;
        }

        private void EnsurePrefabContainer()
        {
            if (_prefabContainer != null)
            {
                return;
            }

            GameObject container = new GameObject("Maze Walls (Prefabs)");
            container.transform.SetParent(transform, false);
            _prefabContainer = container.transform;
        }

        /// <summary>Destroys the mesh of the current result (the containers stay alive).</summary>
        private void DisposeCurrentResult()
        {
            if (Result == null)
            {
                return;
            }

            if (_meshFilter != null)
            {
                _meshFilter.sharedMesh = null;
            }

            if (_meshCollider != null)
            {
                _meshCollider.sharedMesh = null;
            }

            DestroyObject(Result.MeshData.Mesh);

            for (int i = 0; i < _runtimeMaterials.Count; i++)
            {
                DestroyObject(_runtimeMaterials[i]);
            }

            _runtimeMaterials.Clear();
        }

        private void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Objects generated outside of play mode are marked so they are not written into the scene
        /// file: they are pure previews and are rebuilt on the next generation.
        /// </summary>
        private static void HideGeneratedObjectsIfNotPlaying(GameObject target)
        {
            target.hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave;
        }
    }
}
