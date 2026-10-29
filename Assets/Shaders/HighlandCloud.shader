Shader "Game/Environment/HighlandCloud"
{
    Properties
    {
        _LightColor("Light Color", Color) = (1, 0.96, 0.88, 1)
        _ShadowColor("Shadow Color", Color) = (0.38, 0.46, 0.53, 1)
        _CloudScale("Cloud Scale", Range(0.2, 12)) = 3.4
        _Density("Density", Range(0, 1)) = 0.54
        _Softness("Softness", Range(0.01, 0.5)) = 0.16
        _Speed("Speed", Range(0, 0.2)) = 0.018
        _Direction("Direction", Vector) = (0.82, 0, 0.57, 0)
        _Opacity("Opacity", Range(0, 1)) = 0.62
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudLayer"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _ShadowColor;
                float _CloudScale;
                float _Density;
                float _Softness;
                float _Speed;
                float4 _Direction;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash21(i), Hash21(i + float2(1.0, 0.0)), f.x),
                    lerp(Hash21(i + float2(0.0, 1.0)), Hash21(i + 1.0), f.x),
                    f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.54;
                float2 shift = float2(17.31, 9.24);
                for (int octave = 0; octave < 5; ++octave)
                {
                    value += Noise(p) * amplitude;
                    p = p * 2.03 + shift;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 direction = normalize(_Direction.xz + float2(0.0001, 0.0001));
                float2 movingUV = input.uv * _CloudScale + direction * (_Time.y * _Speed);
                float broad = Fbm(movingUV);
                float detail = Fbm(movingUV * 2.8 + float2(12.7, -8.4));
                float cloudField = broad * 0.78 + detail * 0.22;
                float threshold = lerp(0.76, 0.34, _Density);
                float cloud = smoothstep(threshold - _Softness, threshold + _Softness, cloudField);
                float edge = saturate(min(min(input.uv.x, 1.0 - input.uv.x), min(input.uv.y, 1.0 - input.uv.y)) * 8.0);
                float lighting = saturate(detail * 0.72 + broad * 0.38);
                half3 color = lerp(_ShadowColor.rgb, _LightColor.rgb, lighting);
                return half4(color, cloud * edge * _Opacity);
            }
            ENDHLSL
        }
    }
}
