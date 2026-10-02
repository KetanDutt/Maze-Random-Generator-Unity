using UnityEngine;

namespace Maze.Rendering
{
    /// <summary>
    /// Creates the materials the generated maze needs at runtime, without hard coding a render
    /// pipeline. The built-in pipeline, URP and HDRP are all detected through
    /// <see cref="Shader.Find"/> and their property names are resolved at runtime, so the same
    /// component works in every template.
    /// </summary>
    public static class MazeMaterialFactory
    {
        /// <summary>Shader names, in the order they are tried.</summary>
        private static readonly string[] ShaderCandidates =
        {
            "Universal Render Pipeline/Lit",
            "HDRP/Lit",
            "Standard",
            "Legacy Shaders/Diffuse",
            "Unlit/Color",
            "Sprites/Default",
        };

        private static Shader _cachedShader;

        /// <summary>Returns the best available lit shader for the active render pipeline.</summary>
        public static Shader FindBestShader()
        {
            if (_cachedShader != null)
            {
                return _cachedShader;
            }

            for (int i = 0; i < ShaderCandidates.Length; i++)
            {
                Shader shader = Shader.Find(ShaderCandidates[i]);
                if (shader != null)
                {
                    _cachedShader = shader;
                    return shader;
                }
            }

            return null;
        }

        /// <summary>Creates a lit material with a colour, an optional texture and smoothness.</summary>
        public static Material Create(string name, Color color, Texture texture, float smoothness,
            float metallic = 0f)
        {
            Shader shader = FindBestShader();
            if (shader == null)
            {
                Debug.LogWarning("[Maze] No usable shader was found. Assign a material manually.");
                return null;
            }

            Material material = new Material(shader);
            material.name = name;
            SetColor(material, color);
            SetTexture(material, texture);
            SetFloat(material, "_Smoothness", "_Glossiness", smoothness);
            SetFloat(material, "_Metallic", null, metallic);
            return material;
        }

        /// <summary>
        /// Creates the material used for the solution path (a line renderer). Emissive colours keep
        /// the path readable in dark palettes.
        /// </summary>
        public static Material CreatePathMaterial(string name, Color color, bool emissive)
        {
            Material material = Create(name, color, null, 0f);
            if (material == null)
            {
                return null;
            }

            if (emissive)
            {
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 1.6f);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }

            return material;
        }

        /// <summary>
        /// Creates an unlit material, optionally textured. Used by the HUD and the minimap where the
        /// appearance must not depend on scene lighting; falls back to a lit material when no unlit
        /// shader is available.
        /// </summary>
        public static Material CreateUnlit(string name, Color color, Texture texture, bool transparent = false)
        {
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material;
            if (shader != null)
            {
                material = new Material(shader);
                material.name = name;
                SetColor(material, color);
                SetTexture(material, texture);
            }
            else
            {
                material = Create(name, color, texture, 0f);
                if (material == null)
                {
                    return null;
                }
            }

            if (transparent && material.HasProperty("_Color"))
            {
                Color tint = material.GetColor("_Color");
                tint.a = Mathf.Min(1f, tint.a);
                material.SetColor("_Color", tint);
            }

            return material;
        }

        /// <summary>Sets the base colour of a material, whatever pipeline it belongs to.</summary>
        public static void SetColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        /// <summary>Sets the main texture of a material, whatever pipeline it belongs to.</summary>
        public static void SetTexture(Material material, Texture texture)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        /// <summary>Creates a one pixel texture of a solid colour (used by the HUD and the minimap).</summary>
        public static Texture2D CreateSolidTexture(Color color, string name = "Maze Solid")
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private static void SetFloat(Material material, string primary, string fallback, float value)
        {
            if (material == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(primary) && material.HasProperty(primary))
            {
                material.SetFloat(primary, value);
            }
            else if (!string.IsNullOrEmpty(fallback) && material.HasProperty(fallback))
            {
                material.SetFloat(fallback, value);
            }
        }
    }
}
