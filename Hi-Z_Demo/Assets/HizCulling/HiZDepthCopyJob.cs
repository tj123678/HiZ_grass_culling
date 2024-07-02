using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Serialization;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    [BurstCompile]
    struct HiZDepthCopyJob : IJobParallelFor
    {
        [ReadOnly] 
        public NativeArray<float> srcDepthData; // 原始深度数据
        [NativeDisableParallelForRestriction]
        [WriteOnly] 
        public NativeArray<float> allDepthData;
        public int multi;

        public void Execute(int index)
        {
            for (int i = 0; i < multi; i++)
            {
                var tempIndex = index * multi + i;
                allDepthData[tempIndex] = srcDepthData[tempIndex];
            }
        }
    }
}