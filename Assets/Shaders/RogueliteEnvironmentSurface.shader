Shader "Game/Environment/RogueliteSurface"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _DetailMap("Detail Map", 2D) = "gray" {}
        _BaseColor("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        _SecondaryColor("Secondary Color", Color) = (0.2, 0.2, 0.2, 1)
        _DetailScale("Detail Scale", Range(0.25, 24.0)) = 6.0
        _PatternStrength("Pattern Strength", Range(0.0, 1.0)) = 0.38
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.3
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _NormalStrength("Micro Normal Strength", Range(0.0, 2.0)) = 0.42
        _RoughnessVariation("Roughness Variation", Range(0.0, 1.0)) = 0.3
        _MacroScale("Macro Variation Scale", Range(0.02, 1.0)) = 0.14
        _OcclusionStrength("Crevice Occlusion", Range(0.0, 1.0)) = 0.35
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SecondaryColor;
                half4 _EmissionColor;
                float _DetailScale;
                float _PatternStrength;
                float _Smoothness;
                float _Metallic;
                float _NormalStrength;
                float _RoughnessVariation;
                float _MacroScale;
                float _OcclusionStrength;
            CBUFFER_END

            half SampleTriplanarDetail(float3 positionWS, half3 normalWS, float scale)
            {
                half3 blendWeights = pow(abs(normalWS), 4.0h);
                blendWeights /= max(blendWeights.x + blendWeights.y + blendWeights.z, 0.001h);
                half detailX = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, positionWS.zy * scale).r;
                half detailY = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, positionWS.xz * scale).r;
                half detailZ = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, positionWS.xy * scale).r;
                return dot(half3(detailX, detailY, detailZ), blendWeights);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = normalize(input.normalWS);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float detailScale = 0.045 * _DetailScale;
                half detail = SampleTriplanarDetail(input.positionWS, normalWS, detailScale);
                const float bumpStep = 0.14;
                half3 detailGradient = half3(
                    SampleTriplanarDetail(input.positionWS + float3(bumpStep, 0.0, 0.0), normalWS, detailScale) - detail,
                    SampleTriplanarDetail(input.positionWS + float3(0.0, bumpStep, 0.0), normalWS, detailScale) - detail,
                    SampleTriplanarDetail(input.positionWS + float3(0.0, 0.0, bumpStep), normalWS, detailScale) - detail);
                detailGradient -= normalWS * dot(detailGradient, normalWS);
                normalWS = normalize(normalWS - detailGradient * (_NormalStrength / bumpStep));

                half macroDetail = SampleTriplanarDetail(input.positionWS, normalWS, detailScale * _MacroScale);
                half pattern = smoothstep(0.22h, 0.82h, detail);
                half macroPattern = smoothstep(0.18h, 0.86h, macroDetail);
                half baseLuminance = dot(baseSample.rgb, half3(0.299h, 0.587h, 0.114h));
                half3 textureTint = clamp(baseSample.rgb / max(baseLuminance, 0.08h), 0.58h, 1.42h);
                half colorBlend = saturate(0.34h + baseLuminance * 0.44h + pattern * 0.22h);
                half3 albedo = lerp(_SecondaryColor.rgb, _BaseColor.rgb, colorBlend);
                albedo *= lerp(half3(1.0h, 1.0h, 1.0h), textureTint, 0.34h);
                albedo *= lerp(1.0h, lerp(0.9h, 1.08h, pattern), _PatternStrength);
                albedo *= lerp(0.88h, 1.08h, macroPattern);
                half surfaceOcclusion = lerp(1.0h, lerp(0.72h, 1.0h, smoothstep(0.12h, 0.7h, detail)), _OcclusionStrength);
                half surfaceSmoothness = saturate(_Smoothness - (detail - 0.5h) * _RoughnessVariation);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = saturate((dot(normalWS, mainLight.direction) + 0.28h) / 1.28h);
                half3 ambient = max(SampleSH(normalWS), half3(0.22h, 0.22h, 0.24h));
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specularPower = lerp(8.0h, 96.0h, surfaceSmoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower) * lerp(0.08h, 0.72h, surfaceSmoothness);
                half3 specularColor = lerp(half3(0.04h, 0.04h, 0.04h), albedo, _Metallic);
                half shadow = lerp(0.42h, 1.0h, mainLight.shadowAttenuation);
                half3 color = albedo * surfaceOcclusion * (ambient + mainLight.color * diffuse * shadow);
                color += specularColor * specular * mainLight.color * mainLight.shadowAttenuation;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
                    {
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                        half additionalAttenuation = additionalLight.distanceAttenuation * additionalLight.shadowAttenuation;
                        half additionalDiffuse = saturate(dot(normalWS, additionalLight.direction));
                        half3 additionalHalfDirection = SafeNormalize(additionalLight.direction + viewDirection);
                        half additionalSpecular = pow(saturate(dot(normalWS, additionalHalfDirection)), specularPower)
                            * lerp(0.05h, 0.55h, surfaceSmoothness);
                        color += albedo * surfaceOcclusion * additionalLight.color * additionalDiffuse * additionalAttenuation;
                        color += specularColor * additionalSpecular * additionalLight.color * additionalAttenuation;
                    }
                #endif
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    color += albedo * surfaceOcclusion * VertexLighting(input.positionWS, normalWS);
                #endif

                color += _EmissionColor.rgb * (0.35h + pattern * 0.65h);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack Off
}
