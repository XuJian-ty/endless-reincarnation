Shader "Game/Environment/HighlandGrass"
{
    Properties
    {
        _BladeTexture("Blade Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.28
        _TextureStrength("Texture Strength", Range(0, 1)) = 0.82
        _BaseColor("Root Color", Color) = (0.08, 0.2, 0.045, 1)
        _TipColor("Tip Color", Color) = (0.48, 0.66, 0.19, 1)
        _DryColor("Dry Color", Color) = (0.58, 0.5, 0.2, 1)
        _WindDirection("Wind Direction", Vector) = (0.82, 0, 0.57, 0)
        _WindStrength("Wind Strength", Range(0, 1.5)) = 0.52
        _WindSpeed("Wind Speed", Range(0, 8)) = 2.1
        _GustScale("Gust Scale", Range(0.001, 0.2)) = 0.025
        _GustStrength("Gust Strength", Range(0, 2)) = 0.72
        _Translucency("Translucency", Range(0, 1)) = 0.42
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.38
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BladeTexture);
            SAMPLER(sampler_BladeTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _BladeTexture_ST;
                float _Cutoff;
                float _TextureStrength;
                half4 _BaseColor;
                half4 _TipColor;
                half4 _DryColor;
                float4 _WindDirection;
                float _WindStrength;
                float _WindSpeed;
                float _GustScale;
                float _GustStrength;
                float _Translucency;
                float _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half2 uv : TEXCOORD2;
                half4 color : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 worldPosition = TransformObjectToWorld(input.positionOS.xyz);
                float rootWeight = saturate(input.uv.y);
                float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float phase = Hash21(floor(worldPosition.xz * 0.73)) * 6.28318;
                float travellingWave = sin(dot(worldPosition.xz, windDirection) * 0.085 - _Time.y * _WindSpeed + phase);
                float crossWave = sin(dot(worldPosition.xz, float2(-windDirection.y, windDirection.x)) * 0.047 + _Time.y * (_WindSpeed * 0.63) + phase * 0.61);
                float gust = sin(dot(worldPosition.xz, windDirection) * _GustScale - _Time.y * (_WindSpeed * 0.38));
                gust = smoothstep(0.05, 0.92, gust * 0.5 + 0.5) * _GustStrength;
                float bend = (travellingWave * 0.28 + crossWave * 0.12 + 0.62 + gust) * _WindStrength;
                float bendWeight = rootWeight * rootWeight;
                worldPosition.xz += windDirection * bend * bendWeight;
                worldPosition.y -= abs(bend) * 0.08 * bendWeight;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(TransformWorldToObject(worldPosition));
                output.positionCS = positionInputs.positionCS;
                output.positionWS = worldPosition;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BladeTexture);
                output.color = input.color;
                output.shadowCoord = TransformWorldToShadowCoord(worldPosition);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half4 bladeSample = SAMPLE_TEXTURE2D(_BladeTexture, sampler_BladeTexture, input.uv);
                clip(bladeSample.a - _Cutoff);
                half3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                half spatialVariation = Hash21(floor(input.positionWS.xz * 0.58));
                half dryVariation = saturate((spatialVariation - 0.48) * 1.7);
                half3 tipColor = lerp(_TipColor.rgb, _DryColor.rgb, dryVariation * 0.62);
                half3 albedo = lerp(_BaseColor.rgb, tipColor, saturate(input.uv.y * 0.86 + 0.08));
                albedo *= lerp(0.84, 1.1, spatialVariation);
                albedo *= lerp(half3(1.0, 1.0, 1.0), pow(max(bladeSample.rgb, 0.03), 0.72) * 1.22, _TextureStrength);
                albedo *= lerp(half3(1.0, 1.0, 1.0), input.color.rgb, 0.52);
                Light mainLight = GetMainLight(input.shadowCoord);
                half forwardLight = saturate(dot(normalWS, mainLight.direction) * 0.72 + 0.28);
                half backLight = pow(saturate(dot(-normalWS, mainLight.direction)), 1.6) * _Translucency;
                half3 ambientLight = max(SampleSH(float3(0.0, 1.0, 0.0)), half3(_AmbientStrength, _AmbientStrength * 1.08, _AmbientStrength * 0.88));
                half3 ambient = ambientLight * albedo;
                half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 color = ambient + albedo * mainLight.color * (forwardLight + backLight) * attenuation;
                color += _TipColor.rgb * mainLight.color * backLight * 0.18;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
