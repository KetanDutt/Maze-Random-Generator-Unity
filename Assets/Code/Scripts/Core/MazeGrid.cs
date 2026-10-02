using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Maze.Core
{
    /// <summary>An opening (entrance or exit) cut through the outer wall of the maze.</summary>
    public readonly struct MazeOpening
    {
        /// <summary>Creates an opening description.</summary>
        public MazeOpening(MazeDirection side, Vector2Int roomCell, Vector2Int borderCell)
        {
            Side = side;
            RoomCell = roomCell;
            BorderCell = borderCell;
        }

        /// <summary>Side of the maze the opening belongs to.</summary>
        public MazeDirection Side { get; }

        /// <summary>The room (grid cell on the room lattice) the opening connects to.</summary>
        public Vector2Int RoomCell { get; }

        /// <summary>The outermost carved cell, always located on the grid border.</summary>
        public Vector2Int BorderCell { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return "Opening(" + Side.ToDisplayName() + ", border " + BorderCell + ")";
        }
    }

    /// <summary>
    /// A rectangular grid whose cells are either a wall or a passage. This is the model every
    /// algorithm works on; it is deliberately free of any Unity scene objects so that it can be
    /// unit tested and serialised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grid is always odd sized and surrounded by <see cref="Padding"/> rings of cells that
    /// stay walls (openings are the only exception). Rooms live on a lattice with a spacing of
    /// two cells; the cell in between two rooms becomes the corridor that connects them. Working
    /// on that lattice is what guarantees that no row or column of the maze is stranded - the
    /// limitation of the original implementation.
    /// </para>
    /// </remarks>
    public sealed class MazeGrid
    {
        /// <summary>Smallest supported interior size (in cells). Must be odd.</summary>
        public const int MinimumInteriorSize = 3;

        /// <summary>Largest supported dimension (in cells).</summary>
        public const int MaximumDimension = 501;

        private readonly bool[] _cells;
        private readonly List<MazeOpening> _openings = new List<MazeOpening>(4);

        /// <summary>Creates a solid grid of <paramref name="width"/> x <paramref name="height"/> wall cells.</summary>
        /// <exception cref="ArgumentOutOfRangeException">When the size is invalid.</exception>
        public MazeGrid(int width, int height, int padding = 1)
        {
            if (padding < 1)
            {
                throw new ArgumentOutOfRangeException("padding", "Padding must be at least one cell.");
            }

            if (width < 2 * padding + MinimumInteriorSize || height < 2 * padding + MinimumInteriorSize)
            {
                throw new ArgumentOutOfRangeException("width",
                    "The maze needs an interior of at least " + MinimumInteriorSize + " cells on both axes.");
            }

            if ((width - 2 * padding) % 2 == 0 || (height - 2 * padding) % 2 == 0)
            {
                throw new ArgumentOutOfRangeException("width",
                    "Interior dimensions must be odd so that the room lattice fits.");
            }

            Width = width;
            Height = height;
            Padding = padding;
            _cells = new bool[width * height];

            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = true; // everything starts as a wall
            }
        }

        /// <summary>Number of cells on the x axis.</summary>
        public int Width { get; }

        /// <summary>Number of cells on the y axis.</summary>
        public int Height { get; }

        /// <summary>Thickness of the forced-wall ring around the maze, in cells.</summary>
        public int Padding { get; }

        /// <summary>The openings that have been cut into the outer wall.</summary>
        public IReadOnlyList<MazeOpening> Openings
        {
            get { return _openings; }
        }

        // ---------------------------------------------------------------------
        // Geometry
        // ---------------------------------------------------------------------

        /// <summary>Number of rooms along the x axis (the room lattice).</summary>
        public int RoomCountX
        {
            get { return (Width - 2 * Padding + 1) / 2; }
        }

        /// <summary>Number of rooms along the y axis (the room lattice).</summary>
        public int RoomCountY
        {
            get { return (Height - 2 * Padding + 1) / 2; }
        }

        /// <summary>Total number of rooms; every one of them becomes a passage.</summary>
        public int RoomCount
        {
            get { return RoomCountX * RoomCountY; }
        }

        /// <summary>Number of cells that are currently walls.</summary>
        public int WallCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Number of cells that are currently passages.</summary>
        public int PassageCount
        {
            get { return _cells.Length - WallCount; }
        }

        /// <summary>Converts a room coordinate on the lattice into a grid cell.</summary>
        public Vector2Int CellOfRoom(int roomX, int roomY)
        {
            return new Vector2Int(Padding + roomX * 2, Padding + roomY * 2);
        }

        /// <summary>Converts a grid cell into the room lattice coordinate it belongs to.</summary>
        public Vector2Int RoomOfCell(int x, int y)
        {
            return new Vector2Int((x - Padding) / 2, (y - Padding) / 2);
        }

        /// <summary><c>true</c> when the cell is inside the grid.</summary>
        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        /// <summary><c>true</c> when the cell is part of the room lattice.</summary>
        public bool IsRoomCell(int x, int y)
        {
            return InBounds(x, y) && (x - Padding) % 2 == 0 && (y - Padding) % 2 == 0;
        }

        /// <summary><c>true</c> when the room coordinate is inside the room lattice.</summary>
        public bool InRoomBounds(int roomX, int roomY)
        {
            return roomX >= 0 && roomY >= 0 && roomX < RoomCountX && roomY < RoomCountY;
        }

        /// <summary><c>true</c> when the cell is a wall. Cells outside the grid count as walls.</summary>
        public bool IsWall(int x, int y)
        {
            return !InBounds(x, y) || _cells[y * Width + x];
        }

        /// <summary><c>true</c> when the cell is a passage. Cells outside the grid are never passages.</summary>
        public bool IsPassage(int x, int y)
        {
            return InBounds(x, y) && !_cells[y * Width + x];
        }

        /// <summary><c>true</c> when the room has been carved into a passage.</summary>
        public bool IsRoomCarved(int roomX, int roomY)
        {
            if (!InRoomBounds(roomX, roomY))
            {
                return false;
            }

            Vector2Int cell = CellOfRoom(roomX, roomY);
            return IsPassage(cell.x, cell.y);
        }

        // ---------------------------------------------------------------------
        // Mutation
        // ---------------------------------------------------------------------

        /// <summary>Forces the cell to be a wall.</summary>
        public void SetWall(int x, int y)
        {
            if (InBounds(x, y))
            {
                _cells[y * Width + x] = true;
            }
        }

        /// <summary>Carves the cell into a passage.</summary>
        public void Carve(int x, int y)
        {
            if (InBounds(x, y))
            {
                _cells[y * Width + x] = false;
            }
        }

        /// <summary>Carves the cell of a room into a passage.</summary>
        public void CarveRoom(int roomX, int roomY)
        {
            if (InRoomBounds(roomX, roomY))
            {
                Vector2Int cell = CellOfRoom(roomX, roomY);
                Carve(cell.x, cell.y);
            }
        }

        /// <summary>
        /// Connects the room <c>(roomX, roomY)</c> with its neighbour towards
        /// <paramref name="direction"/>. Both rooms and the corridor cell in between are carved,
        /// so callers can never forget to carve the destination room.
        /// </summary>
        public void CarveCorridor(int roomX, int roomY, MazeDirection direction)
        {
            Vector2Int offset = direction.ToOffset();
            int neighbourX = roomX + offset.x;
            int neighbourY = roomY + offset.y;
            if (!InRoomBounds(roomX, roomY) || !InRoomBounds(neighbourX, neighbourY))
            {
                return;
            }

            Vector2Int cell = CellOfRoom(roomX, roomY);
            Carve(cell.x, cell.y);
            Carve(cell.x + offset.x, cell.y + offset.y);
            Vector2Int neighbourCell = CellOfRoom(neighbourX, neighbourY);
            Carve(neighbourCell.x, neighbourCell.y);
        }

        /// <summary><c>true</c> when the room is already connected towards <paramref name="direction"/>.</summary>
        public bool IsCorridorCarved(int roomX, int roomY, MazeDirection direction)
        {
            Vector2Int offset = direction.ToOffset();
            if (!InRoomBounds(roomX + offset.x, roomY + offset.y))
            {
                return false;
            }

            Vector2Int cell = CellOfRoom(roomX, roomY);
            return IsPassage(cell.x + offset.x, cell.y + offset.y);
        }

        /// <summary>Number of corridors that leave the room (0 - 4).</summary>
        public int RoomDegree(int roomX, int roomY)
        {
            int degree = 0;
            for (int i = 0; i < MazeDirections.Count; i++)
            {
                if (IsCorridorCarved(roomX, roomY, MazeDirections.Values[i]))
                {
                    degree++;
                }
            }

            return degree;
        }

        /// <summary>Fills <paramref name="buffer"/> with the in-bounds room neighbours of a room.</summary>
        public int CollectRoomNeighbours(int roomX, int roomY, Vector2Int[] buffer)
        {
            int count = 0;
            for (int i = 0; i < MazeDirections.Count; i++)
            {
                Vector2Int offset = MazeDirections.Values[i].ToOffset();
                int x = roomX + offset.x;
                int y = roomY + offset.y;
                if (InRoomBounds(x, y))
                {
                    buffer[count++] = new Vector2Int(x, y);
                }
            }

            return count;
        }

        /// <summary>Registers an opening; used by the generator after the cells were carved.</summary>
        public void RegisterOpening(MazeOpening opening)
        {
            _openings.Add(opening);
        }

        /// <summary>Removes every registered opening (the carved cells stay carved).</summary>
        public void ClearOpenings()
        {
            _openings.Clear();
        }

        /// <summary><c>true</c> when the room hosts an opening.</summary>
        public bool IsOpeningRoom(int roomX, int roomY)
        {
            Vector2Int cell = CellOfRoom(roomX, roomY);
            for (int i = 0; i < _openings.Count; i++)
            {
                if (_openings[i].RoomCell == cell)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------------
        // Iteration / diagnostics
        // ---------------------------------------------------------------------

        /// <summary>Enumerates every cell that is a passage.</summary>
        public IEnumerable<Vector2Int> EnumeratePassages()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!_cells[y * Width + x])
                    {
                        yield return new Vector2Int(x, y);
                    }
                }
            }
        }

        /// <summary>Enumerates every room coordinate on the lattice.</summary>
        public IEnumerable<Vector2Int> EnumerateRooms()
        {
            for (int y = 0; y < RoomCountY; y++)
            {
                for (int x = 0; x < RoomCountX; x++)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }

        /// <summary>Returns a deep copy of the grid (cells and openings).</summary>
        public MazeGrid Clone()
        {
            MazeGrid clone = new MazeGrid(Width, Height, Padding);
            Array.Copy(_cells, clone._cells, _cells.Length);
            clone._openings.AddRange(_openings);
            return clone;
        }

        /// <summary>
        /// 64 bit FNV-1a fingerprint of the grid. Two grids with the same fingerprint contain the
        /// same map; used for determinism tests and for logging.
        /// </summary>
        public ulong ComputeFingerprint()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < _cells.Length; i++)
            {
                hash ^= _cells[i] ? 1UL : 0UL;
                hash *= prime;
            }

            return hash;
        }

        /// <summary>Renders the maze as ASCII art (top row is the highest y).</summary>
        public string ToAscii(char wall = '#', char passage = '.')
        {
            StringBuilder builder = new StringBuilder((Width + 1) * Height);
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    builder.Append(IsWall(x, y) ? wall : passage);
                }

                if (y > 0)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        /// <summary>Clamps a requested dimension so the interior is odd and within the limits.</summary>
        public static int NormalizeDimension(int requested, int padding)
        {
            int minimum = 2 * padding + MinimumInteriorSize;
            int value = requested < minimum ? minimum : requested;
            if (value > MaximumDimension)
            {
                value = MaximumDimension;
            }

            if ((value - 2 * padding) % 2 == 0)
            {
                value++;
            }

            return value;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "MazeGrid(" + Width + "x" + Height + ", rooms " + RoomCountX + "x" + RoomCountY +
                   ", passages " + PassageCount + ", openings " + _openings.Count + ")";
        }
    }
}
