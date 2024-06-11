using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class HizCulling : MonoBehaviour
{
    public ComputeShader cullShader;
    private bool isCull;
    private AABBMgr aabb;

    private int kernelId;

    // Start is called before the first frame update
    void Start()
    {
        aabb = FindObjectOfType<AABBMgr>();
        kernelId = cullShader.FindKernel("CullingFrag");
    }

    // Update is called once per frame

    private bool isCulled = false;
    void Update()
    {
        if (isCull && !isCulled)
        {
            // GPUCull();
            CPUCull();
            // isCulled = true;
        }
        else
        {
            aabb.ShowAll();
        }
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal(); // 开始一个水平布局
        GUILayout.Space(500); // 填充空白，将按钮推向右侧

        if (GUILayout.Button("是否剔除：" + (isCull ? "on" : "off")))
        {
            isCull = !isCull;
        }

        GUILayout.EndHorizontal(); // 结束水平布局
    }

    private void GPUCull()
    {
        var m = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;
        var (center, zise, _) = aabb.GetAABBInfo();
        cullShader.SetMatrix("_GPUCullingVP", m);
        cullShader.SetTexture(kernelId, "_centerTexture", center);
        cullShader.SetTexture(kernelId, "_sizeTexture", center);
        cullShader.SetTexture(kernelId, "_DepthPyramidTex", HzbInstance.HZB_Depth);
        cullShader.SetVector("_MipmapLevelMinMaxIndex", new Vector4(0, HzbInstance.HZB_Depth.descriptor.mipCount - 1));
        cullShader.SetVector("_Mip0Size", new Vector4(1024, 1024));
        cullShader.SetFloat("_width", aabb.Size);
        var resultBuffer = new ComputeBuffer(aabb.Size * aabb.Size, sizeof(uint)); // 分配
        cullShader.SetBuffer(kernelId, "_Result", resultBuffer);
        cullShader.Dispatch(kernelId, Mathf.CeilToInt(aabb.Size / 8.0f), Mathf.CeilToInt(aabb.Size / 8.0f), 1);

        var results = new uint[aabb.Size * aabb.Size];
        resultBuffer.GetData(results);
        aabb.UpdateRender(results);
    }

    public SpriteRenderer sp;
    private void CPUCull()
    {
        var (_, _, bounds) = aabb.GetAABBInfo();
        NativeArray<bool> result = new NativeArray<bool>(bounds.Length, Allocator.TempJob);
        NativeArray<Bounds> aabbs = new NativeArray<Bounds>(bounds.Length, Allocator.TempJob);
        NativeArray<float> buffer = new NativeArray<float>(1024 * 1024, Allocator.TempJob);

        for (int i = 0; i < bounds.Length; i++)
        {
            aabbs[i] = bounds[i];
        }
        
        // 假设你已经有了一个RenderTexture实例
        RenderTexture renderTexture = HzbInstance.HZB_Depth;

        // 确保RenderTexture是活动的
        RenderTexture.active = renderTexture;

        // 创建一个Texture2D来存储RenderTexture的数据
        Texture2D targetTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);

        // 从RenderTexture复制像素数据到Texture2D
        targetTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        targetTexture.Apply();

        // 创建一个Color数组来存储像素数据
        Color[] pixels = targetTexture.GetPixels();

        for (int i = 0; i < pixels.Length; i++)
        {
            buffer[i] = pixels[i].r;
        }
        
        // 创建Texture2D并设置为可读写
        // 填充纹理数据，这里只是示例，将纹理填充为纯色
        // 创建像素数组
        Color32[] pixel1s = new Color32[sp.sprite.texture.width * sp.sprite.texture.height];

        // 填充像素数组
        for (int i = 0; i < pixels.Length; i++)
        {
            // 将结果复制到目标纹理的像素数组中，这里假设进行某种形式的映射或缩放
            // 例如，这里简单地将 16x16 的纹理数据复制到 32x32 的纹理的左上角
            int x = (i % renderTexture.width) / 2;
            int y = (i / renderTexture.width) / 2;
            var color = pixels[i];
            pixel1s[x + y * sp.sprite.texture.width] = new Color(color.r, color.g, color.b, 256);  //new Color32((byte)(results[i].x * 255), (byte)(results[i].y * 255), (byte)(results[i].z * 255), (byte)(results[i].w * 255));
        }
        // 设置像素数据到目标纹理
        sp.sprite.texture.SetPixels32(pixel1s);
        // 应用纹理数据
        sp.sprite.texture.Apply();

        int textureWidth = 1024;
#if UNITY_REVERSED_Z
       bool usesReversedZBuffe = true;
#else
        bool usesReversedZBuffe = false;
#endif
        float4x4 world2HZB = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;
        Vector2Int mip0SizeVector = new Vector2Int(1024, 1024);

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
        Destroy(targetTexture);
    }
}