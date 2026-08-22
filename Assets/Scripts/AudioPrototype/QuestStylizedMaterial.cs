using UnityEngine;
using UnityEngine.Rendering;

namespace TsukiVox.AudioPrototype
{
    public enum QuestMaterialDetailMode
    {
        None = 0,
        Wood = 1,
        Fabric = 2,
        BrushedMetal = 3,
        Stone = 4,
        Perforated = 5,
        Plaster = 6,
    }

    public static class QuestStylizedMaterial
    {
        public const string ShaderName = "TsukiVox/Quest Stylized Lit";

        public static Material CreateLit(
            string materialName,
            Color color,
            float roughness,
            float metallic,
            float emissionIntensity = 0f,
            bool transparent = false,
            bool additive = false)
        {
            var shader = FindLitShader();
            var material = new Material(shader)
            {
                name = materialName,
            };

            SetBaseColor(material, color);
            ApplySurface(material, roughness, metallic);
            ApplyStylizedDefaults(material, color, roughness, metallic);

            if (emissionIntensity > 0f)
            {
                SetEmission(material, color, emissionIntensity);
            }

            if (transparent)
            {
                ConfigureTransparent(material, additive);
            }
            else
            {
                ConfigureOpaque(material);
            }

            return material;
        }

        public static void SetBaseColor(Material material, Color color)
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

        public static void SetAlpha(Material material, float alpha)
        {
            if (material == null)
            {
                return;
            }

            var color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.color;
            color.a = Mathf.Clamp01(alpha);
            SetBaseColor(material, color);
        }

        public static void SetEmission(Material material, Color color, float intensity)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
            }

            material.EnableKeyword("_EMISSION");
        }

        public static void ConfigureDetail(
            Material material,
            QuestMaterialDetailMode mode,
            float scale,
            float strength,
            float verticalGradient = 0f)
        {
            if (material == null)
            {
                return;
            }

            SetFloat(material, "_DetailMode", (float)mode);
            SetFloat(material, "_DetailScale", Mathf.Max(0.01f, scale));
            SetFloat(material, "_DetailStrength", Mathf.Clamp01(strength));
            SetFloat(material, "_VerticalGradient", Mathf.Clamp(verticalGradient, -1f, 1f));
        }

        public static void ConfigureLighting(
            Material material,
            float ambientFloor,
            float indirectStrength,
            float rimIntensity)
        {
            if (material == null)
            {
                return;
            }

            SetFloat(material, "_AmbientFloor", Mathf.Clamp(ambientFloor, 0f, 0.75f));
            SetFloat(material, "_IndirectStrength", Mathf.Clamp(indirectStrength, 0f, 2f));
            SetFloat(material, "_RimIntensity", Mathf.Clamp01(rimIntensity));
        }

        public static void ConfigureStableLighting(Material material)
        {
            if (material == null)
            {
                return;
            }

            SetFloat(material, "_StableLighting", 1f);
        }

        private static Shader FindLitShader()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return shader;
        }

        private static void ApplySurface(Material material, float roughness, float metallic)
        {
            var smoothness = Mathf.Clamp01(1f - roughness);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }
        }

        private static void ApplyStylizedDefaults(Material material, Color color, float roughness, float metallic)
        {
            var clampedMetallic = Mathf.Clamp01(metallic);
            var smoothness = Mathf.Clamp01(1f - roughness);
            SetFloat(material, "_ToonStep", Mathf.Lerp(0.44f, 0.36f, clampedMetallic));
            SetFloat(material, "_ToonFeather", Mathf.Lerp(0.16f, 0.075f, clampedMetallic));
            SetFloat(material, "_ToonStrength", Mathf.Lerp(0.62f, 0.78f, clampedMetallic));
            SetFloat(material, "_IndirectStrength", 0.92f);
            SetFloat(material, "_AmbientFloor", 0.38f);
            SetFloat(material, "_SpecularIntensity", Mathf.Lerp(0.12f, 0.92f, clampedMetallic) * Mathf.Lerp(0.68f, 1f, smoothness));
            SetFloat(material, "_ReflectionStrength", clampedMetallic * Mathf.Lerp(0.08f, 0.38f, smoothness));
            SetFloat(material, "_RimPower", Mathf.Lerp(5.5f, 3.8f, clampedMetallic));
            SetFloat(material, "_RimIntensity", Mathf.Lerp(0.018f, 0.085f, clampedMetallic));

            if (material.HasProperty("_ShadowTint"))
            {
                material.SetColor("_ShadowTint", new Color(0.38f, 0.43f, 0.52f, 1f));
            }

            if (material.HasProperty("_RimColor"))
            {
                material.SetColor("_RimColor", Color.Lerp(color, Color.white, 0.5f));
            }
        }

        private static void ConfigureOpaque(Material material)
        {
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("ShadowCaster", true);
        }

        private static void ConfigureTransparent(Material material, bool additive)
        {
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            SetFloat(material, "_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
        }

        private static void SetFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
