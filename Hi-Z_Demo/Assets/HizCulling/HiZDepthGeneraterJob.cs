using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Serialization;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    [BurstCompile]
    struct HiZDepthGeneraterJob : IJobParallelFor
    {
        [ReadOnly] 
        public NativeArray<float> srcDepthData; // 原始深度数据
        [WriteOnly]
        public NativeArray<float> destDepthData; // 降采样后的深度数据
        [NativeDisableParallelForRestriction]
        [WriteOnly] 
        public NativeArray<float> allDepthData;
        
        public int srcWidth; // 原始纹理的宽度
        public int srcHeight; // 原始纹理的高度
        public int startIndex;
        public bool usesReversedZBuffer;

        public void Execute(int index)
        {
            // 计算降采样纹理中当前像素的x和y坐标
            int dstX = index % (srcWidth / 2);
            int dstY = index / (srcWidth / 2);

            // 确定原始纹理中2x2区域的起始坐标
            int srcStartX = dstX * 2;
            int srcStartY = dstY * 2;
            
            float depth = usesReversedZBuffer ? float.MaxValue : float.MinValue;
            
            // 遍历2x2区域，找到最小深度值
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int srcIndex = (srcStartY + i) * srcWidth + (srcStartX + j);
                    // 确保索引在有效范围内
                    if (srcIndex < srcWidth * srcHeight)
                    {
                        if (usesReversedZBuffer)
                        {
                            depth = Mathf.Min(depth, srcDepthData[srcIndex]);
                        }
                        else
                        {
                            depth = Mathf.Max(depth, srcDepthData[srcIndex]);
                        }
                    }
                }
            }

            // 将找到的最小深度值写入到降采样纹理的对应位置
            // Debug.LogError($"width:{srcWidth} index:{index} length:{destDepthData.Length}");
            destDepthData[index] = depth;
            allDepthData[startIndex + index] = depth;
        }
    }
}