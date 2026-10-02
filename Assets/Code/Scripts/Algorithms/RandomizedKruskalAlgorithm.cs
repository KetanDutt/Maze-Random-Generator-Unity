using Maze.Core;
using UnityEngine;

namespace Maze.Algorithms
{
    /// <summary>
    /// Randomized Kruskal's algorithm: shuffles all possible corridors and connects rooms whose
    /// sets are still disjoint. Produces a uniform spanning tree with a very even texture.
    /// </summary>
    public sealed class RandomizedKruskalAlgorithm : IMazeAlgorithm
    {
        private struct Edge
        {
            public int RoomA;
            public int RoomB;
            public int RoomX;
            public int RoomY;
            public MazeDirection Direction;
        }

        /// <inheritdoc />
        public string Id
        {
            get { return "kruskal"; }
        }

        /// <inheritdoc />
        public string DisplayName
        {
            get { return "Randomized Kruskal"; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Uniform spanning tree: even difficulty, balanced dead ends, no bias."; }
        }

        /// <inheritdoc />
        public int MinimumRoomCount
        {
            get { return 1; }
        }

        /// <inheritdoc />
        public void Generate(MazeGrid grid, MazeRandom random)
        {
            int roomCountX = grid.RoomCountX;
            int roomCountY = grid.RoomCountY;
            int roomCount = grid.RoomCount;

            // Every horizontal and vertical neighbour pair becomes a candidate corridor.
            int horizontalEdges = (roomCountX - 1) * roomCountY;
            int verticalEdges = roomCountX * (roomCountY - 1);
            Edge[] edges = new Edge[horizontalEdges + verticalEdges];
            int edgeCount = 0;

            for (int y = 0; y < roomCountY; y++)
            {
                for (int x = 0; x < roomCountX; x++)
                {
                    int index = y * roomCountX + x;
                    if (x + 1 < roomCountX)
                    {
                        edges[edgeCount++] = new Edge
                        {
                            RoomA = index,
                            RoomB = index + 1,
                            RoomX = x,
                            RoomY = y,
                            Direction = MazeDirection.East,
                        };
                    }

                    if (y + 1 < roomCountY)
                    {
                        edges[edgeCount++] = new Edge
                        {
                            RoomA = index,
                            RoomB = index + roomCountX,
                            RoomX = x,
                            RoomY = y,
                            Direction = MazeDirection.North,
                        };
                    }
                }
            }

            random.Shuffle(edges);

            int[] parent = new int[roomCount];
            int[] rank = new int[roomCount];
            for (int i = 0; i < roomCount; i++)
            {
                parent[i] = i;
            }

            int remaining = roomCount - 1;
            for (int i = 0; i < edges.Length && remaining > 0; i++)
            {
                Edge edge = edges[i];
                if (!Union(parent, rank, edge.RoomA, edge.RoomB))
                {
                    continue;
                }

                grid.CarveCorridor(edge.RoomX, edge.RoomY, edge.Direction);
                remaining--;
            }

            RandomizedPrimAlgorithm.EnsureEveryRoomCarved(grid);
        }

        private static bool Union(int[] parent, int[] rank, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);
            if (rootA == rootB)
            {
                return false;
            }

            if (rank[rootA] < rank[rootB])
            {
                int swap = rootA;
                rootA = rootB;
                rootB = swap;
            }

            parent[rootB] = rootA;
            if (rank[rootA] == rank[rootB])
            {
                rank[rootA]++;
            }

            return true;
        }

        private static int Find(int[] parent, int node)
        {
            while (parent[node] != node)
            {
                parent[node] = parent[parent[node]]; // path halving
                node = parent[node];
            }

            return node;
        }
    }
}
