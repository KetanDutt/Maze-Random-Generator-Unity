using System.Collections.Generic;
using Maze.Analysis;
using Maze.Core;
using Maze.Rendering;
using UnityEngine;

namespace Maze.Generation
{
    /// <summary>
    /// Turns a carved <see cref="MazeGrid"/> into a <see cref="MazeResult"/>: builds the mesh,
    /// computes the solution path, the spawn/exit anchor points and the statistics.
    /// </summary>
    public static class MazeBaker
    {
        /// <summary>Height above the floor at which the solution path is drawn.</summary>
        public const float PathHeight = 0.05f;

        /// <summary>Bakes everything the scene needs from a carved grid.</summary>
        public static MazeResult Bake(MazeGrid grid, MazeGeneratorSettings settings, string algorithmId, int seed,
            double algorithmMilliseconds, bool includeFloor, bool center = true)
        {
            float cellSize = settings.CellSize;
            Vector3 origin = ComputeOrigin(grid, cellSize, center);

            MazeMeshData meshData = MazeMeshBuilder.Build(grid, cellSize, settings.WallHeight, includeFloor, origin);

            // Spawn: the entrance room if the maze has openings, otherwise the first passage.
            Vector2Int spawnCell = grid.Openings.Count > 0
                ? grid.Openings[0].RoomCell
                : MazePathfinder.FindFirstPassage(grid);

            // Exit: the second opening, otherwise the cell farthest away from the spawn.
            Vector2Int exitCell;
            if (grid.Openings.Count > 1)
            {
                exitCell = grid.Openings[grid.Openings.Count - 1].RoomCell;
            }
            else
            {
                Vector2Int farthest;
                MazePathfinder.MeasureDepth(grid, spawnCell, out farthest);
                exitCell = farthest;
            }

            List<Vector3> solutionPath = new List<Vector3>();
            List<Vector2Int> cells = MazePathfinder.FindPath(grid, spawnCell, exitCell);
            if (cells != null)
            {
                solutionPath.Capacity = cells.Count;
                for (int i = 0; i < cells.Count; i++)
                {
                    Vector3 point = CellToWorld(origin, cellSize, cells[i]);
                    point.y = PathHeight;
                    solutionPath.Add(point);
                }
            }

            Vector3 spawnPosition = CellToWorld(origin, cellSize, spawnCell);
            spawnPosition.y = 0f;

            Quaternion spawnRotation = Quaternion.identity;
            if (grid.Openings.Count > 0)
            {
                MazeDirection side = grid.Openings[0].Side;
                Vector2Int inward = side.ToOffset() * -1;
                if (inward.sqrMagnitude > 0)
                {
                    spawnRotation = Quaternion.LookRotation(new Vector3(inward.x, 0f, inward.y), Vector3.up);
                }
            }

            Vector3 exitPosition = CellToWorld(origin, cellSize, exitCell);
            exitPosition.y = 0f;

            MazeStatistics statistics = MazeStatistics.Compute(grid, algorithmId, seed, solutionPath.Count);

            double totalMilliseconds = algorithmMilliseconds;
            if (totalMilliseconds <= 0)
            {
                totalMilliseconds = 0.0001; // keep the value meaningful for the UI
            }

            MazeResult result = new MazeResult(grid, statistics, meshData, origin, cellSize, settings.WallHeight,
                spawnPosition, spawnRotation, exitPosition, solutionPath, totalMilliseconds);

            // Sanity guard: a room that is entirely walled in would break the gameplay layer.
            if (!statistics.IsFullyConnected)
            {
                Debug.LogWarning("[Maze] The generated maze is not fully connected (seed " + seed +
                                 ", " + grid.Width + "x" + grid.Height + "). Please report this seed.");
            }

            return result;
        }

        /// <summary>
        /// World offset of cell (0, 0) so that the maze is centred on the parent transform
        /// (or starts at its corner when <paramref name="center"/> is false).
        /// </summary>
        public static Vector3 ComputeOrigin(MazeGrid grid, float cellSize, bool center)
        {
            if (!center)
            {
                return Vector3.zero;
            }

            return new Vector3(
                -(grid.Width - 1) * cellSize * 0.5f,
                0f,
                -(grid.Height - 1) * cellSize * 0.5f);
        }

        private static Vector3 CellToWorld(Vector3 origin, float cellSize, Vector2Int cell)
        {
            return new Vector3(origin.x + cell.x * cellSize, 0f, origin.z + cell.y * cellSize);
        }
    }
}
