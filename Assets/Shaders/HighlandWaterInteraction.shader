Shader "Game/Environment/HighlandWaterInteraction"
{
    Properties
    {
        _TintColor("Tint Color", Color) = (0.72, 0.86, 0.86, 0.32)
        _EffectMode("Effect Mode", Float) = 0
        _Opacity("Opacity", Range(0, 1)) = 0.32
        _Softness("Softness", Range(0.01, 1)) = 0.32
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "WaterInteraction"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _EffectMode;
                float _Opacity;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _TintColor;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha;
                if (_EffectMode < 0.5)
                {
                    float angle = input.uv.y * 6.2831853;
                    half band = pow(saturate(1.0 - abs(input.uv.x * 2.0 - 1.0)), lerp(1.8, 4.2, _Softness));
                    half broadVariation = sin(angle * 2.0 + 0.7) * 0.42
                                        + sin(angle * 5.0 - 1.9) * 0.25;
                    half brokenArc = smoothstep(0.02, 0.34, broadVariation);
                    half fineVariation = 0.72 + sin(angle * 11.0 + 2.4) * 0.16
                                               + sin(angle * 17.0 - 0.8) * 0.08;
                    alpha = band * brokenArc * saturate(fineVariation);
                }
                else if (_EffectMode < 1.5)
                {
                    float2 centered = input.uv - float2(0.5, 0.5);
                    half taper = lerp(0.15, 0.035, saturate(input.uv.y));
                    half side = 1.0 - smoothstep(taper * 0.58, taper, abs(centered.x));
                    half ends = smoothstep(0.0, 0.18, input.uv.y)
                              * smoothstep(0.0, 0.3, 1.0 - input.uv.y);
                    half softCore = 1.0 - smoothstep(0.0, max(0.012, taper * 0.55), abs(centered.x));
                    alpha = side * ends * lerp(0.44, 1.0, softCore);
                }
                else
                {
                    half crest = pow(saturate(1.0 - abs(input.uv.x * 2.0 - 1.0)), lerp(2.4, 5.2, _Softness));
                    half headFade = smoothstep(0.0, 0.1, input.uv.y);
                    half tailFade = 1.0 - smoothstep(0.46, 1.0, input.uv.y);
                    half coarseBreakup = sin(input.uv.y * 16.0 + 0.8) * 0.52
                                       + sin(input.uv.y * 35.0 - 1.7) * 0.28;
                    half brokenSegments = smoothstep(-0.34, 0.16, coarseBreakup + 0.1);
                    half fineVariation = 0.82 + sin(input.uv.y * 57.0 + 2.1) * 0.18;
                    alpha = crest * headFade * tailFade * brokenSegments * saturate(fineVariation);
                }

                alpha *= input.color.a * _Opacity;
                clip(alpha - 0.004);
                half3 color = MixFog(input.color.rgb, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
