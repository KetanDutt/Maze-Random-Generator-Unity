using System.Collections.Generic;
using Maze.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maze.Rendering
{
    /// <summary>The meshes produced for one maze.</summary>
    public struct MazeMeshData
    {
        /// <summary>Combined mesh with submesh 0 = walls and submesh 1 = floor (when present).</summary>
        public Mesh Mesh;

        /// <summary>Number of wall quads (each quad is two triangles).</summary>
        public int WallQuadCount;

        /// <summary>Number of floor quads.</summary>
        public int FloorQuadCount;

        /// <summary>Number of vertices in <see cref="Mesh"/>.</summary>
        public int VertexCount;

        /// <summary>Number of triangles in <see cref="Mesh"/>.</summary>
        public int TriangleCount;

        /// <summary>Bounds of the generated geometry.</summary>
        public Bounds Bounds;

        /// <summary><c>true</c> when the mesh has a floor submesh.</summary>
        public bool HasFloor
        {
            get { return FloorQuadCount > 0; }
        }

        /// <summary><c>true</c> when a mesh was built.</summary>
        public bool IsValid
        {
            get { return Mesh != null; }
        }
    }

    /// <summary>
    /// Turns a <see cref="MazeGrid"/> into a single mesh. One draw call instead of one game object
    /// per wall - the reason the generator stays fast with mazes of a few hundred cells per side.
    /// </summary>
    /// <remarks>
    /// Faces are culled: a wall face that touches another wall cell is never emitted, and the top
    /// face of a wall is only emitted once. A cell in the middle of a wall block therefore costs two
    /// triangles instead of twelve. The winding of every quad follows <c>Mesh.triangles</c>
    /// (counter clockwise when seen from the front) so the mesh renders correctly with back face
    /// culling both in the built-in pipeline and in the SRPs.
    /// </remarks>
    public static class MazeMeshBuilder
    {
        /// <summary>Vertex count above which a 32 bit index buffer is required.</summary>
        public const int UInt16VertexLimit = 65000;

        /// <summary>Builds the wall (and optionally floor) mesh of a maze.</summary>
        /// <param name="grid">Carved maze grid.</param>
        /// <param name="cellSize">Size of a cell in world units.</param>
        /// <param name="wallHeight">Height of the walls in world units.</param>
        /// <param name="includeFloor">Adds a floor submesh covering every passage cell.</param>
        /// <param name="origin">World position of the centre of cell (0, 0).</param>
        public static MazeMeshData Build(MazeGrid grid, float cellSize, float wallHeight, bool includeFloor,
            Vector3 origin)
        {
            MazeMeshData data = new MazeMeshData();
            if (grid == null)
            {
                return data;
            }

            int cellCount = grid.Width * grid.Height;
            List<Vector3> vertices = new List<Vector3>(cellCount * 4);
            List<int> wallTriangles = new List<int>(cellCount * 6);
            List<int> floorTriangles = new List<int>(cellCount * 6);

            float half = cellSize * 0.5f;
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    float centerX = origin.x + x * cellSize;
                    float centerZ = origin.z + y * cellSize;
                    float left = centerX - half;
                    float right = centerX + half;
                    float back = centerZ - half;
                    float front = centerZ + half;

                    if (grid.IsWall(x, y))
                    {
                        // Top face - always visible from above.
                        AddQuad(vertices, wallTriangles,
                            new Vector3(left, wallHeight, back),
                            new Vector3(left, wallHeight, front),
                            new Vector3(right, wallHeight, front),
                            new Vector3(right, wallHeight, back));

                        // Side faces, only where the neighbour is a passage or outside the grid.
                        if (IsVisibleNeighbour(grid, x + 1, y))
                        {
                            AddQuad(vertices, wallTriangles,
                                new Vector3(right, 0f, front),
                                new Vector3(right, 0f, back),
                                new Vector3(right, wallHeight, back),
                                new Vector3(right, wallHeight, front));
                        }

                        if (IsVisibleNeighbour(grid, x - 1, y))
                        {
                            AddQuad(vertices, wallTriangles,
                                new Vector3(left, 0f, back),
                                new Vector3(left, 0f, front),
                                new Vector3(left, wallHeight, front),
                                new Vector3(left, wallHeight, back));
                        }

                        if (IsVisibleNeighbour(grid, x, y + 1))
                        {
                            AddQuad(vertices, wallTriangles,
                                new Vector3(left, 0f, front),
                                new Vector3(right, 0f, front),
                                new Vector3(right, wallHeight, front),
                                new Vector3(left, wallHeight, front));
                        }

                        if (IsVisibleNeighbour(grid, x, y - 1))
                        {
                            AddQuad(vertices, wallTriangles,
                                new Vector3(right, 0f, back),
                                new Vector3(left, 0f, back),
                                new Vector3(left, wallHeight, back),
                                new Vector3(right, wallHeight, back));
                        }

                        data.WallQuadCount++;
                        TrackBounds(ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ,
                            new Vector3(left, 0f, back));
                        TrackBounds(ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ,
                            new Vector3(right, wallHeight, front));
                    }
                    else if (includeFloor)
                    {
                        AddQuad(vertices, floorTriangles,
                            new Vector3(left, 0f, back),
                            new Vector3(left, 0f, front),
                            new Vector3(right, 0f, front),
                            new Vector3(right, 0f, back));
                        data.FloorQuadCount++;
                        TrackBounds(ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ,
                            new Vector3(left, 0f, back));
                        TrackBounds(ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ,
                            new Vector3(right, 0f, front));
                    }
                }
            }

            if (vertices.Count == 0)
            {
                return data;
            }

            Mesh mesh = new Mesh();
            mesh.name = "Maze " + grid.Width + "x" + grid.Height;
            if (vertices.Count > UInt16VertexLimit)
            {
                // Required for mazes above roughly 120x120 cells.
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            if (data.FloorQuadCount > 0)
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(wallTriangles, 0);
                mesh.SetTriangles(floorTriangles, 1);
            }
            else
            {
                mesh.subMeshCount = 1;
                mesh.SetTriangles(wallTriangles, 0);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            data.Mesh = mesh;
            data.VertexCount = vertices.Count;
            data.TriangleCount = (wallTriangles.Count + floorTriangles.Count) / 3;
            data.Bounds = new Bounds(
                new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
            return data;
        }

        /// <summary>Estimated triangle count for a grid, useful for UI warnings.</summary>
        public static int EstimateTriangleCount(MazeGrid grid, bool includeFloor)
        {
            int quads = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsWall(x, y))
                    {
                        quads++;
                        for (int i = 0; i < MazeDirections.Count; i++)
                        {
                            Vector2Int offset = MazeDirections.Values[i].ToOffset();
                            if (IsVisibleNeighbour(grid, x + offset.x, y + offset.y))
                            {
                                quads++;
                            }
                        }
                    }
                    else if (includeFloor)
                    {
                        quads++;
                    }
                }
            }

            return quads * 2;
        }

        /// <summary>
        /// <c>true</c> when the face towards that neighbour is visible: the neighbour is a passage
        /// or outside the grid, so the outer wall of the maze is closed and visible from below.
        /// </summary>
        private static bool IsVisibleNeighbour(MazeGrid grid, int x, int y)
        {
            return !grid.InBounds(x, y) || !grid.IsWall(x, y);
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 p0, Vector3 p1,
            Vector3 p2, Vector3 p3)
        {
            int index = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        private static void TrackBounds(ref float minX, ref float minY, ref float minZ, ref float maxX,
            ref float maxY, ref float maxZ, Vector3 point)
        {
            if (point.x < minX) minX = point.x;
            if (point.y < minY) minY = point.y;
            if (point.z < minZ) minZ = point.z;
            if (point.x > maxX) maxX = point.x;
            if (point.y > maxY) maxY = point.y;
            if (point.z > maxZ) maxZ = point.z;
        }
    }
}
