using System.Collections.Generic;
using Maze.Analysis;
using Maze.Core;
using Maze.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Maze.Tests
{
    /// <summary>Tests for the graph analysis used by the UI, the HUD and the objective tracking.</summary>
    public sealed class MazePathfinderTests
    {
        [Test]
        public void PathIsValidAndOptimal()
        {
            MazeGrid grid = GenerateWithOpenings(31, 4242);
            Vector2Int start = grid.Openings[0].BorderCell;
            Vector2Int goal = grid.Openings[grid.Openings.Count - 1].BorderCell;

            List<Vector2Int> path = MazePathfinder.FindPath(grid, start, goal);
            Assert.IsNotNull(path);
            Assert.AreEqual(start, path[0]);
            Assert.AreEqual(goal, path[path.Count - 1]);

            for (int i = 0; i < path.Count; i++)
            {
                Assert.IsTrue(grid.IsPassage(path[i].x, path[i].y), "path leaves the passages at " + i);
                if (i > 0)
                {
                    int steps = Mathf.Abs(path[i].x - path[i - 1].x) + Mathf.Abs(path[i].y - path[i - 1].y);
                    Assert.AreEqual(1, steps, "path is not continuous at " + i);
                }
            }

            // A breadth first search must not be longer than any route through the maze centre.
            List<Vector2Int> detour = MazePathfinder.FindPath(grid, start, path[path.Count / 2]);
            Assert.IsNotNull(detour);
            Assert.LessOrEqual(detour.Count, path.Count);
        }

        [Test]
        public void PathReturnsNullForWallsOrDisconnectedCells()
        {
            MazeGrid grid = new MazeGrid(21, 21);
            grid.CarveRoom(0, 0);
            grid.CarveRoom(5, 5);

            Assert.IsNull(MazePathfinder.FindPath(grid, new Vector2Int(1, 1), new Vector2Int(11, 11)),
                "isolated rooms must not be connected");
            Assert.IsNull(MazePathfinder.FindPath(grid, new Vector2Int(0, 0), new Vector2Int(5, 5)),
                "wall cells are not part of any path");
            Assert.IsNotNull(MazePathfinder.FindPath(grid, new Vector2Int(1, 1), new Vector2Int(1, 1)),
                "a path from a cell to itself exists");
        }

        [Test]
        public void DepthIsMeasuredFromTheEntrance()
        {
            MazeGrid grid = GenerateWithOpenings(31, 7);
            Vector2Int farthest;
            int depth = MazePathfinder.MeasureDepth(grid, grid.Openings[0].BorderCell, out farthest);

            Assert.Greater(depth, 0);
            Assert.IsTrue(grid.IsPassage(farthest.x, farthest.y));

            List<Vector2Int> path = MazePathfinder.FindPath(grid, grid.Openings[0].BorderCell, farthest);
            Assert.IsNotNull(path);
            Assert.AreEqual(depth + 1, path.Count, "depth must match the shortest path length");
        }

        [Test]
        public void ReachabilityMatchesThePassageCount()
        {
            MazeGrid grid = GenerateWithOpenings(21, 11);
            int reachable = MazePathfinder.CountReachable(grid, grid.Openings[0].BorderCell);

            Assert.AreEqual(grid.PassageCount, reachable);
            Assert.IsTrue(MazePathfinder.IsFullyConnected(grid));
        }

        [Test]
        public void PartiallyCarvedGridIsDetectedAsDisconnected()
        {
            MazeGrid grid = new MazeGrid(21, 21);
            grid.CarveRoom(0, 0);
            grid.CarveRoom(4, 4);

            Assert.IsFalse(MazePathfinder.IsFullyConnected(grid));
            Assert.AreEqual(1, MazePathfinder.CountReachable(grid, new Vector2Int(1, 1)));
        }

        [Test]
        public void DeadEndsMatchAHandCountedExample()
        {
            // A single corridor: two dead ends and nothing in between.
            MazeGrid grid = new MazeGrid(7, 7);
            for (int x = 0; x < 3; x++)
            {
                grid.CarveCorridor(x, 0, MazeDirection.East);
            }

            Assert.AreEqual(2, MazePathfinder.CountDeadEnds(grid));
            Assert.AreEqual(0, MazePathfinder.CountJunctions(grid));
        }

        [Test]
        public void JunctionsAreCountedCorrectly()
        {
            MazeGrid grid = new MazeGrid(9, 9);
            grid.CarveCorridor(0, 0, MazeDirection.East);
            grid.CarveCorridor(1, 0, MazeDirection.East);
            grid.CarveCorridor(1, 0, MazeDirection.North);
            grid.CarveCorridor(1, 0, MazeDirection.South);

            Assert.AreEqual(1, MazePathfinder.CountJunctions(grid), "room (1,0) has three corridors");
            Assert.AreEqual(3, MazePathfinder.CountDeadEnds(grid), "the three leaves are dead ends");
        }

        [Test]
        public void FindFirstPassageSkipsWalls()
        {
            MazeGrid grid = new MazeGrid(7, 7);
            Assert.AreEqual(new Vector2Int(-1, -1), MazePathfinder.FindFirstPassage(grid));

            grid.Carve(5, 4);
            Assert.AreEqual(new Vector2Int(5, 4), MazePathfinder.FindFirstPassage(grid));
        }

        private static MazeGrid GenerateWithOpenings(int size, int seed)
        {
            MazeGeneratorCore core = new MazeGeneratorCore();
            MazeGeneratorSettings settings = new MazeGeneratorSettings
            {
                MazeWidth = size,
                MazeHeight = size,
                OpeningMode = MazeOpeningMode.FixedEntranceAndExit,
            };

            return core.Generate(settings, seed).Grid;
        }
    }
}
