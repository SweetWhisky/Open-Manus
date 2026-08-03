using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunRealms2
{
    public enum RealmId
    {
        NeonRainCity,
        DesertMegacity,
        FloodedKingdom,
        ZeroGStation,
        OvergrownLab,
        FrozenMegastructure,
        VolcanicFoundry,
        CelestialRuins
    }

    [Serializable]
    public struct RealmPalette
    {
        public RealmId Id;
        public string Name;
        public Color Sky;
        public Color Ground;
        public Color Primary;
        public Color Secondary;
        public Color Fog;

        public RealmPalette(RealmId id, string name, Color sky, Color ground, Color primary, Color secondary, Color fog)
        {
            Id = id;
            Name = name;
            Sky = sky;
            Ground = ground;
            Primary = primary;
            Secondary = secondary;
            Fog = fog;
        }
    }

    public static class RuntimeAssets
    {
        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly Dictionary<string, Texture2D> Textures = new();
        private static readonly Dictionary<PrimitiveType, Stack<GameObject>> Pool = new();

        public static readonly RealmPalette[] Palettes =
        {
            new(RealmId.NeonRainCity, "NEON RAIN CITY", Hex("061326"), Hex("08101D"), Hex("23D7FF"), Hex("FF3BD4"), Hex("071426")),
            new(RealmId.DesertMegacity, "DESERT MEGACITY", Hex("241106"), Hex("24180F"), Hex("FFB547"), Hex("FF694D"), Hex("3B1A09")),
            new(RealmId.FloodedKingdom, "FLOODED KINGDOM", Hex("071B27"), Hex("09222D"), Hex("4FE4FF"), Hex("52FFBF"), Hex("0B2D3A")),
            new(RealmId.ZeroGStation, "ZERO-G STATION", Hex("080815"), Hex("11101F"), Hex("8B5CFF"), Hex("23D7FF"), Hex("0A0920")),
            new(RealmId.OvergrownLab, "OVERGROWN LAB", Hex("07150F"), Hex("0B1A14"), Hex("67FF80"), Hex("FFB547"), Hex("0D2418")),
            new(RealmId.FrozenMegastructure, "FROZEN MEGASTRUCTURE", Hex("071521"), Hex("0B1D2A"), Hex("A9ECFF"), Hex("6BA7FF"), Hex("102A3A")),
            new(RealmId.VolcanicFoundry, "VOLCANIC FOUNDRY", Hex("240905"), Hex("210D09"), Hex("FF6B35"), Hex("FFD166"), Hex("35100B")),
            new(RealmId.CelestialRuins, "CELESTIAL RUINS", Hex("100B24"), Hex("15102A"), Hex("C59BFF"), Hex("5DE4FF"), Hex("1A1236"))
        };

        public static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString("#" + value, out var color) ? color : Color.magenta;
        }

        public static Material Material(string key, Color color, float metallic = 0.1f, float smoothness = 0.55f, bool emission = false)
        {
            var cacheKey = $"{key}_{ColorUtility.ToHtmlStringRGBA(color)}_{metallic:F2}_{smoothness:F2}_{emission}";
            if (Materials.TryGetValue(cacheKey, out var cached)) return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = cacheKey };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                var glow = color * 2.2f;
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", glow);
            }

            Materials[cacheKey] = material;
            return material;
        }

        public static Material WindowMaterial(RealmPalette palette, int seed)
        {
            var key = $"window_{palette.Id}_{seed % 5}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var texture = WindowTexture(palette, seed);
            var material = Material(key, Color.white, 0.15f, 0.45f, true);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            Materials[key] = material;
            return material;
        }

        public static Texture2D WindowTexture(RealmPalette palette, int seed)
        {
            var key = $"window_tex_{palette.Id}_{seed % 5}";
            if (Textures.TryGetValue(key, out var cached)) return cached;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = key,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            var random = new System.Random(seed);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var frame = x % 12 < 2 || y % 10 < 2;
                    var lit = random.NextDouble() > 0.38;
                    var baseColor = frame ? palette.Ground * 0.35f : (lit ? Color.Lerp(palette.Primary, palette.Secondary, (float)random.NextDouble()) * 1.4f : palette.Sky * 0.45f);
                    pixels[y * size + x] = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            Textures[key] = texture;
            return texture;
        }

        public static Texture2D NoiseTexture(string key, Color a, Color b, int seed)
        {
            if (Textures.TryGetValue(key, out var cached)) return cached;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = key,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            var random = new System.Random(seed);
            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                var n = (float)random.NextDouble();
                pixels[i] = Color.Lerp(a, b, Mathf.SmoothStep(0f, 1f, n));
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            Textures[key] = texture;
            return texture;
        }

        public static GameObject GetPrimitive(PrimitiveType type, Transform parent, string objectName)
        {
            if (!Pool.TryGetValue(type, out var stack))
            {
                stack = new Stack<GameObject>();
                Pool[type] = stack;
            }

            GameObject item;
            if (stack.Count > 0)
            {
                item = stack.Pop();
                item.SetActive(true);
            }
            else
            {
                item = GameObject.CreatePrimitive(type);
                item.AddComponent<PooledPrimitive>();
            }

            item.name = objectName;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = Vector3.one;
            return item;
        }

        public static void Release(GameObject item)
        {
            if (item == null) return;
            var pooled = item.GetComponent<PooledPrimitive>();
            if (pooled == null)
            {
                UnityEngine.Object.Destroy(item);
                return;
            }

            // Pickups and hazards own short-lived trigger/spinner components. Destroy them
            // rather than returning them to the primitive pool; otherwise Unity's deferred
            // component destruction can leave duplicate gameplay components on same-frame reuse.
            if (item.GetComponent<WorldInteractable>() != null || item.GetComponent<PickupSpinner>() != null)
            {
                UnityEngine.Object.Destroy(item);
                return;
            }

            var filter = item.GetComponent<MeshFilter>();
            var type = filter != null && filter.sharedMesh != null ? DetectPrimitive(filter.sharedMesh.name) : PrimitiveType.Cube;
            item.SetActive(false);
            item.transform.SetParent(null);
            if (!Pool.TryGetValue(type, out var stack))
            {
                stack = new Stack<GameObject>();
                Pool[type] = stack;
            }
            stack.Push(item);
        }

        private static PrimitiveType DetectPrimitive(string meshName)
        {
            if (meshName.Contains("Sphere", StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Sphere;
            if (meshName.Contains("Capsule", StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Capsule;
            if (meshName.Contains("Cylinder", StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Cylinder;
            if (meshName.Contains("Plane", StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Plane;
            if (meshName.Contains("Quad", StringComparison.OrdinalIgnoreCase)) return PrimitiveType.Quad;
            return PrimitiveType.Cube;
        }
    }

    public sealed class PooledPrimitive : MonoBehaviour { }
}
