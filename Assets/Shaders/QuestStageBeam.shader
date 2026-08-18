Shader "TsukiVox/Quest Stage Beam"
{
    Properties
    {
        [MainColor] _BaseColor("Beam Color", Color) = (0.3, 0.85, 1, 0.32)
        _Intensity("Intensity", Range(0, 2)) = 0.8
        [Enum(Beam,0,Pool,1)] _EffectMode("Effect", Float) = 0
        _NoiseSpeed("Noise Speed", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
        }

        Pass
        {
            Name "StageBeam"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BeamVertex
            #pragma fragment BeamFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _EffectMode;
                half _NoiseSpeed;
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

            Varyings BeamVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 BeamFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half alpha;
                if (_EffectMode < 0.5h)
                {
                    half center = pow(saturate(1.0h - abs(input.uv.x * 2.0h - 1.0h)), 1.7h);
                    half nearFade = smoothstep(0.0h, 0.08h, input.uv.y);
                    half farFade = 1.0h - smoothstep(0.62h, 1.0h, input.uv.y);
                    half shimmer = 0.86h + 0.14h * sin(input.uv.y * 21.0h - _Time.y * _NoiseSpeed * 3.2h);
                    alpha = center * nearFade * farFade * shimmer * _BaseColor.a;
                }
                else
                {
                    half radius = length(input.uv * 2.0h - 1.0h);
                    half core = 1.0h - smoothstep(0.08h, 0.92h, radius);
                    half ring = (1.0h - smoothstep(0.02h, 0.1h, abs(radius - 0.72h))) * 0.24h;
                    alpha = saturate(core + ring) * _BaseColor.a;
                }

                alpha *= saturate(_Intensity);
                return half4(_BaseColor.rgb * (0.72h + _Intensity * 0.45h), alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
