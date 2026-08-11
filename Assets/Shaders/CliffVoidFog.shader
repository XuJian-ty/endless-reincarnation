Shader "Game/VFX/CliffVoidFog"
{
    Properties
    {
        [HDR] _BaseColor("Fog Color", Color) = (0.05, 0.12, 0.16, 0.72)
        [HDR] _GlowColor("Mist Highlight", Color) = (0.22, 0.64, 0.78, 1.0)
        _Density("Density", Range(0.0, 2.0)) = 0.9
        _FlowSpeed("Flow Speed", Range(0.0, 2.0)) = 0.18
        _NoiseScale("Noise Scale", Range(0.2, 12.0)) = 3.5
        _EdgeFade("Edge Fade", Range(0.01, 0.48)) = 0.12
        _DepthSoftness("Depth Softness", Range(0.05, 8.0)) = 2.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+5"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "VoidMist"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float viewDepth : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GlowColor;
                float _Density;
                float _FlowSpeed;
                float _NoiseScale;
                float _EdgeFade;
                float _DepthSoftness;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(127.1, 311.7));
                value += dot(value, value + 34.53);
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
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.viewDepth = -TransformWorldToView(output.positionWS).z;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float time = _Time.y * _FlowSpeed;
                float2 worldUv = input.positionWS.xz * (0.018 * _NoiseScale) + float2(time, -time * 0.63);
                float noiseA = ValueNoise(worldUv);
                float noiseB = ValueNoise(worldUv * 2.07 + float2(8.3, -3.7));
                float mist = saturate(noiseA * 0.68 + noiseB * 0.42 - 0.2);
                float edgeX = smoothstep(0.0, _EdgeFade, input.uv.x) * smoothstep(0.0, _EdgeFade, 1.0 - input.uv.x);
                float edgeY = smoothstep(0.0, _EdgeFade, input.uv.y) * smoothstep(0.0, _EdgeFade, 1.0 - input.uv.y);
                float edgeFade = saturate(edgeX * edgeY + 0.18);

                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                float depthFade = saturate((sceneDepth - input.viewDepth) / max(_DepthSoftness, 0.001));
                half3 color = lerp(_BaseColor.rgb, _GlowColor.rgb, mist * 0.3);
                color = MixFog(color, input.fogFactor);
                half alpha = saturate(_BaseColor.a * _Density * (0.48 + mist * 0.52) * edgeFade * depthFade);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
