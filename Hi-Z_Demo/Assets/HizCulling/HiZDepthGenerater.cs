using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    public class HiZDepthGenerater : ScriptableRendererFeature
    {
        private HiZDepthGeneratePass m_hizCullTestPass;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public int renderPassEventOffset = 0;

        public override void Create()
        {
            m_hizCullTestPass = new HiZDepthGeneratePass(renderPassEvent + renderPassEventOffset);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_hizCullTestPass);
        }
    }

    public class HiZDepthGeneratePass : ScriptableRenderPass
    {
        public HiZDepthGeneratePass(RenderPassEvent rpe)
        {
            this.renderPassEvent = rpe;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 检查当前相机是否为剔除相机
            if (!renderingData.cameraData.camera.CompareTag("MainCamera")) return;

            if (HizMgr.Instance == null) return;

            HizMgr.Instance.ExecuteDepthGenerate(context, renderPassEvent, ref renderingData);
        }
    }
}