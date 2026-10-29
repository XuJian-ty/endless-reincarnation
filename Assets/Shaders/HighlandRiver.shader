Shader "Game/Environment/HighlandRiver"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.16, 0.48, 0.46, 0.72)
        _DeepColor("Deep Color", Color) = (0.035, 0.19, 0.24, 0.86)
        _SunGlintColor("Sun Glint", Color) = (1, 0.88, 0.62, 1)
        _FoamColor("Foam Color", Color) = (0.82, 0.91, 0.84, 1)
        _FlowDirection("Flow Direction", Vector) = (0.18, 0, -1, 0)
        _FlowSpeed("Flow Speed", Range(0, 4)) = 0.72
        _WaveScale("Wave Scale", Range(0.02, 2)) = 0.24
        _WaveStrength("Wave Strength", Range(0, 1)) = 0.22
        _DepthRange("Depth Range", Range(0.1, 12)) = 4.2
        _FoamWidth("Foam Width", Range(0.01, 3)) = 0.52
        _Smoothness("Smoothness", Range(0, 1)) = 0.88
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-30"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "RiverForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _SunGlintColor;
                half4 _FoamColor;
                float4 _FlowDirection;
                float _FlowSpeed;
                float _WaveScale;
                float _WaveStrength;
                float _DepthRange;
                float _FoamWidth;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half2 uv : TEXCOORD4;
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

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.54;
                for (int octave = 0; octave < 4; ++octave)
                {
                    value += ValueNoise(p) * amplitude;
                    p = p * 2.07 + float2(13.17, -9.41);
                    amplitude *= 0.49;
                }
                return value;
            }

            float WaveHeight(float2 positionXZ)
            {
                float2 flowDirection = normalize(_FlowDirection.xz + float2(0.0001, 0.0001));
                float2 crossDirection = float2(-flowDirection.y, flowDirection.x);
                float time = _Time.y * _FlowSpeed;
                float2 moving = positionXZ * (_WaveScale * 0.72) - flowDirection * time * 0.54;
                float broad = Fbm(moving);
                float fine = Fbm(moving * 2.73 + crossDirection * time * 0.31 + 19.4);
                float ripple = sin(dot(positionXZ, normalize(flowDirection + crossDirection * 0.38)) * (_WaveScale * 13.0) - time * 3.8);
                return ((broad - 0.5) * 0.72 + (fine - 0.5) * 0.38 + ripple * 0.08) * _WaveStrength;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.y += WaveHeight(positionWS.xz) * 0.12;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const float epsilon = 0.18;
                float centerHeight = WaveHeight(input.positionWS.xz);
                float heightX = WaveHeight(input.positionWS.xz + float2(epsilon, 0.0));
                float heightZ = WaveHeight(input.positionWS.xz + float2(0.0, epsilon));
                float3 normalWS = normalize(float3(centerHeight - heightX, epsilon * 1.35, centerHeight - heightZ));

                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceEyeDepth = input.screenPosition.w;
                float thickness = max(0.0, sceneEyeDepth - surfaceEyeDepth);
                float depthFactor = saturate(thickness / max(0.001, _DepthRange));
                float shoreFoam = 1.0 - smoothstep(0.05, max(0.06, _FoamWidth), thickness);
                float2 flowDirection = normalize(_FlowDirection.xz + float2(0.0001, 0.0001));
                float2 crossDirection = float2(-flowDirection.y, flowDirection.x);
                float time = _Time.y * _FlowSpeed;
                float alongFlow = dot(input.positionWS.xz, flowDirection);
                float acrossFlow = dot(input.positionWS.xz, crossDirection);
                float brokenWater = Fbm(float2(alongFlow * 0.21 - time * 1.1, acrossFlow * 0.47) + 7.3);
                float fineFoam = Fbm(input.positionWS.xz * 0.39 - flowDirection * time * 1.7 + 31.8);
                float currentFoam = smoothstep(0.72, 0.91, brokenWater * 0.64 + fineFoam * 0.36 + abs(centerHeight) * 0.8);
                float shallowRapids = (1.0 - smoothstep(0.12, 0.72, depthFactor)) * smoothstep(0.58, 0.86, fineFoam);
                float foam = saturate(shoreFoam + currentFoam * 0.19 + shallowRapids * 0.34);

                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half sunGlint = pow(saturate(dot(normalWS, halfDirection)), lerp(42.0, 220.0, _Smoothness));
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirection)), 4.0);
                half diffuse = saturate(dot(normalWS, mainLight.direction)) * 0.18 + 0.82;
                half3 color = waterColor * diffuse;
                color += _SunGlintColor.rgb * sunGlint * mainLight.color * mainLight.shadowAttenuation;
                color = lerp(color, _FoamColor.rgb, foam * 0.86);
                color += fresnel * half3(0.19, 0.29, 0.31);
                color = MixFog(color, input.fogFactor);
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depthFactor) + fresnel * 0.08 + foam * 0.14;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
