using System.Collections;
using System.Collections.Generic;
 using UnityEngine;
using UnityEngine.Rendering;

public class HzbDepthTexMaker : MonoBehaviour
{

    public RenderTexture hzbDepth;
     public Shader hzbShader;
    private Material hzbMat;
 
    public bool stopMpde;
    // Use this for initialization
    void Start()
    {
        hzbMat = new Material(hzbShader);
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;

        hzbDepth = new RenderTexture(1024, 1024, 0, RenderTextureFormat.RHalf);
        hzbDepth.autoGenerateMips = false;

        hzbDepth.useMipMap = true;
        hzbDepth.filterMode = FilterMode.Point;
        hzbDepth.Create();
        HzbInstance.HZB_Depth = hzbDepth;
        Test();
    }

    public ComputeShader computeShader;
    public Texture sourceTexture;
    public SpriteRenderer sp;
    private void Test()
    {
        // 假设computeShader是已经加载的Compute Shader对象
        // 假设sourceTexture是纹理资源
        int kernelHandle = computeShader.FindKernel("CSMain");

        // 设置纹理资源
        computeShader.SetTexture(kernelHandle, "input", sourceTexture);
        computeShader.SetFloat("width", sourceTexture.width);
        computeShader.SetFloat("height", sourceTexture.width);

        // 创建RWBuffer并设置其大小为纹理的像素总数
        ComputeBuffer resultBuffer = new ComputeBuffer(sourceTexture.width * sourceTexture.height, sizeof(float) * 4);
        computeShader.SetBuffer(kernelHandle, "Result", resultBuffer);

        // 计算线程组的数量以覆盖整个纹理
        int numGroupsX = sourceTexture.width / 8; // 确保完全覆盖纹理宽度
        int numGroupsY = sourceTexture.width / 8; // 确保完全覆盖纹理高度
        computeShader.Dispatch(kernelHandle, numGroupsX, numGroupsY, 1);
        

        Vector4[] results = new Vector4[sourceTexture.width * sourceTexture.height];
        resultBuffer.GetData(results);
        resultBuffer.Release();
        
        
        // 创建Texture2D并设置为可读写
        // 填充纹理数据，这里只是示例，将纹理填充为纯色
        // 创建像素数组
        Color32[] pixels = new Color32[sp.sprite.texture.width * sp.sprite.texture.height];

        // 填充像素数组
        for (int i = 0; i < results.Length; i++)
        {
            // 将结果复制到目标纹理的像素数组中，这里假设进行某种形式的映射或缩放
            // 例如，这里简单地将 16x16 的纹理数据复制到 32x32 的纹理的左上角
            int x = i % sourceTexture.width;
            int y = i / sourceTexture.width;
            var color = results[i];
            pixels[x + y * sp.sprite.texture.width] = new Color(color.x, color.y, color.z, color.w);  //new Color32((byte)(results[i].x * 255), (byte)(results[i].y * 255), (byte)(results[i].z * 255), (byte)(results[i].w * 255));
        }

        // 设置像素数据到目标纹理
        sp.sprite.texture.SetPixels32(pixels);
        // 应用纹理数据
        sp.sprite.texture.Apply();
    }
    
    void OnDestroy()
    {
        hzbDepth.Release();
        Destroy(hzbDepth);

    }

    int ID_DepthTexture;
    int ID_InvSize;
#if UNITY_EDITOR
    void Update()
    {
#else

    void OnPreRender()
    {
#endif

        if (stopMpde)
        {

            return;
        }
        int w = hzbDepth.width;
        int h = hzbDepth.height;
        int level = 0;

        RenderTexture lastRt = null;
        if (ID_DepthTexture == 0)
        {
            ID_DepthTexture = Shader.PropertyToID("_DepthTexture");
            ID_InvSize = Shader.PropertyToID("_InvSize");
        }
        RenderTexture tempRT;
        while (h > 8)
        {


            hzbMat.SetVector(ID_InvSize, new Vector4(1.0f / w, 1.0f / h, 0, 0));

            tempRT = RenderTexture.GetTemporary(w, h, 0, hzbDepth.format);
            tempRT.filterMode = FilterMode.Point;
            if (lastRt == null)
            {
              //  hzbMat.SetTexture(ID_DepthTexture, Shader.GetGlobalTexture("_CameraDepthTexture"));
                Graphics.Blit(Shader.GetGlobalTexture("_CameraDepthTexture"), tempRT);
            }
            else
            {
                hzbMat.SetTexture(ID_DepthTexture, lastRt);
                Graphics.Blit(null, tempRT, hzbMat);
                RenderTexture.ReleaseTemporary(lastRt);
            }
            Graphics.CopyTexture(tempRT, 0, 0, hzbDepth, 0, level);
            lastRt = tempRT;

            w /= 2;
            h /= 2;
            level++;


        }
        RenderTexture.ReleaseTemporary(lastRt);
    }

}
