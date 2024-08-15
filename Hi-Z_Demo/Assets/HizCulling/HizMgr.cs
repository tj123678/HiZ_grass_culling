using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    public class HizMgr : MonoBehaviour
    {
        public static HizMgr Instance { get; private set; }

        [Tooltip("打开debug绘制")] public bool IsDrawDebug = false;

        private string m_DepthRTGeneratePassTag = "HiZDepthGeneratePass";

        private int m_Clearkernel;
        private int m_Cullkernel;

        private Matrix4x4 m_lastVPs;
        private Matrix4x4 m_Vps;
        private RenderTexture m_hizDepthRT;
        private RenderTexture m_backupHizDepthRT;
        private int m_hizDepthRTMip;

        private ComputeBuffer m_clusterBuffer;
        private ComputeBuffer m_drawArgsBuffer;
        private ComputeBuffer m_resultBuffer;
        private bool m_bEnable = false;

        private Material m_genHiZRTMat;
        private int mipWidthSize;
        public Camera CullCamera => cullCamera;
        private Camera cullCamera;

        private List<MeshRenderer> tempOccludes;
        private List<Bounds> tempBounds;

        private int maxMipLevel = 7;

        private void Awake()
        {
            Application.targetFrameRate = 30;
            Instance = this;
            isOpenGl = IsOpenGL();
            m_genHiZRTMat = new Material(Shader.Find("HZB/HZBBuild"));
            cullCamera = Camera.main;
            tempBounds = new List<Bounds>();
            tempOccludes = new List<MeshRenderer>();
            aabb = gameObject.AddComponent<AABBMgr>();
        }

        void OnGUI()
        {
            GUILayout.BeginVertical();
            // 在垂直布局中添加20像素的垂直空间
            GUILayout.Space(50);
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal(); // 开始一个水平布局
            GUILayout.Space(10); // 填充空白，将按钮推向右侧
            if (GUILayout.Button("是否剔除：" + (isCull ? "on" : "off"), GUILayout.ExpandWidth(true), GUILayout.Height(100)))
            {
                isCull = !isCull;
                if (!isCull)
                {
                    aabb.ShowAll();
                }
            }

            GUILayout.EndHorizontal(); // 结束水平布局
        }

        #region 生成HizMinimap

        public void ExecuteDepthGenerate(ScriptableRenderContext context, RenderPassEvent passEvent,
            ref RenderingData renderingData)
        {
            EnsureResourceReady(renderingData);
            m_Vps = GL.GetGPUProjectionMatrix(cullCamera.projectionMatrix, false) * cullCamera.worldToCameraMatrix;
            Log(
                $"projectionMatrix:\n{cullCamera.projectionMatrix}  worldToCameraMatrixL:\n{cullCamera.worldToCameraMatrix}");
            CommandBuffer cmd = CommandBufferPool.Get(m_DepthRTGeneratePassTag);

            cmd.SetGlobalTexture("_DepthTexture", renderingData.cameraData.renderer.cameraDepthTargetHandle);
            cmd.SetGlobalVector("_InvSize", new Vector4(1f / mipWidthSize, 1f / mipWidthSize, 0, 0));
            cmd.Blit(renderingData.cameraData.renderer.cameraDepthTargetHandle, m_hizDepthRT, m_genHiZRTMat);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void EnsureResourceReady(RenderingData renderingData)
        {
            if (m_hizDepthRT == null)
                CreateHiZDepthRT(renderingData.cameraData.camera.pixelWidth);
        }

        public void CreateHiZDepthRT(int screenWidth)
        {
            if (m_hizDepthRT == null)
            {
                (int w, int mip) = GetDepthRTWidthFromScreen(screenWidth);
                mipWidthSize = w;
                var depthRT = new RenderTexture(w, w, 1, RenderTextureFormat.RFloat, mip);
                depthRT.name = "hizDepthRT";
                depthRT.useMipMap = false;
                depthRT.autoGenerateMips = false;
                // depthRT.enableRandomWrite = true;
                depthRT.wrapMode = TextureWrapMode.Clamp;
                depthRT.filterMode = FilterMode.Point;
                depthRT.Create();
                m_hizDepthRT = depthRT;
            }

            if (m_backupHizDepthRT == null)
            {
                (int w, int mip) = GetDepthRTWidthFromScreen(screenWidth);
                mipWidthSize = w;
                var depthRT = new RenderTexture(w, w, 0, RenderTextureFormat.RFloat, mip);
                depthRT.name = "BackupHizDepthRT";
                depthRT.useMipMap = false;
                depthRT.autoGenerateMips = false;
                // depthRT.enableRandomWrite = true;
                depthRT.wrapMode = TextureWrapMode.Clamp;
                depthRT.filterMode = FilterMode.Point;
                depthRT.Create();
                m_backupHizDepthRT = depthRT;
            }
        }

        private (int, int) GetDepthRTWidthFromScreen(int screenWidth)
        {
            return (512, 0);
        }

        #endregion

        #region 遮挡剔除

        private bool isRequest;
        private AABBMgr aabb;
        private bool isOpenGl;
        private bool isCull;

        public void ExecuteCull(ScriptableRenderContext context, RenderPassEvent passEvent,
            ref RenderingData renderingData)
        {
            if (isCull)
            {
                if (isRequest) return;
                //当前mipmap的大小
                var mipIndex = 0;
                var width = m_hizDepthRT.width;
                var height = m_hizDepthRT.height;
                Log(
                    $"size mipCount:{m_hizDepthRT.mipmapCount} originWidth:{m_hizDepthRT.width} width:{width}  height:{height}  result:{width * height}");
                CPUCulling(context, mipIndex, width, height);
            }
        }

        private void CPUCulling(ScriptableRenderContext context, int mipIndex, int width, int height)
        {
            isRequest = true;
            m_lastVPs = m_Vps;
            CommandBuffer cmd = CommandBufferPool.Get(m_DepthRTGeneratePassTag);
            // frameCount = Time.frameCount;
            NativeArray<float> pixels = new NativeArray<float>(width * height, Allocator.Persistent);
            cmd.RequestAsyncReadbackIntoNativeArray(ref pixels, m_hizDepthRT, mipIndex, OnReadBack);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            void OnReadBack(AsyncGPUReadbackRequest request)
            {
                if (isCull && request.done && !request.hasError)
                {
                    try
                    {
                        aabb.GetAABBInfos(tempOccludes, tempBounds);
                        Task.Run(RunTask);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                        throw;
                    }
                }
                else
                {
                    isRequest = false;
                }
            }

            void RunTask()
            {
                Profiler.BeginSample("hiz start");
                
                NativeArray<Bounds> aabbs = new NativeArray<Bounds>(tempBounds.Count, Allocator.TempJob);
                for (int i = 0; i < tempBounds.Count; i++)
                {
                    aabbs[i] = tempBounds[i];
                }

                NativeArray<bool> result = new NativeArray<bool>(tempBounds.Count, Allocator.TempJob);
                var hizDepthData = new float[GetHizSize(maxMipLevel, width / 2, height / 2)];
                Profiler.EndSample();
                Profiler.BeginSample("hiz HiZDepthGenerater");
                HiZDepthGenerater(pixels.ToArray(), width, height, maxMipLevel, ref hizDepthData);

                Log("hizDepthData size:" + hizDepthData.Length);

                bool usesReversedZBuffe = SystemInfo.usesReversedZBuffer;
                Vector2Int mip0SizeVector = new Vector2Int(width, height);
                var job = new HizCullJob()
                {
                    result = result,
                    aabbs = aabbs,

                    depth0buffer = pixels,
                    // buffer = hizDepthData,

                    textureWidth = width,
                    textureHeight = height,
                    isOpenGl = isOpenGl,
                    usesReversedZBuffer = usesReversedZBuffe,
                    world2HZB = m_lastVPs,
                    mip0SizeVector = mip0SizeVector,
                    dataVail = true,
                    mipMaxIndex = maxMipLevel,
                    srcLog2 = (int)Math.Log(width, 2)
                };
                Profiler.EndSample();
                Profiler.BeginSample("hiz job");
                // 调度作业
                JobHandle handle = job.Schedule(result.Length, 64);
                handle.Complete();
                Profiler.EndSample();
                Profiler.BeginSample("hiz UpdateRender");
                aabb.UpdateRender(tempOccludes, result.ToArray());
                Profiler.EndSample();

                //清理
                result.Dispose();
                aabbs.Dispose();
                // hizDepthData.Dispose();
                pixels.Dispose();
                isRequest = false;
            }
        }

        private bool IsOpenGL()
        {
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.OpenGLCore:
                case GraphicsDeviceType.OpenGLES2:
                case GraphicsDeviceType.OpenGLES3:
                    return true;
            }

            return false;
        }

        #endregion

        #region 利用cpu生成hiz

        private void HiZDepthGenerater(float[] srcDepthData, int srcWidth, int srcHeight, int maxMipLevel,
            ref float[] hizDepthData)
        {
            // HiZDepthCopyJob(srcDepthData, ref hizDepthData);
            var (tempWidth, tempHeight) = (srcWidth, srcHeight);
            Dictionary<int,float[]> destDatas = DictionaryPool<int, float[]>.Get();
            destDatas.Add(0, srcDepthData);
            for (int i = 1; i <= maxMipLevel; i++)
            {
                var dstDepthData = new float[tempWidth / 2 * tempHeight / 2];
                HiZDepthGeneraterJob(destDatas[i - 1], tempWidth, tempHeight, i, ref dstDepthData, ref hizDepthData);
                destDatas.Add(i, dstDepthData);

                tempWidth /= 2;
                tempHeight /= 2;
            }

            for (int i = 1; i <= maxMipLevel; i++)
            {
                destDatas[i] = null;
            }

            DictionaryPool<int, float[]>.Release(destDatas);
        }

        private void AddHizData(NativeArray<float> srcdata, NativeArray<float> dstData, ref int starIndex)
        {
            for (int i = 0; i < dstData.Length; i++)
            {
                srcdata[starIndex + i] = dstData[i];
            }

            starIndex += dstData.Length;
        }

        private int GetHizSize(int levels, int width, int height)
        {
            var size = 0;
            for (int i = 1; i <= levels; i++)
            {
                size += width * height;
                width /= 2;
                height /= 2;
            }

            return size;
        }

        private void HiZDepthCopyJob(NativeArray<float> srcDepthData, ref NativeArray<float> allDepthData)
        {
            var multi = (int)Mathf.Pow(2, 5);
            // 创建Job
            var job = new HiZDepthCopyJob()
            {
                srcDepthData = srcDepthData,
                allDepthData = allDepthData,
                multi = multi
            };

            // 调度Job
            JobHandle jobHandle = job.Schedule(srcDepthData.Length / multi, 32);

            // 等待Job完成
            jobHandle.Complete();
        }

        private HiZDepthGeneraterJob hiZDepthGeneraterJob;
        private void HiZDepthGeneraterJob(float[] srcDepthData, int srcWidth, int srcHeight, int mapLevel,
            ref float[] dstDepthData,
            ref float[] allDepthData)
        {
            var startIndex = 0;
            var (tempWidth, tempHeight) = (mipWidthSize / 2, mipWidthSize / 2);
            for (int i = 1; i < mapLevel; i++)
            {
                startIndex += tempWidth * tempHeight;
                tempWidth /= 2;
                tempHeight /= 2;
            }

            // Debug.LogError($"width:{srcWidth} star:{startIndex} mapLevel:{mapLevel} all:{allDepthData.Length} dst:{dstDepthData.Length}");
            // 创建Job
            hiZDepthGeneraterJob ??= new HiZDepthGeneraterJob();
            {
                hiZDepthGeneraterJob.srcDepthData = srcDepthData;
                hiZDepthGeneraterJob.destDepthData = dstDepthData;
                hiZDepthGeneraterJob.srcWidth = srcWidth;
                hiZDepthGeneraterJob.srcHeight = srcHeight;
                hiZDepthGeneraterJob.startIndex = startIndex;
                hiZDepthGeneraterJob.allDepthData = allDepthData;
                // hiZDepthGeneraterJob.usesReversedZBuffer = SystemInfo.usesReversedZBuffer;
            };
            hiZDepthGeneraterJob.Execute();
        }

        #endregion

        public static void Log(string str)
        {
            // Debug.Log($"hiz cull: {str}");
        }
    }
}

public class HiZDepthGeneraterJob
{
    public float[] srcDepthData; // 原始深度数据
    public float[] destDepthData; // 降采样后的深度数据
    public float[] allDepthData;
        
    public int srcWidth; // 原始纹理的宽度
    public int srcHeight; // 原始纹理的高度
    public int startIndex;
    public bool usesReversedZBuffer;

    public void Execute()
    {
        for (int index = 0; index < destDepthData.Length; index++)
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


  public class HizCullJob
    {
        public bool[] result;
        public Bounds[]aabbs;
        public float[] depth0buffer;
        public float[] buffer;
        // [ReadOnly]
        // public NativeArray<Vector2Int> mipLevelOffsets;
        // [ReadOnly]
        // public NativeArray<Vector2Int> mipLevelSizes;

        public int textureWidth;
        public int textureHeight;
        public bool isOpenGl;
        public bool usesReversedZBuffer;
        public Matrix4x4 world2HZB;
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

        public Vector3 TransferNDC(Vector3 pos)
        {
            Vector4 ndc = world2HZB * new Vector4(pos.x, pos.y, pos.z, 1f);
            ndc.x /= ndc.w;
            ndc.y /= ndc.w;
            ndc.z /= ndc.w;
            ndc.x = ndc.x * 0.5f + 0.5f;
            ndc.y = ndc.y * 0.5f + 0.5f;
            if (isOpenGl)
            {
                ndc.z = ndc.z * 0.5f + 0.5f;
            }
            return new Vector3(ndc.x, ndc.y, ndc.z);
        }

        public bool IsHizCulled(int index)
        {
            var bound = aabbs[index];
            Vector3 bmin = bound.min;
            Vector3 bmax = bound.max;
            Log($"1.5index:{index} bmin:{bmin} bmax:{bmax}");
            Vector3 pos0 = new Vector3(bmin.x, bmin.y, bmin.z);
            Vector3 pos1 = new Vector3(bmin.x, bmin.y, bmax.z);
            Vector3 pos2 = new Vector3(bmin.x, bmax.y, bmin.z);
            Vector3 pos3 = new Vector3(bmax.x, bmin.y, bmin.z);
            Vector3 pos4 = new Vector3(bmax.x, bmax.y, bmin.z);
            Vector3 pos5 = new Vector3(bmax.x, bmin.y, bmax.z);
            Vector3 pos6 = new Vector3(bmin.x, bmax.y, bmax.z);
            Vector3 pos7 = new Vector3(bmax.x, bmax.y, bmax.z);
            Vector3 ndcMax = Vector3.one * float.NegativeInfinity;
            Vector3 ndcMin = Vector3.one * float.PositiveInfinity;

            Vector3 ndc = TransferNDC(pos0);
            ndcMax = Vector3.Max(ndc, ndcMax);
            ndcMin = Vector3.Min(ndc, ndcMin);
            ndc = TransferNDC(pos1);
            ndcMax = Vector3.Max(ndc, ndcMax);
            ndcMin = Vector3.Min(ndc, ndcMin);
            ndc = TransferNDC(pos2);
            ndcMax = Vector3.Max(ndc, ndcMax);
            ndcMin = Vector3.Min(ndc, ndcMin);
            ndc = TransferNDC(pos3);
            ndcMax = Vector3.Max(ndc, ndcMax);
            ndcMin = Vector3.Min(ndc, ndcMin);
            ndc = TransferNDC(pos4);
            ndcMax = Vector3.Max(ndc, ndcMax);
            ndcMin = Vector3.Min(ndc, ndcMin);
            ndc = TransferNDC(pos5);
            ndcMax = Vector3.Max(ndc, ndcMax);
            ndcMin = Vector3.Min(ndc, ndcMin);
            ndc = TransferNDC(pos6);
            ndcMax = Vector3.Max(ndc, ndcMax);
            ndcMin = Vector3.Min(ndc, ndcMin);
            ndc = TransferNDC(pos7);
            ndcMax = Vector3.Max(ndc, ndcMax);
            ndcMin = Vector3.Min(ndc, ndcMin);


            //上一帧不在屏幕内
            Vector4 ndcXY = new Vector4(ndcMin.x, ndcMin.y, ndcMax.x, ndcMax.y);
            Vector2 ndcZ = new Vector2(ndcMin.z, ndcMax.z);
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