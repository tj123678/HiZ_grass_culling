Shader "Hidden/GenerateDepthRT"
{
    Properties
    {
//        [HideInInspector] _DepthTexture("Depth Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "LightweightPipeline" "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "LightweightForward"
            Tags
            {
                "LightMode" = "LightweightForward"
            }

            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_FLOAT(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D_FLOAT(_DepthTexture);
            SAMPLER(sampler_DepthTexture);
            float4 _CameraDepthTexture_TexelSize;

            struct Attributes
            {
                float4 positioonOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positioonOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float frag(Varyings i) : SV_Target
            {
                float2 offset = _CameraDepthTexture_TexelSize.xy * 0.5;
                float x = SAMPLE_TEXTURE2D(_DepthTexture, sampler_DepthTexture, i.uv + offset).x;
                float y = SAMPLE_TEXTURE2D(_DepthTexture, sampler_DepthTexture, i.uv - offset).x;
                float z = SAMPLE_TEXTURE2D(_DepthTexture, sampler_DepthTexture, i.uv + float2(offset.x, -offset.y)).x;
                float w = SAMPLE_TEXTURE2D(_DepthTexture, sampler_DepthTexture, i.uv + float2(-offset.x, offset.y)).x;
                float4 readDepth = float4(x, y, z, w);
                #if UNITY_REVERSED_Z
                readDepth.xy = min(readDepth.xy, readDepth.zw);
                readDepth.x = min(readDepth.x, readDepth.y);
                #else
                    readDepth.xy = max(readDepth.xy, readDepth.zw);
                    readDepth.x = max(readDepth.x, readDepth.y);
                #endif
                return readDepth.x;
            }
            ENDHLSL
        }
    }
}