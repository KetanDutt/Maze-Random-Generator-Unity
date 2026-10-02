using System;
using UnityEngine;

namespace Maze.Rendering
{
    /// <summary>Colour presets that are applied to the generated maze at runtime.</summary>
    public enum MazePalette
    {
        /// <summary>Blue-grey walls on a light floor (the look of the original demo).</summary>
        Slate = 0,

        /// <summary>Warm sand coloured walls.</summary>
        Sandstone = 1,

        /// <summary>Green walls, mossy floor.</summary>
        Forest = 2,

        /// <summary>Dark walls with a glowing path - easy to read in first person.</summary>
        Neon = 3,

        /// <summary>Keeps the texture of the assigned material (the third party Gridbox textures).</summary>
        Texture = 4,
    }

    /// <summary>The colours and material tweaks of a palette.</summary>
    public readonly struct MazePaletteDefinition
    {
        /// <summary>Creates a palette definition.</summary>
        public MazePaletteDefinition(Color wall, Color floor, Color path, Color accent, float wallSmoothness,
            float floorSmoothness, bool keepsTexture, bool emissivePath)
        {
            WallColor = wall;
            FloorColor = floor;
            PathColor = path;
            AccentColor = accent;
            WallSmoothness = wallSmoothness;
            FloorSmoothness = floorSmoothness;
            KeepsTexture = keepsTexture;
            EmissivePath = emissivePath;
        }

        /// <summary>Colour applied to the wall material.</summary>
        public Color WallColor { get; }

        /// <summary>Colour applied to the floor material.</summary>
        public Color FloorColor { get; }

        /// <summary>Colour of the solution path line.</summary>
        public Color PathColor { get; }

        /// <summary>Accent colour used by the minimap and the HUD.</summary>
        public Color AccentColor { get; }

        /// <summary>Smoothness of the wall material.</summary>
        public float WallSmoothness { get; }

        /// <summary>Smoothness of the floor material.</summary>
        public float FloorSmoothness { get; }

        /// <summary>When true the texture of the assigned material is kept instead of a flat colour.</summary>
        public bool KeepsTexture { get; }

        /// <summary>When true the path material emits light.</summary>
        public bool EmissivePath { get; }
    }

    /// <summary>Lookup table for the built in palettes.</summary>
    public static class MazePalettes
    {
        private static readonly string[] Names = { "Slate", "Sandstone", "Forest", "Neon", "Texture" };

        private static readonly MazePaletteDefinition[] Definitions =
        {
            new MazePaletteDefinition(
                wall: new Color(0.31f, 0.37f, 0.51f),
                floor: new Color(0.90f, 0.92f, 0.95f),
                path: new Color(1f, 0.24f, 0.51f),
                accent: new Color(0.09f, 0.76f, 0.84f),
                wallSmoothness: 0.12f,
                floorSmoothness: 0.05f,
                keepsTexture: false,
                emissivePath: false),
            new MazePaletteDefinition(
                wall: new Color(0.79f, 0.62f, 0.39f),
                floor: new Color(0.93f, 0.87f, 0.75f),
                path: new Color(0.85f, 0.20f, 0.12f),
                accent: new Color(0.36f, 0.24f, 0.12f),
                wallSmoothness: 0.08f,
                floorSmoothness: 0.03f,
                keepsTexture: false,
                emissivePath: false),
            new MazePaletteDefinition(
                wall: new Color(0.27f, 0.45f, 0.31f),
                floor: new Color(0.72f, 0.78f, 0.66f),
                path: new Color(0.98f, 0.72f, 0.16f),
                accent: new Color(0.55f, 0.83f, 0.44f),
                wallSmoothness: 0.06f,
                floorSmoothness: 0.04f,
                keepsTexture: false,
                emissivePath: false),
            new MazePaletteDefinition(
                wall: new Color(0.09f, 0.10f, 0.15f),
                floor: new Color(0.15f, 0.17f, 0.24f),
                path: new Color(0.29f, 0.98f, 0.86f),
                accent: new Color(0.90f, 0.29f, 0.98f),
                wallSmoothness: 0.35f,
                floorSmoothness: 0.25f,
                keepsTexture: false,
                emissivePath: true),
            new MazePaletteDefinition(
                wall: Color.white,
                floor: new Color(0.85f, 0.85f, 0.88f),
                path: new Color(1f, 0.35f, 0.35f),
                accent: new Color(0.20f, 0.60f, 0.90f),
                wallSmoothness: 0.10f,
                floorSmoothness: 0.05f,
                keepsTexture: true,
                emissivePath: false),
        };

        /// <summary>Number of palettes.</summary>
        public static int Count
        {
            get { return Definitions.Length; }
        }

        /// <summary>All palette names, in enum order.</summary>
        public static string[] GetNames()
        {
            return (string[])Names.Clone();
        }

        /// <summary>Returns the definition of a palette.</summary>
        public static MazePaletteDefinition Get(MazePalette palette)
        {
            int index = (int)palette;
            if (index < 0 || index >= Definitions.Length)
            {
                index = 0;
            }

            return Definitions[index];
        }

        /// <summary>Name of a palette.</summary>
        public static string GetName(MazePalette palette)
        {
            int index = (int)palette;
            return index >= 0 && index < Names.Length ? Names[index] : Names[0];
        }

        /// <summary>Parses a palette name (used by the JSON export).</summary>
        public static bool TryParse(string value, out MazePalette palette)
        {
            palette = MazePalette.Slate;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < Names.Length; i++)
            {
                if (string.Equals(Names[i], value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    palette = (MazePalette)i;
                    return true;
                }
            }

            return false;
        }
    }
}
