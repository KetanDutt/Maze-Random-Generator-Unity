using System;
using System.Collections.Generic;
using System.Text;
using Maze.Core;

namespace Maze.Algorithms
{
    /// <summary>
    /// Single place that knows every available <see cref="IMazeAlgorithm"/>. Add an algorithm by
    /// implementing the interface, adding it to <see cref="MazeAlgorithmKind"/> and registering it
    /// here - the UI, the inspector and the exporters all read from this registry.
    /// </summary>
    public static class MazeAlgorithmRegistry
    {
        private static readonly IMazeAlgorithm[] Algorithms =
        {
            new RandomizedPrimAlgorithm(),
            new DepthFirstAlgorithm(),
            new RandomizedKruskalAlgorithm(),
            new BinaryTreeAlgorithm(),
        };

        /// <summary>Every registered algorithm, in the order of <see cref="MazeAlgorithmKind"/>.</summary>
        public static IReadOnlyList<IMazeAlgorithm> All
        {
            get { return Algorithms; }
        }

        /// <summary>Number of registered algorithms.</summary>
        public static int Count
        {
            get { return Algorithms.Length; }
        }

        /// <summary>Returns the implementation of <paramref name="kind"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">When the enum value has no implementation.</exception>
        public static IMazeAlgorithm Get(MazeAlgorithmKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= Algorithms.Length)
            {
                throw new ArgumentOutOfRangeException("kind", kind, "No algorithm registered for this kind.");
            }

            return Algorithms[index];
        }

        /// <summary>Name shown in the UI for <paramref name="kind"/>.</summary>
        public static string GetDisplayName(MazeAlgorithmKind kind)
        {
            return Get(kind).DisplayName;
        }

        /// <summary>Description shown in tooltips for <paramref name="kind"/>.</summary>
        public static string GetDescription(MazeAlgorithmKind kind)
        {
            return Get(kind).Description;
        }

        /// <summary>Parses an algorithm id (<c>prim</c>) or display name (<c>Randomized Prim</c>).</summary>
        public static bool TryParse(string value, out MazeAlgorithmKind kind)
        {
            kind = MazeAlgorithmKind.RandomizedPrim;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            for (int i = 0; i < Algorithms.Length; i++)
            {
                IMazeAlgorithm algorithm = Algorithms[i];
                if (string.Equals(algorithm.Id, trimmed, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(algorithm.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    kind = (MazeAlgorithmKind)i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>All algorithm display names, handy for building UI.</summary>
        public static string[] GetDisplayNames()
        {
            string[] names = new string[Algorithms.Length];
            for (int i = 0; i < Algorithms.Length; i++)
            {
                names[i] = Algorithms[i].DisplayName;
            }

            return names;
        }

        /// <summary>All algorithm ids, handy for save files and command line tools.</summary>
        public static string[] GetIds()
        {
            string[] ids = new string[Algorithms.Length];
            for (int i = 0; i < Algorithms.Length; i++)
            {
                ids[i] = Algorithms[i].Id;
            }

            return ids;
        }

        /// <summary>Comma separated list of the registered algorithm ids (used by the CLI docs).</summary>
        public static string GetIdList()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Algorithms.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Algorithms[i].Id);
            }

            return builder.ToString();
        }
    }
}
