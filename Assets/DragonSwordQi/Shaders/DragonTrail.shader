Shader "DragonSwordQi/DragonTrail"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.03, 0.52, 1.0, 0.72)
        [HDR] _CoreColor ("Core Color", Color) = (0.82, 1.0, 1.0, 1.0)
        _FlowSpeed ("Flow Speed", Float) = 2.4
        _FlowDensity ("Flow Density", Float) = 10.0
        _EdgePower ("Edge Power", Range(0.2, 8.0)) = 1.8
        _EffectAlpha ("Effect Alpha", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+30"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DragonTrail"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CoreColor;
                float _FlowSpeed;
                float _FlowDensity;
                float _EdgePower;
                float _EffectAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 value)
            {
                value = frac(value * float2(234.34, 435.345));
                value += dot(value, value + 34.23);
                return frac(value.x * value.y);
            }

            float SmoothNoise(float2 value)
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float centeredWidth = abs(input.uv.y * 2.0 - 1.0);
                float widthMask = pow(saturate(1.0 - centeredWidth), _EdgePower);
                float flowTime = _Time.y * _FlowSpeed;
                float2 flowUv = float2(
                    input.uv.x * _FlowDensity - flowTime * 4.0,
                    input.uv.y * 3.0 + flowTime * 0.28);
                float noise = SmoothNoise(flowUv);
                float longBand = 0.5 + 0.5 * sin(
                    input.uv.x * _FlowDensity * 2.6
                    - flowTime * 7.0
                    + noise * 5.2);
                longBand = smoothstep(0.34, 0.94, longBand);

                float outerGlow = pow(saturate(1.0 - centeredWidth), 0.36);
                half3 color = _BaseColor.rgb * (0.38 + noise * 0.92 + outerGlow * 0.55);
                color += _CoreColor.rgb * (widthMask * 1.22 + longBand * 0.72);

                float alpha = _BaseColor.a
                              * input.color.a
                              * _EffectAlpha
                              * saturate(outerGlow * 0.52 + widthMask * 0.72 + longBand * 0.34);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
