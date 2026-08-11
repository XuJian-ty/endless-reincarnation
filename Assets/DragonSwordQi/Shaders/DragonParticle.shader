Shader "DragonSwordQi/DragonParticle"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (0.32, 0.86, 1.0, 1.0)
        _Softness ("Softness", Range(0.2, 8.0)) = 2.2
        _StarStrength ("Star Strength", Range(0.0, 1.0)) = 0.42
        _EffectAlpha ("Effect Alpha", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "DragonParticle"
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
                half4 _TintColor;
                float _Softness;
                float _StarStrength;
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

                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float disc = pow(saturate(1.0 - radius), max(0.01, _Softness));
                float angle = atan2(centered.y, centered.x);
                float rays = pow(abs(cos(angle * 4.0)), 18.0);
                rays *= pow(saturate(1.0 - radius), 0.55);
                float core = pow(saturate(1.0 - radius * 1.72), 2.2);
                float shape = saturate(disc + rays * _StarStrength + core);

                half3 color = input.color.rgb * _TintColor.rgb;
                color *= 0.62 + core * 2.8 + rays * 1.35;
                float alpha = input.color.a * _TintColor.a * _EffectAlpha * shape;
                clip(alpha - 0.003);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
