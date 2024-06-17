using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
 
public class TestUrpPostRenderPassFeature : ScriptableRendererFeature
{
    public Material postMat;
 
    class CustomRenderPass : ScriptableRenderPass
    {
        private int effectRtPropId = Shader.PropertyToID("testUrpRt");
        private RenderTargetIdentifier effectRtId;
        private RenderTargetIdentifier source;
 
        public Material postMat;
 
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            source = renderingData.cameraData.renderer.cameraColorTarget;
 
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
 
            cmd.GetTemporaryRT(effectRtPropId, descriptor, FilterMode.Bilinear);
            effectRtId = new RenderTargetIdentifier(effectRtPropId);
 
            // Debug.Log("custom urp setup camera");
        }
 
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Custom Post Processing");
            cmd.Clear();
            Blit(cmd, source, effectRtId, postMat);
            Blit(cmd, effectRtId, source);
 
            context.ExecuteCommandBuffer(cmd);
 
            CommandBufferPool.Release(cmd);
            // Debug.Log("custom urp execute");
        }
 
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(effectRtPropId);
            // Debug.Log("custom urp release");
        }
    }
 
    CustomRenderPass m_ScriptablePass;
 
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass();
 
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        m_ScriptablePass.postMat = postMat;
    }
 
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}