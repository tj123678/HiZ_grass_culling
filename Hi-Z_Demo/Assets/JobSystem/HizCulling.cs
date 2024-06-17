using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class HizCulling : MonoBehaviour
{
    public ComputeShader cullShader;
    private bool isCull;
    private AABBMgr aabb;

    private int kernelId;
    public static int MapSize = 256;
    private bool isOpenGl;

    // Start is called before the first frame update
    void Start()
    {
        isRequest = false;
        Application.targetFrameRate = 30;
        aabb = FindObjectOfType<AABBMgr>();
        kernelId = cullShader.FindKernel("CullingFrag");
        isOpenGl = IsOpenGL();
        Debug.Log($"cell:{1.0 / MapSize}  {isOpenGl}");
    }

    // Update is called once per frame

    private bool isCulled = false;
    void Update()
    {
        if (isCull && !isCulled)
        {
            // GPUCull();
            if (isRequest) return;
            StartCoroutine(CPUCull());
            isCulled = true;
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
        if (GUILayout.Button("是否剔除：" + (isCull ? "on" : "off"),GUILayout.ExpandWidth(true), GUILayout.Height(100)))
        {
            aabb.UpdateInfo();
            isCull = !isCull;
            isCulled = false;
        }

        GUILayout.Space(500);
        if (GUILayout.Button("显示所有",GUILayout.ExpandWidth(true), GUILayout.Height(100)))
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

    private int[] depthMpID;
    private void GPUCull()
    {
        CommandBuffer cmd = CommandBufferPool.Get("DepthPyramid");
        var world2Project = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;
        cmd.SetGlobalTexture("_centerTexture",aabb._centerTexture);
        cmd.SetGlobalTexture("_sizeTexture",aabb._sizeTexture);
        cmd.SetGlobalTexture("_DepthPyramidTex", HzbInstance.HZB_Depth);
        cmd.SetGlobalMatrix("_GPUCullingVP",world2Project);
        cmd.SetGlobalVector("_MipmapLevelMinMaxIndex", new Vector2(0, 5));
        var screen = new Vector2(MapSize, MapSize);
        cmd.SetGlobalVector("_Mip0Size", new Vector2(screen.x, screen.y));
        // cmd.SetRenderTarget();

    }

    private bool isRequest;
    private int frameCount = 0;
    private IEnumerator CPUCull()
    {
        isRequest = true;
        //当前mipmap的大小
        int size = MapSize / (1 << 4);
        Log("size:" + size);
        var (_, _, bounds) = aabb.GetAABBInfo();
        NativeArray<bool> result = new NativeArray<bool>(bounds.Length, Allocator.TempJob);
        NativeArray<Bounds> aabbs = new NativeArray<Bounds>(bounds.Length, Allocator.TempJob);
        NativeArray<float> buffer = new NativeArray<float>(size * size, Allocator.TempJob);

        for (int i = 0; i < bounds.Length; i++)
        {
            aabbs[i] = bounds[i];
        }
        
        RenderTexture renderTexture = HzbInstance.HZB_Depth;

        // 确保RenderTexture是活动的
        RenderTexture.active = renderTexture;
        
        // // 创建一个Texture2D来存储RenderTexture的数据
        // Texture2D targetTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        // // 从RenderTexture复制像素数据到Texture2D
        // targetTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        // targetTexture.Apply();
        // var pixels1 = targetTexture.GetPixels();
        // // 创建一个Color数组来存储像素数据
        // int logCount1 = 0;
        // for (int i = 0; i < pixels1.Length; i++)
        // {
        //     buffer[i] = pixels1[i].r;
        //     if (buffer[i] > 0 && buffer[i] < 1 && logCount1<10)
        //     {
        //         Debug.LogError($"depth1 unity:{i} {pixels1[i].r}");
        //         logCount1++;
        //     }
        // }


        frameCount = Time.frameCount;
        Matrix4x4 world2HZB = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, true) * Camera.main.worldToCameraMatrix;
        var req = AsyncGPUReadback.Request(renderTexture, 4, renderTexture.graphicsFormat);
        
        yield return new WaitUntil(() => req.done);
        
        float[] pixels = req.GetData<float>().ToArray();

        // int logCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            buffer[i] = pixels[i];
            // if (buffer[i] > 0 && buffer[i] < 1 && logCount<10)
            // {
            //     Debug.LogError($"depth unity:{i} {buffer[i]}");
            //     logCount++;
            // }
        }

        // Debug.Log($"frame count:{Time.frameCount - frameCount}");
        
        int textureWidth = size;
        bool usesReversedZBuffe = SystemInfo.usesReversedZBuffer;
        
        Vector2Int mip0SizeVector = new Vector2Int(size, size);
        
        var job = new HizCullJob()
        {
            result = result,
            aabbs = aabbs,
            buffer = buffer,
            
            textureWidth = textureWidth,
            isOpenGl = isOpenGl,
            usesReversedZBuffer = usesReversedZBuffe,
            world2HZB = world2HZB,
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
        
        isRequest = false;
    }

    public static void Log(string str)
    {
        Debug.LogError(str);
    }
}