using System;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    [BurstCompile]
    struct HizCullJob : IJobParallelFor
    {
        public NativeArray<bool> result;
        [ReadOnly] public NativeArray<Bounds> aabbs;
        [ReadOnly] public NativeArray<float> depth0buffer;
        [ReadOnly] public NativeArray<float> buffer;
        // [ReadOnly]
        // public NativeArray<Vector2Int> mipLevelOffsets;
        // [ReadOnly]
        // public NativeArray<Vector2Int> mipLevelSizes;

        public int textureWidth;
        public int textureHeight;
        public bool isOpenGl;
        public bool usesReversedZBuffer;
        public float4x4 world2HZB;
        public Vector2Int mip0SizeVector;
        public bool dataVail;
        public int mipMaxIndex;
        public int srcLog2;

        public void Execute(int index)
        {
            Log($"1index:{index} {dataVail}");
            if (dataVail)
            {
                bool needCull = IsHizCulled(index);
                Log($"4index:{index} {needCull}");
                result[index] = needCull;
            }
            else
            {
                result[index] = false;
            }
        }

        public float3 TransferNDC(float3 pos)
        {
            float4 ndc = math.mul(world2HZB, math.float4(pos, 1f));
            ndc.xyz /= ndc.w;
            ndc.xy = ndc.xy * 0.5f + 0.5f;
            if (isOpenGl)
            {
                ndc.z = ndc.z * 0.5f + 0.5f;
            }

            // else
            // {
            //     ndc.y = 1 - ndc.y;
            // }
            return ndc.xyz;
        }

        public bool IsHizCulled(int index)
        {
            var bound = aabbs[index];
            float3 bmin = bound.min;
            float3 bmax = bound.max;
            Log($"1.5index:{index} bmin:{bmin} bmax:{bmax}");
            float3 pos0 = math.float3(bmin.x, bmin.y, bmin.z);
            float3 pos1 = math.float3(bmin.x, bmin.y, bmax.z);
            float3 pos2 = math.float3(bmin.x, bmax.y, bmin.z);
            float3 pos3 = math.float3(bmax.x, bmin.y, bmin.z);
            float3 pos4 = math.float3(bmax.x, bmax.y, bmin.z);
            float3 pos5 = math.float3(bmax.x, bmin.y, bmax.z);
            float3 pos6 = math.float3(bmin.x, bmax.y, bmax.z);
            float3 pos7 = math.float3(bmax.x, bmax.y, bmax.z);
            float3 ndcMax = math.float3(float.NegativeInfinity);
            float3 ndcMin = math.float3(float.PositiveInfinity);

            float3 ndc = TransferNDC(pos0);
            ndcMax = math.max(ndc, ndcMax);
            ndcMin = math.min(ndc, ndcMin);
            ndc = TransferNDC(pos1);
            ndcMax = math.max(ndc, ndcMax);
            ndcMin = math.min(ndc, ndcMin);
            ndc = TransferNDC(pos2);
            ndcMax = math.max(ndc, ndcMax);
            ndcMin = math.min(ndc, ndcMin);
            ndc = TransferNDC(pos3);
            ndcMax = math.max(ndc, ndcMax);
            ndcMin = math.min(ndc, ndcMin);
            ndc = TransferNDC(pos4);
            ndcMax = math.max(ndc, ndcMax);
            ndcMin = math.min(ndc, ndcMin);
            ndc = TransferNDC(pos5);
            ndcMax = math.max(ndc, ndcMax);
            ndcMin = math.min(ndc, ndcMin);
            ndc = TransferNDC(pos6);
            ndcMax = math.max(ndc, ndcMax);
            ndcMin = math.min(ndc, ndcMin);
            ndc = TransferNDC(pos7);
            ndcMax = math.max(ndc, ndcMax);
            ndcMin = math.min(ndc, ndcMin);


            //上一帧不在屏幕内
            float4 ndcXY = math.float4(ndcMin.xy, ndcMax.xy);
            float2 ndcZ = math.float2(ndcMin.z, ndcMax.z);
            Log($"2index:{index} {ndcXY} {ndcZ}");
            if (math.any(ndcXY < 0f) || math.any(ndcXY > 1f)
                                     || math.any(ndcZ < 0f) || math.any(ndcZ > 1f)
               )
                return false;
            float2 mipOSize = math.float2(mip0SizeVector.x, mip0SizeVector.y);
            float2 ndcSize = math.floor((ndcMax.xy - ndcMin.xy) * mipOSize);

            float raidus = math.max(ndcSize.x, ndcSize.y);
            int mip = Mathf.FloorToInt(math.log2(raidus));
            mip = math.clamp(mip, 0, mipMaxIndex);
            Log($"2.1 index:{index} mip:{mip}  raidus:{raidus} ndcMax:{ndcMax.xy} ndcMin:{ndcMin.xy} ndcSize:{ndcSize}");

            var mipLevelWidth = textureWidth / (int)Mathf.Pow(2, mip);
            var mipLevelHeight = textureHeight / (int)Mathf.Pow(2, mip);
            var mipSizeData = new Vector2Int(mipLevelWidth, mipLevelHeight); //mipLevelSizes[mip];
            int2 mipSize = math.int2(mipSizeData.x, mipSizeData.y);

            int2 minPx = (int2)(ndcMin.xy * mipSize.xy);
            int2 maxPx = (int2)(ndcMax.xy * mipSize.xy);

            int4 l_bt = math.int4(minPx.x, minPx.y, minPx.x, maxPx.y); // + offset.xyxy;
            int4 r_bt = math.int4(maxPx.x, minPx.y, maxPx.x, maxPx.y); //+ offset.xyxy;


            var starIndex = 0;
            var (tempWidth, tempHeight) = (textureWidth/2, textureHeight/2);
            for (int i = 1; i < mip; i++)
            {
                starIndex += tempWidth * tempHeight;
                tempWidth /= 2;
                tempHeight /= 2;
            }

            int2 index01 = l_bt.xz + l_bt.yw * mipLevelWidth;
            int2 index23 = r_bt.xz + r_bt.yw * mipLevelWidth;

            float d0 = mip == 0 ? depth0buffer[index01.x] : buffer[starIndex + index01.x]; //buffer[minPx.x + minPx.y * textureWidth];
            float d1 = mip == 0 ? depth0buffer[index01.y] : buffer[starIndex + index01.y]; //buffer[maxPx.x + maxPx.y * textureWidth];
            float d2 = mip == 0 ? depth0buffer[index23.x] : buffer[starIndex + index23.x];
            float d3 = mip == 0 ? depth0buffer[index23.y] : buffer[starIndex + index23.y];

            Log($"3index:{index} {l_bt} {r_bt} starIndex:{starIndex} {index01} {index23} {index} d0:{d0} d1:{d1} d2:{d2} d3:{d3}  ndc:{ndcMax.z}");
            if (usesReversedZBuffer)
            {
                float minDepth = math.min(math.min(math.min(d0, d1), d2), d3);
                Log($"5index:{index} {ndcMax.z} {minDepth} {Math.Round(ndcMax.z, 6)},{Math.Round(minDepth, 6)}");
                //精确到6位小数
                return Math.Round(ndcMax.z, 6) < Math.Round(minDepth, 6);
            }
            else
            {
                float maxDepth = math.max(math.max(math.max(d0, d1), d2), d3);
                Log($"6index:{index} {ndcMin.z} {maxDepth}  {Math.Round(maxDepth, 6)},{Math.Round(ndcMin.z, 6)}");
                return Math.Round(ndcMin.z, 6) > Math.Round(maxDepth, 6);
            }
        }

        private void Log(string str)
        {
            HizMgr.Log(str);
        }
    }
}