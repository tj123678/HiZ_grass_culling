using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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

    // Start is called before the first frame update
    void Start()
    {
        isRequest = false;
        Application.targetFrameRate = 30;
        aabb = FindObjectOfType<AABBMgr>();
        kernelId = cullShader.FindKernel("CullingFrag");
        Debug.Log($"cell:{1.0 / MapSize}");
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
            // isCulled = true;
        }
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal(); // 开始一个水平布局
        GUILayout.Space(500); // 填充空白，将按钮推向右侧
        if (GUILayout.Button("是否剔除：" + (isCull ? "on" : "off")))
        {
            aabb.UpdateInfo();
            isCull = !isCull;
            isCulled = false;
        }

        GUILayout.Space(500);
        if (GUILayout.Button("显示所有"))
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

    private int[] depthMpID;
    private void GPUCull()
    {
        // if (depthMpID == null)
        // {
        //     depthMpID = new int[32];
        //     for (int i = 0; i < depthMpID.Length; i++)
        //     {
        //         depthMpID[i] = Shader.PropertyToID("_DepthMip" + i);
        //     }
        // }
        // var (center, zise, _) = aabb.GetAABBInfo();
        // var hizTexture = HzbInstance.HZB_Depth;
        // CommandBuffer cmd = CommandBufferPool.Get("DepthPyramid");
        // cmd.SetGlobalTexture("_InputDepth",new RenderTargetIdentifier("_CameraDepthTexture"));
        // cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
        // //降采样Depth
        // for (int i = 0; i < 8; i++)
        // {
        //     
        // }

        CommandBuffer cmd = CommandBufferPool.Get("DepthPyramid");
        var world2Project = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;
        cmd.SetGlobalTexture("_ObjectAABBTexture0",aabb._centerTexture);
        cmd.SetGlobalTexture("_ObjectAABBTexture1",aabb._sizeTexture);
        cmd.SetGlobalMatrix("_GPUCullingVP",world2Project);
        var screen = new Vector2(MapSize, MapSize);
        cmd.SetGlobalVector("_Mip0Size", new Vector4(screen.x, screen.y));
        // cmd.SetRenderTarget();

    }

    private bool isRequest;
    private int frameCount = 0;
    private IEnumerator CPUCull()
    {
        isRequest = true;
        var (_, _, bounds) = aabb.GetAABBInfo();
        NativeArray<bool> result = new NativeArray<bool>(bounds.Length, Allocator.TempJob);
        NativeArray<Bounds> aabbs = new NativeArray<Bounds>(bounds.Length, Allocator.TempJob);
        NativeArray<float> buffer = new NativeArray<float>(MapSize * MapSize, Allocator.TempJob);

        for (int i = 0; i < bounds.Length; i++)
        {
            aabbs[i] = bounds[i];
        }
        
        // 假设你已经有了一个RenderTexture实例
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
        // for (int i = 0; i < pixels1.Length; i++)
        // {
        //     buffer[i] = pixels1[i].r / 255f;
        //     if (buffer[i] > 0)
        //     {
        //         Debug.Log($"depth1 unity:{i} {pixels1[i].r} {buffer[i]}");
        //     }
        // }


        frameCount = Time.frameCount;
        var req = AsyncGPUReadback.Request(renderTexture, 0, renderTexture.graphicsFormat);
        
        yield return new WaitUntil(() => req.done);

        // Debug.Log($"frame count:{Time.frameCount - frameCount}");
        
        float[] pixels = req.GetData<float>().ToArray();
        
        // 创建一个Color数组来存储像素数据
        for (int i = 0; i < pixels.Length; i++)
        {
            buffer[i] = pixels[i];
            if (buffer[i] > 0)
            {
                // Debug.Log($"depth unity:{i} {buffer[i]}");
            }
        }
        
        
        int textureWidth = MapSize;
        bool usesReversedZBuffe = SystemInfo.usesReversedZBuffer;
        float4x4 world2HZB = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;
        Vector2Int mip0SizeVector = new Vector2Int(MapSize, MapSize);
        
        var job = new HizCullJob()
        {
            result = result,
            aabbs = aabbs,
            buffer = buffer,
            
            textureWidth = textureWidth,
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
}