using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using JobSystem;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;

public class HizMgr : MonoBehaviour
{
    public static HizMgr Instance;

    private string m_DepthRTGeneratePassTag = "HiZDepthGeneratePass";
    private ProfilingSampler m_DepthRTGeneratePassSampler;

    private int m_Clearkernel;
    private int m_Cullkernel;

    public ComputeShader m_cullCS;

    private Matrix4x4 m_lastVPs;
    private Matrix4x4 m_Vps;
    private RenderTexture m_hizDepthRT;
    private RenderTexture m_backupHizDepthRT;
    private int m_hizDepthRTWidth;
    private int m_hizDepthRTMip;

    private ComputeBuffer m_clusterBuffer;
    private ComputeBuffer m_drawArgsBuffer;
    private ComputeBuffer m_resultBuffer;
    private bool m_bEnable = false;

    private int m_SourceTexID = Shader.PropertyToID("_SourceTex");
    private int m_DestTexId = Shader.PropertyToID("_DestTex");
    private int m_DepthRTSize = Shader.PropertyToID("_DepthRTSize");

    private Material m_genHiZRTMat;

    private ComputeShader m_generateMipmapCS;
    private int m_genMipmapKernel;
    private int mipWidthSize;

    private void Start()
    {
        Instance = this;
        aabb = FindObjectOfType<AABBMgr>();
        isOpenGl = IsOpenGL();
        Application.targetFrameRate = 30;
        
        // m_hizDepthRT = new RenderTexture(HizCulling.MapSize, HizCulling.MapSize, 0, RenderTextureFormat.RFloat);
        // m_hizDepthRT.useMipMap = true;
        // m_hizDepthRT.autoGenerateMips = false;
        // m_hizDepthRT.enableRandomWrite = true;
        // m_hizDepthRT.wrapMode = TextureWrapMode.Clamp;
        // m_hizDepthRT.filterMode = FilterMode.Point;
        // m_hizDepthRT.Create();
        m_genHiZRTMat = new Material(Shader.Find("Hidden/GenerateDepthRT"));
        hzbMat = new Material(hzbShader);
    }
    public float mouseSensitivity = 1f;
    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            //摄像机转动
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;

            Camera.main.transform.Rotate(0, -mouseX, 0);
        }

    }

    void OnGUI()
    {
        GUILayout.BeginVertical();
        // 在垂直布局中添加20像素的垂直空间
        GUILayout.Space(50);
        GUILayout.EndVertical();


        GUILayout.BeginHorizontal(); // 开始一个水平布局
        GUILayout.Space(500); // 填充空白，将按钮推向右侧
        if (GUILayout.Button("是否剔除：" + (isCull ? "on" : "off"), GUILayout.ExpandWidth(true), GUILayout.Height(100)))
        {
            aabb.UpdateInfo();
            isCull = !isCull;
            isCulled = false;
        }

        GUILayout.Space(500);
        if (GUILayout.Button("显示所有", GUILayout.ExpandWidth(true), GUILayout.Height(100)))
        {
            isCull = false;
            aabb.ShowAll();
        }

        GUILayout.EndHorizontal(); // 结束水平布局

        float deltaTime = Time.unscaledDeltaTime;
        long lastFrameTimeNs = System.DateTime.Now.Ticks * 100; // 转换为纳秒
        long currentFrameTimeNs = lastFrameTimeNs + (long)(deltaTime * 1000000000);
        float fps = 1.0f / ((currentFrameTimeNs - lastFrameTimeNs) / 1000000000.0f);

        // 设置要显示的文本内容
        string fpsText = "FPS: " + string.Format("{0:F2}", fps);

        // 设置文本的样式，这里只是设置了一些基本样式，你可以根据需要自定义
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white; // 设置文本颜色为白色

        // 计算文本的矩形位置，这里我们将其放在屏幕的左上角
        Rect rect = new Rect(200, 10, 200, 40);

        // 在屏幕上绘制文本
        GUI.Label(rect, fpsText, style);
    }

    #region 生成HizMinimap
    public Shader hzbShader;
    private Material hzbMat;
    int ID_DepthTexture;
    int ID_InvSize;

    // public void ExecuteDepthGenerate(ScriptableRenderContext context,RenderPassEvent rpe,ref RenderingData renderingData)
    // {
    //     int w = m_hizDepthRT.width;
    //     int h = m_hizDepthRT.height;
    //     int level = 0;
    //
    //     RenderTexture lastRt = null;
    //     if (ID_DepthTexture == 0)
    //     {
    //         ID_DepthTexture = Shader.PropertyToID("_DepthTexture");
    //         ID_InvSize = Shader.PropertyToID("_InvSize");
    //     }
    //     RenderTexture tempRT;
    //     while (h > 8)
    //     {
    //         hzbMat.SetVector(ID_InvSize, new Vector4(1.0f / w, 1.0f / h, 0, 0));
    //         tempRT = RenderTexture.GetTemporary(w, h, 0, m_hizDepthRT.format);
    //         tempRT.filterMode = FilterMode.Point;
    //         if (lastRt == null)
    //         {
    //             // copy depth to hiz depth RT
    //             Graphics.Blit(renderingData.cameraData.renderer.cameraDepthTargetHandle.rt, tempRT, m_genHiZRTMat);
    //         }
    //         else
    //         {
    //             hzbMat.SetTexture(ID_DepthTexture, lastRt);
    //             Graphics.Blit(null, tempRT, hzbMat);
    //             RenderTexture.ReleaseTemporary(lastRt);
    //         }
    //         Graphics.CopyTexture(tempRT, 0, 0, m_hizDepthRT, 0, level);
    //         level++;
    //         lastRt = tempRT;
    //         w /= 2;
    //         h /= 2;
    //
    //     }
    //     RenderTexture.ReleaseTemporary(lastRt);
    // }
    //

    public void ExecuteDepthGenerate(ScriptableRenderContext context, RenderPassEvent passEvent, ref RenderingData renderingData)
    {
        // if (isRequest) return;
        EnsureResourceReady(renderingData);
    
        // if (m_DepthRTGeneratePassSampler == null)
        //     m_DepthRTGeneratePassSampler = new ProfilingSampler(m_DepthRTGeneratePassTag);
    
        m_Vps = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, true) * Camera.main.worldToCameraMatrix;
    
        CommandBuffer cmd = CommandBufferPool.Get(m_DepthRTGeneratePassTag);
        // using (new ProfilingScope(cmd, m_DepthRTGeneratePassSampler))
        // {
            // copy depth to hiz depth RT
            // cmd.Blit(Texture2D.blackTexture, m_hizDepthRT, m_genHiZRTMat);
            cmd.Blit(renderingData.cameraData.renderer.cameraDepthTargetHandle, m_hizDepthRT);
    
            float w = m_hizDepthRTWidth;
            float h = m_hizDepthRTWidth;
            for (int i = 1; i < m_hizDepthRTMip; ++i)
            {
                w = Mathf.Max(1, w / 2);
                h = Mathf.Max(1, h / 2);
                cmd.SetComputeTextureParam(m_generateMipmapCS, m_genMipmapKernel, m_SourceTexID, m_hizDepthRT, i - 1);
                cmd.SetComputeTextureParam(m_generateMipmapCS, m_genMipmapKernel, m_DestTexId, m_hizDepthRT, i);
                cmd.SetComputeVectorParam(m_generateMipmapCS, m_DepthRTSize, new Vector4(w, h, 0f, 0f));
    
                int x, y;
                x = Mathf.CeilToInt(w / 8f);
                y = Mathf.CeilToInt(h / 8f);
                cmd.DispatchCompute(m_generateMipmapCS, 0, x, y, 1);
                
            }
        // }
    
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void EnsureResourceReady(RenderingData renderingData)
    {
        // if (!m_cullCS)
        // {
        //     m_cullCS = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Scripts/HiZ/Res/GpuFrustumCulling.compute");
        //     m_Clearkernel = m_cullCS.FindKernel("Clear");
        //     m_Cullkernel = m_cullCS.FindKernel("Cull");
        // }

        if (m_hizDepthRT == null)
            CreateHiZDepthRT(renderingData.cameraData.camera.pixelWidth);

        if (m_genHiZRTMat == null)
            m_genHiZRTMat = new Material(Shader.Find("Hidden/GenerateDepthRT"));

        if (m_generateMipmapCS == null)
        {
            m_generateMipmapCS = Resources.Load<ComputeShader>("GenerateMipmap");
            m_genMipmapKernel = m_generateMipmapCS.FindKernel("GenMip");
        }

        // UpdateHiZDepthRTWidth(renderingData.cameraData.camera.pixelWidth);
    }

    public void CreateHiZDepthRT(int screenWidth)
    {
        if (m_hizDepthRT == null)
        {
            (int w, int mip) = GetDepthRTWidthFromScreen(screenWidth);

            m_hizDepthRTWidth = w;
            m_hizDepthRTMip = mip;

            var depthRT = new RenderTexture(w, w, 0, RenderTextureFormat.RFloat, mip);
            depthRT.name = "hizDepthRT";
            depthRT.useMipMap = true;
            depthRT.autoGenerateMips = false;
            depthRT.enableRandomWrite = true;
            depthRT.wrapMode = TextureWrapMode.Clamp;
            depthRT.filterMode = FilterMode.Point;
            depthRT.Create();
            m_hizDepthRT = depthRT;
            Debug.LogError($"CreateHiZDepthRT:{m_hizDepthRTWidth} mip:{mip}");
        }

        if (m_backupHizDepthRT == null)
        {
            (int w, int mip) = GetDepthRTWidthFromScreen(screenWidth);
            var depthRT = new RenderTexture(w, w, 0, RenderTextureFormat.RFloat, mip);
            depthRT.name = "BackupHizDepthRT";
            depthRT.useMipMap = true;
            depthRT.autoGenerateMips = false;
            depthRT.enableRandomWrite = true;
            depthRT.wrapMode = TextureWrapMode.Clamp;
            depthRT.filterMode = FilterMode.Point;
            depthRT.Create();
            m_backupHizDepthRT = depthRT;
        }
    }

    public void UpdateHiZDepthRTWidth(int screenWidth)
    {
        (int width, int mip) = GetDepthRTWidthFromScreen(screenWidth);

        if (width != m_hizDepthRTWidth)
        {
            m_hizDepthRTWidth = width;
            m_hizDepthRTMip = mip;

            m_hizDepthRT.Release();
            m_hizDepthRT = null;
            CreateHiZDepthRT(screenWidth);
        }

        Debug.LogError($"UpdateHiZDepthRTWidth:{width} mip:{mip}");
    }

    private (int, int) GetDepthRTWidthFromScreen(int screenWidth)
    {
        if (screenWidth >= 2048)
        {
            return (256, 5);
            return (1024, 10);
        }
        else if (screenWidth >= 1024)
        {
            return (256, 5);
            return (512, 9);
        }
        else
        {
            return (256, 8);
        }
    }

    #endregion

    #region 遮挡剔除

    private bool isRequest;
    private AABBMgr aabb;
    private bool isOpenGl;
    private bool isCull;
    private bool isCulled;
    public static int MapSize = 256;
    // private RenderTexture renderTexture;
    public SpriteRenderer sprite;
    public void ExecuteCull(ScriptableRenderContext context, RenderPassEvent passEvent, ref RenderingData renderingData)
    {
        if (isCull && !isCulled)
        {
            if (isRequest) return;
            //当前mipmap的大小
            var mipIndex = 2;
            var width = m_hizDepthRT.width / (1 << mipIndex);
            var height= m_hizDepthRT.height / (1 << mipIndex);
            Log($"size mipCount:{m_hizDepthRT.mipmapCount} originWidth:{m_hizDepthRT.width} width:{width}  height:{height}  result:{width * height}");
            // CommandBuffer cmd = CommandBufferPool.Get(m_DepthRTGeneratePassTag);
            // if (renderTexture == null)
            // {
            //     renderTexture = new RenderTexture(m_hizDepthRT.width, m_hizDepthRT.height, 0, m_hizDepthRT.graphicsFormat); //m_hizDepthRT;
            //     renderTexture.useMipMap = true;
            //     renderTexture.autoGenerateMips = false;
            // }
            //
            // // 复制Mipmap
            // for (int mipLevel = 0; mipLevel < m_hizDepthRT.descriptor.mipCount; mipLevel++)
            // {
            //     cmd.CopyTexture(m_hizDepthRT, 0, mipLevel, renderTexture, 0, mipLevel);
            // }
            // Debug.LogError("m_hizDepthRT size:" + m_hizDepthRT.width + " " + renderTexture.width);
            // // 确保RenderTexture是活动的
            // RenderTexture.active = renderTexture;
            // context.ExecuteCommandBuffer(cmd);
            // CommandBufferPool.Release(cmd);

            CPUCulling(context, mipIndex, width, height);
            // isCulled = true;
        }
    }
    private void CPUCulling(ScriptableRenderContext context,int mipIndex,int width,int height)
    {
        isRequest = true;
        var (_, _, bounds) = aabb.GetAABBInfo();
        NativeArray<bool> result = new NativeArray<bool>(bounds.Length, Allocator.TempJob);
        NativeArray<Bounds> aabbs = new NativeArray<Bounds>(bounds.Length, Allocator.TempJob);
        NativeArray<float> buffer = new NativeArray<float>(width * height, Allocator.TempJob);

        for (int i = 0; i < bounds.Length; i++)
        {
            aabbs[i] = bounds[i];

        }
        m_lastVPs = m_Vps;
        CommandBuffer cmd = CommandBufferPool.Get(m_DepthRTGeneratePassTag);
        // RenderTexture.active = m_hizDepthRT;
        // 复制Mipmap
        for (int mipLevel = 0; mipLevel < m_hizDepthRT.descriptor.mipCount; mipLevel++)
        {
            cmd.CopyTexture(m_hizDepthRT, 0, mipLevel, m_backupHizDepthRT, 0, mipLevel);
        }
        // frameCount = Time.frameCount;
        cmd.RequestAsyncReadback(m_hizDepthRT, mipIndex, OnReadBack);
        
        void OnReadBack(AsyncGPUReadbackRequest request)
        {
            if (request.done && !request.hasError)
            {
                float[] pixels = request.GetData<float>().ToArray();
                var spriteWith = sprite.sprite.texture.width;
                Color[] colors = new Color[pixels.Length];
                if (spriteWith * spriteWith == pixels.Length)
                {
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        colors[i] = new Color(pixels[i], 0, 0, 1);
                    }
                    sprite.sprite.texture.SetPixels(colors);
                    sprite.sprite.texture.Apply();
                }
                else
                {
                    Debug.LogError($"sprite size error sp:{spriteWith * spriteWith} render:{pixels.Length}  ({width},{height}) miplevel:{mipIndex}");
                }
                
                
                Log("pixels size:" + pixels.Length);
                int logCount = 0;
                // var list = ListPool<int>.Get();
                for (int i = 0; i < pixels.Length; i++)
                {
                    buffer[i] = pixels[i];
                    if (buffer[i] > 0 && logCount < 10)
                    {
                        Log($"depth unity:{i} {buffer[i]}");
                        logCount++;
                        // if(i<3) list.Add(i);
                    }
                }

                if (logCount == 0)
                {
                    Log("Null");
                }
                // list.Sort();
                // var sb = new StringBuilder();
                // list.ForEach(id => sb.Append(id + ","));
                // Log("depth total:" + sb);
                // sb.Clear();
                // ListPool<int>.Release(list);

                // Log("center depth:"+buffer[32768]);
                // Debug.Log($"frame count:{Time.frameCount - frameCount}");
        
                bool usesReversedZBuffe = SystemInfo.usesReversedZBuffer;

                Vector2Int mip0SizeVector = new Vector2Int(width, height);
                var job = new HizCullJob()
                {
                    result = result,
                    aabbs = aabbs,
                    buffer = buffer,

                    textureWidth = width,
                    textureHeight = height,
                    isOpenGl = isOpenGl,
                    usesReversedZBuffer = usesReversedZBuffe,
                    world2HZB = m_lastVPs,
                    mip0SizeVector = mip0SizeVector,
                    dataVail = true
                };

                // 调度作业
                JobHandle handle = job.Schedule(result.Length, 64);
                handle.Complete();
                aabb.UpdateRender(result.ToArray());

                //清理
                result.Dispose();
                aabbs.Dispose();
                buffer.Dispose();

            
            }
            isRequest = false;
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
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
    
    
    // public void ExecuteCull(ScriptableRenderContext context, RenderPassEvent passEvent, ref RenderingData renderingData)
    // {
    //     if (!isCull || isCulled) return;
    //     isCulled = true;
    //     var mipIndex = 4;
    //     var width = m_hizDepthRT.width / (1 << mipIndex);
    //     MapSize = width;
    //     CommandBuffer cmd = CommandBufferPool.Get("DepthPyramid");
    //     uint[] result = new uint[aabb._centerTexture.width * aabb._centerTexture.height];
    //     ComputeBuffer resultBuffer = new ComputeBuffer(result.Length, sizeof(uint));
    //     // computeBuffer.SetData(result);
    //     var world2Project = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;
    //     cmd.SetGlobalTexture("_centerTexture", aabb._centerTexture);
    //     cmd.SetGlobalTexture("_sizeTexture", aabb._sizeTexture);
    //     cmd.SetGlobalTexture("_DepthPyramidTex", m_hizDepthRT);
    //     cmd.SetGlobalMatrix("_GPUCullingVP", world2Project);
    //     cmd.SetGlobalVector("_MipmapLevelMinMaxIndex", new Vector2(0, 5));
    //     var screen = new Vector2(MapSize, MapSize);
    //     cmd.SetGlobalVector("_Mip0Size", new Vector2(screen.x, screen.y));
    //     cmd.SetGlobalFloat("_width", width);
    //     cmd.SetComputeBufferParam(m_cullCS, m_Cullkernel, "_Result", resultBuffer);
    //     context.ExecuteCommandBuffer(cmd);
    //     var totalCount = aabb._centerTexture.width * aabb._centerTexture.height;
    //     cmd.DispatchCompute(m_cullCS, m_Cullkernel,  totalCount/ 8,  totalCount/ 8, 1);
    //     resultBuffer.GetData(result);
    //     aabb.UpdateRender(result);
    //     CommandBufferPool.Release(cmd);
    // }
    //

    public static void Log(string str)
    {
        // Debug.LogError(str);
    }
}