using System;

namespace Maze.Core
{
    /// <summary>
    /// Small deterministic pseudo random number generator (xorshift128) used by every
    /// algorithm.
    /// </summary>
    /// <remarks>
    /// The same seed produces the exact same maze on every platform and every run, which is
    /// what makes seeds shareable. <see cref="System.Random"/> and <c>UnityEngine.Random</c>
    /// are deliberately avoided: their implementations are not guaranteed to be stable
    /// between Unity versions or platforms.
    /// </remarks>
    public sealed class MazeRandom
    {
        private const uint DefaultState = 0x9E3779B9u;

        private uint _x;
        private uint _y;
        private uint _z;
        private uint _w;

        /// <summary>Creates a generator for <paramref name="seed"/>.</summary>
        public MazeRandom(int seed)
        {
            Reset(seed);
        }

        /// <summary>The seed this generator was created with.</summary>
        public int Seed { get; private set; }

        /// <summary>Resets the generator so a new sequence can be produced.</summary>
        public void Reset(int seed)
        {
            Seed = seed;
            uint state = unchecked((uint)seed);
            if (state == 0u)
            {
                state = DefaultState;
            }

            // Scramble the seed so that close seeds (1, 2, 3, ...) do not produce
            // visibly related mazes.
            _x = state ^ 0xA5A5A5A5u;
            _y = state * 0x9E3779B1u + 0x85EBCA6Bu;
            _z = state ^ 0xC2B2AE35u;
            _w = state * 0x27D4EB2Fu + 0x165667B1u;
            if ((_x | _y | _z | _w) == 0u)
            {
                _x = DefaultState;
            }

            // Discard the first outputs so that the state is well mixed.
            for (int i = 0; i < 8; i++)
            {
                NextUInt();
            }
        }

        /// <summary>Returns the next 32 bit value from the sequence.</summary>
        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y;
            _y = _z;
            _z = _w;
            _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            return _w;
        }

        /// <summary>Returns a value in <c>[0, int.MaxValue)</c>.</summary>
        public int NextInt()
        {
            return (int)(NextUInt() >> 1);
        }

        /// <summary>Returns a value in <c>[minInclusive, maxExclusive)</c>.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        /// <summary>Returns a value in <c>[0, count)</c>.</summary>
        public int NextIndex(int count)
        {
            return count <= 1 ? 0 : Range(0, count);
        }

        /// <summary>Returns a float in <c>[0, 1)</c>.</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1.0f / 16777216.0f);
        }

        /// <summary>Returns <c>true</c> with the given probability.</summary>
        public bool Chance(float probability)
        {
            if (probability <= 0f)
            {
                return false;
            }

            if (probability >= 1f)
            {
                return true;
            }

            return NextFloat() < probability;
        }

        /// <summary>In-place Fisher-Yates shuffle.</summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException("items");
            }

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }

        /// <summary>Creates a seed that is unlikely to collide with recent ones.</summary>
        public static int CreateSeed()
        {
            return unchecked((int)DateTime.UtcNow.Ticks) ^ Environment.TickCount;
        }
    }
}
