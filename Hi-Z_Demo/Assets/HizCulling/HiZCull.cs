using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    public class HiZCull : ScriptableRendererFeature
    {
        private HiZCullPass _mHizCullPass;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        public int renderPassEventOffset = 0;

        public override void Create()
        {
            _mHizCullPass = new HiZCullPass(renderPassEvent + renderPassEventOffset);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_mHizCullPass);
        }
    }

    public class HiZCullPass : ScriptableRenderPass
    {
        public HiZCullPass(RenderPassEvent rpe)
        {
            renderPassEvent = rpe;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.camera.CompareTag("MainCamera")) return;
            if (HizMgr.Instance == null) return;
            HizMgr.Instance.ExecuteCull(context, renderPassEvent, ref renderingData);
        }
    }
}