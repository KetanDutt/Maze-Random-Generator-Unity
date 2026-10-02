using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maze.Core
{
    /// <summary>
    /// Cardinal direction on the maze grid. <see cref="North"/> increases the grid y axis,
    /// which is world +Z in the generated mesh.
    /// </summary>
    public enum MazeDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>Helpers for <see cref="MazeDirection"/>.</summary>
    public static class MazeDirections
    {
        /// <summary>Number of cardinal directions.</summary>
        public const int Count = 4;

        private static readonly MazeDirection[] AllDirections =
        {
            MazeDirection.North,
            MazeDirection.East,
            MazeDirection.South,
            MazeDirection.West,
        };

        /// <summary>All four cardinal directions (north, east, south, west).</summary>
        public static IReadOnlyList<MazeDirection> Values
        {
            get { return AllDirections; }
        }

        /// <summary>The direction that points the opposite way.</summary>
        public static MazeDirection Opposite(this MazeDirection direction)
        {
            return (MazeDirection)(((int)direction + 2) & 3);
        }

        /// <summary>Grid offset of the neighbour towards <paramref name="direction"/>.</summary>
        public static Vector2Int ToOffset(this MazeDirection direction)
        {
            switch (direction)
            {
                case MazeDirection.North: return new Vector2Int(0, 1);
                case MazeDirection.East: return new Vector2Int(1, 0);
                case MazeDirection.South: return new Vector2Int(0, -1);
                default: return new Vector2Int(-1, 0);
            }
        }

        /// <summary>Human readable name, useful for logs and UI.</summary>
        public static string ToDisplayName(this MazeDirection direction)
        {
            switch (direction)
            {
                case MazeDirection.North: return "North";
                case MazeDirection.East: return "East";
                case MazeDirection.South: return "South";
                default: return "West";
            }
        }

        /// <summary>Parses <c>"North"</c>, <c>"north"</c>, <c>"N"</c>, ... (used by the JSON import).</summary>
        public static bool TryParse(string value, out MazeDirection direction)
        {
            direction = MazeDirection.North;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            for (int i = 0; i < AllDirections.Length; i++)
            {
                MazeDirection candidate = AllDirections[i];
                if (string.Equals(candidate.ToDisplayName(), trimmed, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmed, candidate.ToDisplayName().Substring(0, 1), StringComparison.OrdinalIgnoreCase))
                {
                    direction = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Direction that best matches a grid offset.</summary>
        public static MazeDirection FromOffset(int dx, int dy)
        {
            if (dx > 0)
            {
                return MazeDirection.East;
            }

            if (dx < 0)
            {
                return MazeDirection.West;
            }

            return dy >= 0 ? MazeDirection.North : MazeDirection.South;
        }
    }
}
