Shader "Game/VFX/RogueliteSealGate"
{
    Properties
    {
        [HDR] _BaseColor("Base Color", Color) = (0.28, 0.78, 1.0, 0.22)
        [HDR] _EmissionColor("Emission Color", Color) = (0.36, 0.9, 1.35, 1.0)
        _Intensity("Intensity", Range(0.2, 5.0)) = 0.82
        _FlowSpeed("Flow Speed", Range(0.0, 4.0)) = 0.72
        _NoiseScale("Noise Scale", Range(1.0, 20.0)) = 5.5
        _EdgeSoftness("Edge Softness", Range(0.01, 0.4)) = 0.16
        _DepthSoftness("Depth Softness", Range(0.05, 5.0)) = 1.1
        _DistanceFadeStart("Distance Fade Start", Range(1.0, 200.0)) = 55.0
        _DistanceFadeEnd("Distance Fade End", Range(2.0, 300.0)) = 170.0
        _Pulse("Pulse", Range(0.2, 2.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "SealEnergy"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float4 screenPos : TEXCOORD1;
                float viewDepth : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                float _Intensity;
                float _FlowSpeed;
                float _NoiseScale;
                float _EdgeSoftness;
                float _DepthSoftness;
                float _DistanceFadeStart;
                float _DistanceFadeEnd;
                float _Pulse;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.viewDepth = -TransformWorldToView(positionWS).z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv;
                float time = _Time.y * _FlowSpeed;
                float2 flowUv = float2(uv.x * _NoiseScale, uv.y * (_NoiseScale * 0.72) - time);
                float noiseA = ValueNoise(flowUv + float2(sin(uv.y * 9.0 + time) * 0.65, 0.0));
                float noiseB = ValueNoise(flowUv * 1.83 + float2(4.7, time * -0.74));
                float wisps = saturate(noiseA * 0.68 + noiseB * 0.36 - 0.31);
                float runes = pow(saturate(0.5 + 0.5 * sin(uv.x * 34.0 + noiseA * 5.0 - time * 1.8)), 12.0);
                float waves = pow(saturate(0.5 + 0.5 * sin(uv.y * 24.0 - time * 2.2 + noiseB * 3.0)), 14.0);

                float edgeX = smoothstep(0.0, _EdgeSoftness, uv.x) * smoothstep(0.0, _EdgeSoftness, 1.0 - uv.x);
                float edgeY = smoothstep(0.0, _EdgeSoftness, uv.y) * smoothstep(0.0, _EdgeSoftness, 1.0 - uv.y);
                float edgeFade = edgeX * edgeY;
                float centerGlow = saturate(1.0 - abs(uv.x - 0.5) * 1.75);
                float energy = (wisps * 0.42 + runes * 0.07 + waves * 0.05 + centerGlow * 0.1 + 0.08) * edgeFade;

                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                float depthFade = saturate((sceneDepth - input.viewDepth) / max(_DepthSoftness, 0.001));
                float distanceFade = 1.0 - smoothstep(_DistanceFadeStart, max(_DistanceFadeStart + 0.01, _DistanceFadeEnd), input.viewDepth);

                half4 vertexColor = input.color.a > 0.001 ? input.color : half4(1.0, 1.0, 1.0, 1.0);
                half3 color = lerp(_BaseColor.rgb, _EmissionColor.rgb, saturate(wisps * 0.48 + runes * 0.12));
                color *= vertexColor.rgb * _Intensity * _Pulse;
                half alpha = saturate(energy * _BaseColor.a * vertexColor.a * _Pulse * depthFade * lerp(0.42, 1.0, distanceFade));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
