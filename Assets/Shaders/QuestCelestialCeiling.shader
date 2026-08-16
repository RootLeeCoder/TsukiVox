Shader "TsukiVox/Quest Celestial Ceiling"
{
    Properties
    {
        [MainColor] _BaseColor("Primary Color", Color) = (0.65, 0.85, 1, 1)
        [HDR] _SecondaryColor("Secondary Color", Color) = (1, 0.82, 0.62, 1)
        [Enum(Stars,0,Aurora,1)] _EffectMode("Effect", Float) = 0
        _Intensity("Intensity", Range(0, 2)) = 0.7
        _Scale("Scale", Range(1, 64)) = 24
        _Speed("Speed", Range(0, 4)) = 1
        _Density("Density", Range(0.01, 0.5)) = 0.12
        _Seed("Seed", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "CelestialCeiling"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CelestialVertex
            #pragma fragment CelestialFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SecondaryColor;
                half _EffectMode;
                half _Intensity;
                half _Scale;
                half _Speed;
                half _Density;
                half _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            half EdgeFade(float2 uv)
            {
                half2 edge = smoothstep(0.0h, 0.09h, uv) * smoothstep(0.0h, 0.09h, 1.0h - uv);
                return edge.x * edge.y;
            }

            half4 DrawStars(float2 uv)
            {
                float2 gridUv = uv * float2(_Scale, _Scale * 0.68h);
                float2 cell = floor(gridUv);
                float2 local = frac(gridUv) - 0.5h;
                half randomValue = Hash21(cell + _Seed);
                half randomValueB = Hash21(cell + _Seed + 19.17h);
                float2 jitter = float2(randomValueB - 0.5h, Hash21(cell + 7.31h) - 0.5h) * 0.42h;
                half distanceToStar = length(local - jitter);
                half presence = step(1.0h - _Density, randomValue);
                half core = 1.0h - smoothstep(0.025h, 0.105h, distanceToStar);
                half glow = (1.0h - smoothstep(0.08h, 0.27h, distanceToStar)) * 0.2h;
                half twinkleSpeed = lerp(0.65h, 2.4h, randomValueB);
                half twinkle = 0.34h + 0.66h * (sin(_Time.y * _Speed * twinkleSpeed + randomValue * 6.28318h) * 0.5h + 0.5h);
                half mask = presence * saturate(core + glow) * twinkle * EdgeFade(uv);
                half3 starColor = lerp(_BaseColor.rgb, _SecondaryColor.rgb, randomValueB * 0.72h);
                return half4(starColor, saturate(mask * _Intensity));
            }

            half4 DrawAurora(float2 uv)
            {
                half time = _Time.y * _Speed;
                half x = uv.x * _Scale;
                half broadWave = sin(x * 0.78h + time + sin(x * 0.24h - time * 0.63h)) * 0.105h;
                half fineWave = sin(x * 1.86h - time * 0.72h) * 0.035h;
                half center = 0.5h + broadWave + fineWave;
                half primaryBand = 1.0h - smoothstep(0.025h, 0.21h, abs(uv.y - center));

                half secondCenter = 0.34h - broadWave * 0.72h + sin(x * 1.12h + time * 0.54h) * 0.035h;
                half secondaryBand = 1.0h - smoothstep(0.02h, 0.16h, abs(uv.y - secondCenter));
                half curtain = 0.62h + 0.38h * (sin(uv.x * 43.0h - time * 2.1h + sin(uv.x * 8.0h)) * 0.5h + 0.5h);
                half shimmer = 0.88h + 0.12h * sin(time * 0.7h + uv.x * 6.0h);
                half mask = saturate(primaryBand * 0.76h + secondaryBand * 0.42h) * curtain * shimmer * EdgeFade(uv);
                half colorBlend = saturate(uv.y * 0.85h + broadWave * 1.8h + 0.12h);
                half3 auroraColor = lerp(_BaseColor.rgb, _SecondaryColor.rgb, colorBlend);
                return half4(auroraColor, saturate(mask * _Intensity));
            }

            Varyings CelestialVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 CelestialFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return _EffectMode < 0.5h ? DrawStars(input.uv) : DrawAurora(input.uv);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
