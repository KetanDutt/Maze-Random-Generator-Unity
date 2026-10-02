using System;
using UnityEngine;

namespace Maze.Core
{
    /// <summary>How the generator cuts entrances and exits into the outer wall.</summary>
    public enum MazeOpeningMode
    {
        /// <summary>The maze is completely closed - the classic "perfect maze" look.</summary>
        None = 0,

        /// <summary>One opening per side, placed at a random room of that side.</summary>
        Random = 1,

        /// <summary>Exactly one entrance and one exit at the configured sides.</summary>
        FixedEntranceAndExit = 2,
    }

    /// <summary>Available maze generation algorithms.</summary>
    public enum MazeAlgorithmKind
    {
        /// <summary>Randomized Prim's algorithm - short dead ends, many of them.</summary>
        RandomizedPrim = 0,

        /// <summary>Depth first (recursive backtracker) - long winding corridors.</summary>
        DepthFirst = 1,

        /// <summary>Randomized Kruskal's algorithm - uniform spanning tree.</summary>
        RandomizedKruskal = 2,

        /// <summary>Binary tree - fast and simple, strongly biased towards two corners.</summary>
        BinaryTree = 3,
    }

    /// <summary>
    /// Every structural parameter of a maze. Instances are serializable so the settings can be
    /// edited in the inspector and stored in the scene; the generator core only ever reads them.
    /// </summary>
    [Serializable]
    public sealed class MazeGeneratorSettings
    {
        /// <summary>Smallest width/height the user can pick.</summary>
        public const int MinimumSize = 5;

        [SerializeField, Tooltip("Grid width in cells. Kept odd so that the room lattice fits.")]
        private int _mazeWidth = 21;

        [SerializeField, Tooltip("Grid height in cells. Kept odd so that the room lattice fits.")]
        private int _mazeHeight = 21;

        [SerializeField, Tooltip("Algorithm used to carve the maze.")]
        private MazeAlgorithmKind _algorithm = MazeAlgorithmKind.RandomizedPrim;

        [SerializeField, Tooltip("When enabled a fresh seed is drawn for every generation.")]
        private bool _useRandomSeed = true;

        [SerializeField, Tooltip("Seed used when 'Use Random Seed' is disabled. Share it to share the maze.")]
        private int _seed = 12345;

        [SerializeField, Range(0f, 1f), Tooltip("Removes that fraction of the dead ends by opening extra corridors (creates loops).")]
        private float _braidFactor = 0.25f;

        [SerializeField, Tooltip("Entrance/exit handling.")]
        private MazeOpeningMode _openingMode = MazeOpeningMode.FixedEntranceAndExit;

        [SerializeField, Range(0, 4), Tooltip("How many openings are cut when the mode is 'Random'.")]
        private int _randomOpeningCount = 2;

        [SerializeField, Tooltip("Side of the fixed entrance.")]
        private MazeDirection _entranceSide = MazeDirection.North;

        [SerializeField, Tooltip("Side of the fixed exit.")]
        private MazeDirection _exitSide = MazeDirection.South;

        [SerializeField, Range(1, 4), Tooltip("Thickness of the forced wall ring around the maze, in cells.")]
        private int _padding = 1;

        [SerializeField, Range(1f, 50f), Tooltip("Size of one cell in world units.")]
        private float _cellSize = 5f;

        [SerializeField, Range(1f, 50f), Tooltip("Height of the walls in world units.")]
        private float _wallHeight = 5f;

        [SerializeField, Tooltip("Adds a floor plane made of passage cells to the generated mesh.")]
        private bool _generateFloor = true;

        /// <summary>Creates settings for a maze with the default look.</summary>
        public MazeGeneratorSettings()
        {
        }

        /// <summary>Creates a copy of <paramref name="other"/>.</summary>
        public MazeGeneratorSettings(MazeGeneratorSettings other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            _mazeWidth = other._mazeWidth;
            _mazeHeight = other._mazeHeight;
            _algorithm = other._algorithm;
            _useRandomSeed = other._useRandomSeed;
            _seed = other._seed;
            _braidFactor = other._braidFactor;
            _openingMode = other._openingMode;
            _randomOpeningCount = other._randomOpeningCount;
            _entranceSide = other._entranceSide;
            _exitSide = other._exitSide;
            _padding = other._padding;
            _cellSize = other._cellSize;
            _wallHeight = other._wallHeight;
            _generateFloor = other._generateFloor;
        }

        // ---------------------------------------------------------------------
        // Properties
        // ---------------------------------------------------------------------

        /// <summary>Requested grid width (may be adjusted by <see cref="Sanitize"/>).</summary>
        public int MazeWidth
        {
            get { return _mazeWidth; }
            set { _mazeWidth = value; }
        }

        /// <summary>Requested grid height (may be adjusted by <see cref="Sanitize"/>).</summary>
        public int MazeHeight
        {
            get { return _mazeHeight; }
            set { _mazeHeight = value; }
        }

        /// <summary>Algorithm used to carve the maze.</summary>
        public MazeAlgorithmKind Algorithm
        {
            get { return _algorithm; }
            set { _algorithm = value; }
        }

        /// <summary>Whether a fresh seed is drawn for every generation.</summary>
        public bool UseRandomSeed
        {
            get { return _useRandomSeed; }
            set { _useRandomSeed = value; }
        }

        /// <summary>Seed used when <see cref="UseRandomSeed"/> is disabled.</summary>
        public int Seed
        {
            get { return _seed; }
            set { _seed = value; }
        }

        /// <summary>Fraction of the dead ends that get braided away (0 = perfect maze).</summary>
        public float BraidFactor
        {
            get { return _braidFactor; }
            set { _braidFactor = Mathf.Clamp01(value); }
        }

        /// <summary>Entrance/exit handling.</summary>
        public MazeOpeningMode OpeningMode
        {
            get { return _openingMode; }
            set { _openingMode = value; }
        }

        /// <summary>Number of openings used by <see cref="MazeOpeningMode.Random"/>.</summary>
        public int RandomOpeningCount
        {
            get { return _randomOpeningCount; }
            set { _randomOpeningCount = Mathf.Clamp(value, 0, 4); }
        }

        /// <summary>Side used for the fixed entrance.</summary>
        public MazeDirection EntranceSide
        {
            get { return _entranceSide; }
            set { _entranceSide = value; }
        }

        /// <summary>Side used for the fixed exit.</summary>
        public MazeDirection ExitSide
        {
            get { return _exitSide; }
            set { _exitSide = value; }
        }

        /// <summary>Thickness of the forced wall ring, in cells.</summary>
        public int Padding
        {
            get { return _padding; }
            set { _padding = Mathf.Clamp(value, 1, 4); }
        }

        /// <summary>Size of a cell in world units.</summary>
        public float CellSize
        {
            get { return _cellSize; }
            set { _cellSize = Mathf.Clamp(value, 1f, 50f); }
        }

        /// <summary>Height of the walls in world units.</summary>
        public float WallHeight
        {
            get { return _wallHeight; }
            set { _wallHeight = Mathf.Clamp(value, 1f, 50f); }
        }

        /// <summary>Whether the generated mesh contains a floor.</summary>
        public bool GenerateFloor
        {
            get { return _generateFloor; }
            set { _generateFloor = value; }
        }

        /// <summary>Width after clamping/rounding; this is what the generator uses.</summary>
        public int Width
        {
            get { return MazeGrid.NormalizeDimension(_mazeWidth, _padding); }
        }

        /// <summary>Height after clamping/rounding; this is what the generator uses.</summary>
        public int Height
        {
            get { return MazeGrid.NormalizeDimension(_mazeHeight, _padding); }
        }

        /// <summary>Number of rooms along the x axis.</summary>
        public int RoomCountX
        {
            get { return (Width - 2 * _padding + 1) / 2; }
        }

        /// <summary>Number of rooms along the y axis.</summary>
        public int RoomCountY
        {
            get { return (Height - 2 * _padding + 1) / 2; }
        }

        // ---------------------------------------------------------------------
        // Behaviour
        // ---------------------------------------------------------------------

        /// <summary>
        /// Returns the seed for the next generation: either the fixed seed or a fresh random one.
        /// </summary>
        public int ResolveSeed()
        {
            return _useRandomSeed ? MazeRandom.CreateSeed() : _seed;
        }

        /// <summary>
        /// Clamps every value into a range the generator can handle and makes the dimensions odd.
        /// Called automatically by <c>MazeGenerator</c>, so user code can be sloppy.
        /// </summary>
        public void Sanitize()
        {
            _padding = Mathf.Clamp(_padding, 1, 4);

            // The requested size is kept as it is (only clamped to the hard limits) so that the
            // inspector/HUD can tell the user that their even size was rounded up. The rounding to an
            // odd interior size happens in Width/Height, which is what the generator uses.
            _mazeWidth = Mathf.Clamp(_mazeWidth, MinimumSize, MazeGrid.MaximumDimension);
            _mazeHeight = Mathf.Clamp(_mazeHeight, MinimumSize, MazeGrid.MaximumDimension);
            _braidFactor = Mathf.Clamp01(_braidFactor);
            _randomOpeningCount = Mathf.Clamp(_randomOpeningCount, 0, 4);
            _cellSize = Mathf.Clamp(_cellSize, 1f, 50f);
            _wallHeight = Mathf.Clamp(_wallHeight, 1f, 50f);

            if (_openingMode == MazeOpeningMode.FixedEntranceAndExit && _entranceSide == _exitSide)
            {
                _exitSide = _entranceSide.Opposite();
            }
        }

        /// <summary><c>true</c> when the requested size had to be adjusted (rounding or clamping).</summary>
        public bool SizeWasAdjusted
        {
            get { return _mazeWidth != Width || _mazeHeight != Height; }
        }

        /// <summary>Human readable one line summary, used by logs and the inspector.</summary>
        public string Describe()
        {
            return Width + "x" + Height + " " + _algorithm + " seed " + _seed +
                   " braid " + _braidFactor.ToString("0.##") + (SizeWasAdjusted ? " (size adjusted)" : string.Empty);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Describe();
        }
    }
}
