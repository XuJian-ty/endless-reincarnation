Shader "DragonSwordQi/DragonEnergy"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.04, 0.58, 1.0, 0.42)
        [HDR] _CoreColor ("Core Color", Color) = (0.72, 1.0, 1.0, 1.0)
        _FlowSpeed ("Flow Speed", Float) = 1.8
        _NoiseScale ("Noise Scale", Float) = 4.5
        _StripeDensity ("Stripe Density", Float) = 7.0
        _FresnelPower ("Fresnel Power", Range(0.2, 8.0)) = 2.0
        _EffectAlpha ("Effect Alpha", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
        }

        Pass
        {
            Name "DragonEnergy"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            Cull Back
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
                float _NoiseScale;
                float _StripeDensity;
                float _FresnelPower;
                float _EffectAlpha;
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
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

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

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), local.x);
                float top = lerp(
                    Hash21(cell + float2(0.0, 1.0)),
                    Hash21(cell + float2(1.0, 1.0)),
                    local.x);
                return lerp(bottom, top, local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float flowTime = _Time.y * _FlowSpeed;
                float2 noiseUv = float2(
                    input.uv.x * 0.72 - flowTime,
                    input.uv.y * 2.0 + flowTime * 0.17);
                float noise = ValueNoise(noiseUv * _NoiseScale);
                float secondaryNoise = ValueNoise(noiseUv * _NoiseScale * 2.07 + 17.4);
                float stripePhase = input.uv.x * _StripeDensity
                                    + input.uv.y * 12.566
                                    - flowTime * 5.2
                                    + noise * 4.0;
                float stripes = 0.5 + 0.5 * sin(stripePhase);
                stripes = smoothstep(0.18, 0.96, stripes);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(
                    saturate(1.0 - abs(dot(normalWS, viewDirectionWS))),
                    max(0.01, _FresnelPower));
                float pulse = 0.82 + sin(_Time.y * 11.0 - input.uv.x * 4.0) * 0.18;
                float energy = saturate(0.24 + stripes * 0.74 + noise * 0.36 + secondaryNoise * 0.22);

                half3 color = _BaseColor.rgb * (0.48 + energy * 1.18);
                color += _CoreColor.rgb * (fresnel * 1.16 + pow(stripes, 5.0) * 0.28);
                color *= pulse;

                float alpha = _BaseColor.a
                              * input.color.a
                              * _EffectAlpha
                              * saturate(0.34 + fresnel * 0.72 + energy * 0.42);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
