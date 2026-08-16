Shader "TsukiVox/Quest Stylized Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShadowTint("Shadow Tint", Color) = (0.42, 0.46, 0.54, 1)
        _ToonStep("Light Step", Range(0.05, 0.95)) = 0.42
        _ToonFeather("Light Feather", Range(0.01, 0.5)) = 0.14
        _ToonStrength("Toon Strength", Range(0, 1)) = 0.68
        _IndirectStrength("Indirect Light", Range(0, 2)) = 0.82
        _AmbientFloor("Ambient Floor", Range(0, 0.5)) = 0.32
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.35
        _SpecularIntensity("Specular", Range(0, 2)) = 0.25
        _ReflectionStrength("Reflection", Range(0, 1)) = 0.08
        [HDR] _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(1, 12)) = 5
        _RimIntensity("Rim Intensity", Range(0, 1)) = 0.03
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)

        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _AlphaClip("Alpha Clip", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1
        [HideInInspector] _DstBlend("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite("Z Write", Float) = 1
        [HideInInspector] _MainTex("Base Map", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowTint;
            half _ToonStep;
            half _ToonFeather;
            half _ToonStrength;
            half _IndirectStrength;
            half _AmbientFloor;
            half _Metallic;
            half _Smoothness;
            half _SpecularIntensity;
            half _ReflectionStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimIntensity;
            half4 _EmissionColor;
            half _Cutoff;
            half _Surface;
            half _AlphaClip;
            half _Cull;
            half _SrcBlend;
            half _DstBlend;
            half _ZWrite;
            float4 _MainTex_ST;
            half4 _Color;
            half _Glossiness;
        CBUFFER_END

        half4 SampleBase(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        void ApplyAlphaClip(half alpha)
        {
            if (_AlphaClip > 0.5h)
            {
                clip(alpha - _Cutoff);
            }
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StylizedVertex
            #pragma fragment StylizedFragment

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #if defined(UNITY_PLATFORM_META_QUEST)
                #pragma multi_compile _ META_QUEST_LIGHTUNROLL
                #pragma multi_compile _ META_QUEST_ORTHO_PROJ
            #endif

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half3 vertexLighting : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings StylizedVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.vertexLighting = VertexLighting(positionInputs.positionWS, output.normalWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            void AddStylizedLight(
                Light light,
                half3 normalWS,
                half3 viewDirectionWS,
                half3 baseColor,
                inout float3 diffuse,
                inout float3 specular)
            {
                float attenuation = saturate(light.distanceAttenuation * light.shadowAttenuation);
                float lightEnergy = max(light.color.r, max(light.color.g, light.color.b));
                if (attenuation <= HALF_MIN || lightEnergy <= HALF_MIN)
                {
                    return;
                }

                float normalLight = saturate(dot(normalWS, light.direction));
                float feather = max(_ToonFeather, 0.01h);
                float band = smoothstep(
                    _ToonStep - feather,
                    _ToonStep + feather,
                    normalLight);
                float lightAmount = lerp(normalLight, band, saturate(_ToonStrength));
                diffuse += light.color * lightAmount * attenuation;

                half3 reflectedLight = reflect(-light.direction, normalWS);
                float reflectionDot = saturate(dot(reflectedLight, viewDirectionWS));
                float highlightStart = lerp(0.72h, 0.94h, saturate(_Smoothness));
                float highlight = smoothstep(highlightStart, min(0.995h, highlightStart + 0.045h), reflectionDot);
                half3 specularColor = lerp(0.04h.xxx, baseColor, saturate(_Metallic));
                specular += specularColor * light.color * highlight * attenuation * max(_SpecularIntensity, 0.0h);
            }

            half4 StylizedFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SampleBase(input.uv);
                ApplyAlphaClip(baseSample.a);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 directDiffuse = 0;
                float3 directSpecular = 0;
                half4 shadowMask = half4(1, 1, 1, 1);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(additionalLightCount)
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        AddStylizedLight(
                            additionalLight,
                            normalWS,
                            viewDirectionWS,
                            baseSample.rgb,
                            directDiffuse,
                            directSpecular);
                    LIGHT_LOOP_END
                #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
                    directDiffuse += input.vertexLighting;
                #endif

                half upFacing = saturate(normalWS.y * 0.5h + 0.5h);
                half3 indirectTint = lerp(_ShadowTint.rgb, 1.0h.xxx, upFacing);
                half3 indirect = max(
                    SampleSH(normalWS) * indirectTint * max(_IndirectStrength, 0.0h),
                    _AmbientFloor.xxx);
                half rim = pow(saturate(1.0h - dot(normalWS, viewDirectionWS)), max(_RimPower, 1.0h));
                float3 color = baseSample.rgb * (indirect + directDiffuse);
                color += directSpecular;
                if (_ReflectionStrength > 0.001h)
                {
                    half3 reflectionVector = reflect(-viewDirectionWS, normalWS);
                    half reflectionFacing = saturate(reflectionVector.y * 0.5h + 0.5h);
                    half3 reflectionTint = lerp(_ShadowTint.rgb, 1.0h.xxx, reflectionFacing);
                    half3 reflectionColor = lerp(0.04h.xxx, baseSample.rgb, saturate(_Metallic));
                    color += reflectionTint * reflectionColor * saturate(_ReflectionStrength) * _AmbientFloor;
                }

                color += _RimColor.rgb * rim * max(_RimIntensity, 0.0h);
                color += max(_EmissionColor.rgb, 0.0h.xxx);
                color = MixFog(color, input.fogFactor);
                return half4(color, baseSample.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    half3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    half3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyAlphaClip(SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyAlphaClip(SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
