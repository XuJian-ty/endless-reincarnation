Shader "Game/Environment/HighlandFineGrass"
{
    Properties
    {
        _BaseColor("Root Color", Color) = (0.035, 0.12, 0.012, 1)
        _TipColor("Tip Color", Color) = (0.28, 0.45, 0.08, 1)
        _DryColor("Dry Color", Color) = (0.45, 0.34, 0.08, 1)
        _WindDirection("Wind Direction", Vector) = (0.82, 0, 0.57, 0)
        _WindStrength("Wind Strength", Range(0, 1)) = 0.24
        _WindSpeed("Wind Speed", Range(0, 8)) = 1.85
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.54
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+5"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _DryColor;
                float4 _WindDirection;
                float _WindStrength;
                float _WindSpeed;
                float _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 direction = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float phase = Hash21(floor(positionWS.xz * 0.67)) * 6.28318;
                float wave = sin(dot(positionWS.xz, direction) * 0.11 - _Time.y * _WindSpeed + phase);
                float bend = input.uv.y * input.uv.y * _WindStrength * (0.52 + wave * 0.48);
                positionWS.xz += direction * bend;
                positionWS.y -= abs(bend) * 0.055;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                half variation = Hash21(floor(input.positionWS.xz * 0.83));
                half dryMask = smoothstep(0.78, 0.98, variation);
                half3 tip = lerp(_TipColor.rgb, _DryColor.rgb, dryMask);
                half3 albedo = lerp(_BaseColor.rgb, tip, saturate(input.uv.y * 0.9 + 0.04));
                albedo *= lerp(0.82, 1.12, variation);

                Light mainLight = GetMainLight(input.shadowCoord);
                half diffuse = saturate(dot(normalWS, mainLight.direction) * 0.58 + 0.42);
                half backLight = saturate(dot(-normalWS, mainLight.direction)) * 0.26;
                half3 ambientLight = max(SampleSH(float3(0.0, 1.0, 0.0)), _AmbientStrength.xxx);
                half3 color = ambientLight * albedo;
                color += albedo * mainLight.color * (diffuse + backLight) * mainLight.shadowAttenuation;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
