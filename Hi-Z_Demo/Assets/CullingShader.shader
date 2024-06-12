Shader "Custom/CullingShader"
{
    Properties
    {
        _ObjectAABBTexture0("Object AABB Texture 0", 2D) = "white" {}
        _ObjectAABBTexture1("Object AABB Texture 1", 2D) = "white" {}
        _DepthPyramidTex("Depth Pyramid Texture", 2D) = "white" {}
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
            #pragma vertex Vert
            #pragma fragment CullingFrag

            // Unity内置宏定义
            #pragma multi_compile _ UNITY_REVERSED_Z

            #include "UnityCG.cginc"
            // 包含Unity Shader核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 纹理和采样器定义
            TEXTURE2D(_ObjectAABBTexture0);
            SAMPLER(sampler_ObjectAABBTexture0);
            TEXTURE2D(_ObjectAABBTexture1);
            SAMPLER(sampler_ObjectAABBTexture1);
            TEXTURE2D(_DepthPyramidTex);

            // 属性定义
            float4x4 _GPUCullingVP;
            float2 _MipmapLevelMinMaxIndex;
            float2 _Mip0Size;
            float4 _MipOffsetAndSize[16];

            // 顶点着色器输入结构体
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            // 顶点着色器输出结构体
            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            // 顶点着色器
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.texcoord;
                return output;
            }

            // 深度剔除函数
            float3 TransferNDC(float3 pos)
            {
                float4 ndc = mul(_GPUCullingVP, float4(pos, 1.0));
                ndc.xyz /= ndc.w;
                ndc.xy = ndc.xy * 0.5f + 0.5f;
                ndc.y = 1 - ndc.y;
                return ndc.xyz;
            }

            // 片段着色器
            half4 CullingFrag(Varyings input) : SV_Target
            {
                  float2 uv = input.uv;
            float4 aabbCenter = SAMPLE_TEXTURE2D_LOD(_ObjectAABBTexture0, sampler_ObjectAABBTexture0, uv,0.0);
            float4 aabbSize = SAMPLE_TEXTURE2D_LOD(_ObjectAABBTexture1, sampler_ObjectAABBTexture1, uv, 0.0);
            float3 aabbExtent = aabbSize.xyz * 0.5;//贴图可以直接存extent
            UNITY_BRANCH
            if (aabbCenter.a == 0.0) 
            {
                return 1;
            }
            float3 aabbMin = aabbCenter.xyz - aabbExtent;
            float3 aabbMax = aabbCenter.xyz + aabbExtent;

            float3 pos0 = float3(aabbMin.x, aabbMin.y, aabbMin.z);
            float3 pos1 = float3(aabbMin.x, aabbMin.y, aabbMax.z);
            float3 pos2 = float3(aabbMin.x, aabbMax.y, aabbMin.z);
            float3 pos3 = float3(aabbMax.x, aabbMin.y, aabbMin.z);
            float3 pos4 = float3(aabbMax.x, aabbMax.y, aabbMin.z);
            float3 pos5 = float3(aabbMax.x, aabbMin.y, aabbMax.z);
            float3 pos6 = float3(aabbMin.x, aabbMax.y, aabbMax.z);
            float3 pos7 = float3(aabbMax.x, aabbMax.y, aabbMax.z);
          

            float3 ndc = TransferNDC(pos0);
            float3 ndcMax = ndc;
            float3 ndcMin = ndc;
            ndc = TransferNDC(pos1);
            ndcMax = max(ndc, ndcMax);
            ndcMin = min(ndc, ndcMin);
            ndc = TransferNDC(pos2);
            ndcMax = max(ndc, ndcMax);
            ndcMin = min(ndc, ndcMin);
            ndc = TransferNDC(pos3);
            ndcMax = max(ndc, ndcMax);
            ndcMin = min(ndc, ndcMin);
            ndc = TransferNDC(pos4);
            ndcMax = max(ndc, ndcMax);
            ndcMin = min(ndc, ndcMin);
            ndc = TransferNDC(pos5);
            ndcMax = max(ndc, ndcMax);
            ndcMin = min(ndc, ndcMin);
            ndc = TransferNDC(pos6);
            ndcMax = max(ndc, ndcMax);
            ndcMin = min(ndc, ndcMin);
            ndc = TransferNDC(pos7);
            ndcMax = max(ndc, ndcMax);
            ndcMin = min(ndc, ndcMin);

            // float2 ndcSize = floor((ndcMax.xy - ndcMin.xy) * _Mip0Size);
            // float raidus = max(ndcSize.x, ndcSize.y);
            // int mip = ceil(log2(raidus));
            // mip = clamp(mip, _MipmapLevelMinMaxIndex.x, _MipmapLevelMinMaxIndex.y);
            // float4 offsetAndSize = _MipOffsetAndSize[mip];
            int4 pxMinMax = float4(ndcMin.xy,ndcMax.xy);// * offsetAndSize.zwzw + offsetAndSize.xyxy;


            float d0 = LOAD_TEXTURE2D_LOD(_DepthPyramidTex, pxMinMax.xy,0); // lb
            float d1 = LOAD_TEXTURE2D_LOD(_DepthPyramidTex, pxMinMax.zy,0); // rb
            float d2 = LOAD_TEXTURE2D_LOD(_DepthPyramidTex, pxMinMax.xw,0); // lt
            float d3 = LOAD_TEXTURE2D_LOD(_DepthPyramidTex, pxMinMax.zw,0); // rt
#if UNITY_REVERSED_Z
            float minDepth = min(min(min(d0, d1), d2), d3);
            return  ndcMax.z < minDepth ? half4(1, 0, 0, 1) : half4(0, 0, 0, 1);
#else
            float maxDepth = max(max(max(d0, d1), d2), d3);
            return maxDepth > ndcMax.z   ? half4(1, 0, 0, 1) : half4(0, 0, 0, 1);
#endif
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}