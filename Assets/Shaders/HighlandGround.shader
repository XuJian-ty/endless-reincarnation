Shader "Game/Environment/HighlandGround"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.18, 0.31, 0.12, 1)
        _SecondaryColor("Secondary Color", Color) = (0.38, 0.45, 0.18, 1)
        _SlopeColor("Slope Color", Color) = (0.24, 0.22, 0.18, 1)
        _GroundTex("Ground Texture", 2D) = "white" {}
        _RockTex("Rock Texture", 2D) = "white" {}
        _SnowTex("Snow Texture", 2D) = "white" {}
        _GroundTiling("Ground Tiling", Range(0.01, 1.0)) = 0.12
        _RockTiling("Rock Tiling", Range(0.01, 1.0)) = 0.08
        _TextureStrength("Texture Strength", Range(0, 1)) = 0.82
        _SnowAmount("Snow Amount", Range(0, 1)) = 0
        _SnowLine("Snow Line", Float) = 95
        _SnowFade("Snow Fade", Range(1, 80)) = 24
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.34
        _MacroScale("Macro Scale", Range(0.002, 0.2)) = 0.018
        _DetailScale("Detail Scale", Range(0.05, 2.0)) = 0.42
        _SlopeStart("Slope Start", Range(0, 1)) = 0.35
        _SlopeBlend("Slope Blend", Range(0.01, 1)) = 0.42
        _Smoothness("Smoothness", Range(0, 1)) = 0.18
        _NormalVariation("Normal Variation", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GroundTex);
            SAMPLER(sampler_GroundTex);
            TEXTURE2D(_RockTex);
            SAMPLER(sampler_RockTex);
            TEXTURE2D(_SnowTex);
            SAMPLER(sampler_SnowTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SecondaryColor;
                half4 _SlopeColor;
                float _GroundTiling;
                float _RockTiling;
                float _TextureStrength;
                float _SnowAmount;
                float _SnowLine;
                float _SnowFade;
                float _AmbientStrength;
                float _MacroScale;
                float _DetailScale;
                float _SlopeStart;
                float _SlopeBlend;
                float _Smoothness;
                float _NormalVariation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + 1.0);
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            half3 SampleRock(float3 positionWS, float3 normalWS)
            {
                float3 weights = pow(abs(normalWS), 4.0);
                weights /= max(0.001, weights.x + weights.y + weights.z);
                half3 xProjection = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, positionWS.zy * _RockTiling).rgb;
                half3 yProjection = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, positionWS.xz * _RockTiling).rgb;
                half3 zProjection = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, positionWS.xy * _RockTiling).rgb;
                return xProjection * weights.x + yProjection * weights.y + zProjection * weights.z;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float2 worldXZ = input.positionWS.xz;
                float macro = ValueNoise(worldXZ * _MacroScale);
                float detail = ValueNoise(worldXZ * _DetailScale + macro * 7.3);
                float ridges = abs(ValueNoise(worldXZ * (_MacroScale * 2.7) + 31.7) * 2.0 - 1.0);
                float slope = 1.0 - saturate(normalWS.y);
                float slopeMask = smoothstep(_SlopeStart, _SlopeStart + _SlopeBlend, slope);

                half3 meadow = lerp(_BaseColor.rgb, _SecondaryColor.rgb, saturate(macro * 0.72 + detail * 0.28));
                meadow *= lerp(0.78, 1.12, ridges);
                half3 groundTexture = SAMPLE_TEXTURE2D(_GroundTex, sampler_GroundTex, worldXZ * _GroundTiling).rgb;
                half3 rockTexture = SampleRock(input.positionWS, normalWS);
                groundTexture = pow(max(groundTexture, 0.001), 0.66) * 1.22 + 0.14;
                rockTexture = pow(max(rockTexture, 0.001), 0.7) * 1.18 + 0.1;
                meadow *= lerp(half3(1.0, 1.0, 1.0), groundTexture, _TextureStrength);
                half3 slopeAlbedo = _SlopeColor.rgb * lerp(0.78, 1.14, detail);
                slopeAlbedo *= lerp(half3(1.0, 1.0, 1.0), rockTexture, _TextureStrength);
                half3 albedo = lerp(meadow, slopeAlbedo, slopeMask);

                float snowHeight = smoothstep(_SnowLine - _SnowFade, _SnowLine + _SnowFade, input.positionWS.y);
                float snowFacing = smoothstep(0.34, 0.78, normalWS.y);
                float snowMask = snowHeight * snowFacing * _SnowAmount;
                half3 snowTexture = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, worldXZ * (_GroundTiling * 0.72)).rgb;
                half3 snowAlbedo = pow(max(snowTexture, 0.001), 0.76) * half3(1.08, 1.1, 1.12);
                albedo = lerp(albedo, snowAlbedo, snowMask);

                float normalNoise = (detail - 0.5) * _NormalVariation;
                normalWS = normalize(normalWS + float3(normalNoise, 0.0, -normalNoise * 0.8));

                Light mainLight = GetMainLight(input.shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambientLight = max(SampleSH(normalWS), half3(_AmbientStrength, _AmbientStrength * 1.06, _AmbientStrength * 0.92));
                half3 ambient = ambientLight * albedo;
                half3 color = ambient + albedo * mainLight.color * ndotl * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                #if defined(_ADDITIONAL_LIGHTS)
                uint additionalLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half lambert = saturate(dot(normalWS, light.direction));
                    color += albedo * light.color * lambert * light.distanceAttenuation * light.shadowAttenuation;
                }
                #endif

                float3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(8.0, 96.0, _Smoothness));
                color += mainLight.color * specular * _Smoothness * 0.18 * mainLight.shadowAttenuation;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
