Shader "Custom/DownsampleDepthShader"
{
    Properties
    {
        _InputDepth("Input Depth", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment DownsampleDepthFrag

            // 包含Unity的Shader库
            #pragma multi_compile _ UNITY_REVERSED_Z

            // Core.hlsl 文件包含常用的 HLSL 宏和
            // 函数的定义，还包含对其他 HLSL 文件（例如
            // Common.hlsl、SpaceTransforms.hlsl 等）的 #include 引用。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // 包含其他需要的Unity Shader宏定义

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                return output;
            }

            // 条件编译宏定义
            #if UNITY_REVERSED_Z
            # define MIN_DEPTH(l, r) min(l, r)
            #else
            # define MIN_DEPTH(l, r) max(l, r)
            #endif

            // 定义纹理和采样器
            TEXTURE2D_FLOAT(_InputDepth);
            SAMPLER(sampler_InputDepth);
            float4 _InputScaleAndMaxIndex; // xy: inputTexSize/outputTextureSize, zw: textureSize - 1

            half4 DownsampleDepthFrag(Varyings input) : SV_Target
            {
                int2 texCrood = int2(input.positionCS.xy) * _InputScaleAndMaxIndex.xy;
                uint2 maxIndex = _InputScaleAndMaxIndex.zw;
                int2 texCrood00 = min(texCrood + uint2(0, 0), maxIndex);
                int2 texCrood10 = min(texCrood + uint2(1, 0), maxIndex);
                int2 texCrood01 = min(texCrood + uint2(0, 1), maxIndex);
                int2 texCrood11 = min(texCrood + uint2(1, 1), maxIndex);
                float p00 = LOAD_TEXTURE2D_LOD(_InputDepth, texCrood00, 0);
                float p01 = LOAD_TEXTURE2D_LOD(_InputDepth, texCrood10, 0);
                float p10 = LOAD_TEXTURE2D_LOD(_InputDepth, texCrood01, 0);
                float p11 = LOAD_TEXTURE2D_LOD(_InputDepth, texCrood11, 0);
                return MIN_DEPTH(MIN_DEPTH(p00 ,p01), MIN_DEPTH(p10, p11));
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}