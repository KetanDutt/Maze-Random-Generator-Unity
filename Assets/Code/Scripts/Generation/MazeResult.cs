using System.Collections.Generic;
using Maze.Core;
using Maze.Rendering;
using UnityEngine;

namespace Maze.Generation
{
    /// <summary>
    /// Everything a generated maze consists of: the logical grid, the baked mesh, the spawn/exit
    /// anchors and the statistics. Instances are immutable from the outside; the generator creates a
    /// new result for every generation and disposes the meshes of the previous one.
    /// </summary>
    public sealed class MazeResult
    {
        internal MazeResult(MazeGrid grid, MazeStatistics statistics, MazeMeshData meshData, Vector3 origin,
            float cellSize, float wallHeight, Vector3 spawnPosition, Quaternion spawnRotation,
            Vector3 exitPosition, List<Vector3> solutionPath, double generationMilliseconds)
        {
            Grid = grid;
            Statistics = statistics;
            MeshData = meshData;
            Origin = origin;
            CellSize = cellSize;
            WallHeight = wallHeight;
            SpawnPosition = spawnPosition;
            SpawnRotation = spawnRotation;
            ExitPosition = exitPosition;
            SolutionPath = solutionPath;
            GenerationMilliseconds = generationMilliseconds;
        }

        /// <summary>The carved grid.</summary>
        public MazeGrid Grid { get; }

        /// <summary>Structural statistics of the maze.</summary>
        public MazeStatistics Statistics { get; }

        /// <summary>The baked mesh (walls in submesh 0, floor in submesh 1) or <c>null</c>.</summary>
        public MazeMeshData MeshData { get; }

        /// <summary>World position of the centre of cell (0, 0).</summary>
        public Vector3 Origin { get; }

        /// <summary>Size of a cell in world units.</summary>
        public float CellSize { get; }

        /// <summary>Height of the walls in world units.</summary>
        public float WallHeight { get; }

        /// <summary>Where the player should be placed (centre of the entrance room, on the floor).</summary>
        public Vector3 SpawnPosition { get; }

        /// <summary>Direction the player should look at after spawning (into the maze).</summary>
        public Quaternion SpawnRotation { get; }

        /// <summary>Centre of the exit room, or the farthest room for a closed maze.</summary>
        public Vector3 ExitPosition { get; }

        /// <summary>Shortest route from the entrance to the exit as world positions on the floor.</summary>
        public IReadOnlyList<Vector3> SolutionPath { get; }

        /// <summary>Total time the generation and the baking took, in milliseconds.</summary>
        public double GenerationMilliseconds { get; }

        /// <summary><c>true</c> when the maze has at least one opening.</summary>
        public bool HasOpenings
        {
            get { return Grid.Openings.Count > 0; }
        }

        /// <summary><c>true</c> when a solution path was found.</summary>
        public bool HasSolution
        {
            get { return SolutionPath != null && SolutionPath.Count > 1; }
        }

        /// <summary>Centre of a cell in world space (on the floor).</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(Origin.x + cell.x * CellSize, 0f, Origin.z + cell.y * CellSize);
        }

        /// <summary>Grid cell that contains <paramref name="worldPosition"/>.</summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.RoundToInt((worldPosition.x - Origin.x) / CellSize),
                Mathf.RoundToInt((worldPosition.z - Origin.z) / CellSize));
        }

        /// <summary>World size of the maze (width, height 0, depth).</summary>
        public Vector3 WorldSize
        {
            get { return new Vector3(Grid.Width * CellSize, WallHeight, Grid.Height * CellSize); }
        }

        /// <summary>Centre of the maze in world space.</summary>
        public Vector3 WorldCenter
        {
            get
            {
                return new Vector3(
                    Origin.x + (Grid.Width - 1) * CellSize * 0.5f,
                    WallHeight * 0.5f,
                    Origin.z + (Grid.Height - 1) * CellSize * 0.5f);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "MazeResult(" + Grid + ", seed " + Statistics.Seed + ")";
        }
    }
}
