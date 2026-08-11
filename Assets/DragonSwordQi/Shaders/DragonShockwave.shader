Shader "DragonSwordQi/DragonShockwave"
{
    Properties
    {
        [HDR] _RingColor ("Ring Color", Color) = (0.08, 0.68, 1.0, 0.82)
        [HDR] _CoreColor ("Core Color", Color) = (0.76, 1.0, 1.0, 1.0)
        _RingRadius ("Ring Radius", Range(0.1, 0.95)) = 0.64
        _RingWidth ("Ring Width", Range(0.01, 0.4)) = 0.12
        _RayStrength ("Ray Strength", Range(0.0, 2.0)) = 0.62
        _EffectAlpha ("Effect Alpha", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DragonShockwave"
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
                half4 _RingColor;
                half4 _CoreColor;
                float _RingRadius;
                float _RingWidth;
                float _RayStrength;
                float _EffectAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);
                float ringDistance = abs(radius - _RingRadius);
                float ring = 1.0 - smoothstep(_RingWidth * 0.18, _RingWidth, ringDistance);
                float outerHalo = 1.0 - smoothstep(_RingWidth, _RingWidth * 3.2, ringDistance);
                float rays = pow(abs(cos(angle * 9.0 + _Time.y * 1.7)), 22.0);
                rays *= saturate(1.0 - radius) * _RayStrength;
                float centerFlash = pow(saturate(1.0 - radius / max(0.01, _RingRadius)), 3.2);

                half3 color = _RingColor.rgb * (outerHalo * 0.58 + rays);
                color += _CoreColor.rgb * (ring * 1.28 + centerFlash * 0.22);
                float alpha = _EffectAlpha
                              * _RingColor.a
                              * saturate(ring + outerHalo * 0.42 + rays * 0.62 + centerFlash * 0.12);
                clip(alpha - 0.003);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
