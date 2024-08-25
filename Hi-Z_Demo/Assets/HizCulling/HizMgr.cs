using System;
using System.Collections.Generic;
using TMPro;
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

        public TMP_Text TMPText;
        private TreeComponent treeComponent;

        private void Awake()
        {
            Debug.Log("场景中所有物体数量：" + GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length);
            Instance = this;
            isOpenGl = IsOpenGL();
            m_genHiZRTMat = new Material(Shader.Find("HZB/HZBBuild"));
            cullCamera = Camera.main;
            tempBounds = new List<Bounds>();
            tempOccludes = new List<MeshRenderer>();
            aabb = gameObject.AddComponent<AABBMgr>();
            treeComponent = gameObject.AddComponent<TreeComponent>();
            treeComponent.Init(Camera.main, new Vector3(2000, 1000, 2000));
            TMPText.text = ArabicSupport.Fix("<size=24><color=#A3A5A3>(إذا شعرت بالدوار، الرجاء إيقاف هذا الإعداد)</color></size>ضبط وضوح آلي");
        }
        
        public void AddTreeNode(GameObject obj,HizOccludee hizOccludee)
        {
            var pos = obj.transform.position;
            treeComponent.tree.Instance(new ObjData(hizOccludee, pos, obj.transform.eulerAngles));
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

        public void ExecuteDepthGenerate(ScriptableRenderContext context, RenderPassEvent passEvent, ref RenderingData renderingData)
        {
            EnsureResourceReady(renderingData);
            m_Vps = GL.GetGPUProjectionMatrix(cullCamera.projectionMatrix, false) * cullCamera.worldToCameraMatrix;
            Log($"projectionMatrix:\n{cullCamera.projectionMatrix}  worldToCameraMatrixL:\n{cullCamera.worldToCameraMatrix}");
            CommandBuffer cmd = CommandBufferPool.Get(m_DepthRTGeneratePassTag);

            cmd.SetGlobalTexture("_DepthTexture", renderingData.cameraData.renderer.cameraDepthTargetHandle);
            cmd.SetGlobalVector("_InvSize", new Vector4(1f / mipWidthSize, 1f / mipWidthSize, 0, 0));
            // var depthDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            // depthDescriptor.bindMS = false;// depthDescriptor.msaaSamples > 1 && (SystemInfo.supportsMultisampledTextures != 0);
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
                depthRT.bindTextureMS = true;
                depthRT.antiAliasing = 2;
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

        public void ExecuteCull(ScriptableRenderContext context, RenderPassEvent passEvent, ref RenderingData renderingData)
        {
            if (isCull)
            {
                if (isRequest) return;
                //当前mipmap的大小
                var mipIndex = 0;
                var width = m_hizDepthRT.width;
                var height = m_hizDepthRT.height;
                Log($"size mipCount:{m_hizDepthRT.mipmapCount} originWidth:{m_hizDepthRT.width} width:{width}  height:{height}  result:{width * height}");
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
                    Profiler.BeginSample("hiz start");
                    treeComponent.Reflesh();
                    aabb.GetAABBInfos(tempOccludes, tempBounds);
                    NativeArray<Bounds> aabbs = new NativeArray<Bounds>(tempBounds.Count, Allocator.TempJob);
                    for (int i = 0; i < tempBounds.Count; i++)
                    {
                        aabbs[i] = tempBounds[i];
                    }

                    NativeArray<bool> result = new NativeArray<bool>(tempBounds.Count, Allocator.TempJob);
                    var hizDepthData = new NativeArray<float>(GetHizSize(maxMipLevel, width / 2, height / 2), Allocator.TempJob);
                    Profiler.EndSample();
                    Profiler.BeginSample("hiz HiZDepthGenerater");
                    HiZDepthGenerater(pixels, width, height, maxMipLevel, ref hizDepthData);

                    Log("hizDepthData size:" + hizDepthData.Length);

                    bool usesReversedZBuffe = SystemInfo.usesReversedZBuffer;
                    Vector2Int mip0SizeVector = new Vector2Int(width, height);
                    var job = new HizCullJob()
                    {
                        result = result,
                        aabbs = aabbs,

                        depth0buffer = pixels,
                        buffer = hizDepthData,

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
                    hizDepthData.Dispose();
                    pixels.Dispose();
                }

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

        private void HiZDepthGenerater(NativeArray<float> srcDepthData, int srcWidth, int srcHeight, int maxMipLevel, ref NativeArray<float> hizDepthData)
        {
            // HiZDepthCopyJob(srcDepthData, ref hizDepthData);
            var (tempWidth, tempHeight) = (srcWidth, srcHeight);
            Dictionary<int, NativeArray<float>> destDatas = DictionaryPool<int, NativeArray<float>>.Get();
            destDatas.Add(0, srcDepthData);
            for (int i = 1; i <= maxMipLevel; i++)
            {
                var dstDepthData = new NativeArray<float>(tempWidth / 2 * tempHeight / 2, Allocator.TempJob);
                HiZDepthGeneraterJob(destDatas[i - 1], tempWidth, tempHeight, i, ref dstDepthData, ref hizDepthData);
                destDatas.Add(i, dstDepthData);

                tempWidth /= 2;
                tempHeight /= 2;
            }

            for (int i = 1; i <= maxMipLevel; i++)
            {
                destDatas[i].Dispose();
            }

            DictionaryPool<int, NativeArray<float>>.Release(destDatas);
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

        private void HiZDepthGeneraterJob(NativeArray<float> srcDepthData, int srcWidth, int srcHeight, int mapLevel, ref NativeArray<float> dstDepthData,
            ref NativeArray<float> allDepthData)
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
            var job = new HiZDepthGeneraterJob
            {
                srcDepthData = srcDepthData,
                destDepthData = dstDepthData,
                srcWidth = srcWidth,
                srcHeight = srcHeight,
                startIndex = startIndex,
                allDepthData = allDepthData,
                usesReversedZBuffer = SystemInfo.usesReversedZBuffer
            };

            // 调度Job
            JobHandle jobHandle = job.Schedule(dstDepthData.Length, 64);

            // 等待Job完成
            jobHandle.Complete();
        }

        #endregion

        public static void Log(string str)
        {
            // Debug.Log($"hiz cull: {str}");
        }
    }
}