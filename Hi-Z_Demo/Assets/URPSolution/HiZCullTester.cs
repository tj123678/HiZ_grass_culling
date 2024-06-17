using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class HiZCullTester : ScriptableRendererFeature
{
    private HiZCullTestPass m_hizCullTestPass;
    public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    public int renderPassEventOffset = 0;

    public override void Create()
    {
        m_hizCullTestPass = new HiZCullTestPass(renderPassEvent + renderPassEventOffset);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_hizCullTestPass);
    }
}

public class HiZCullTestPass : ScriptableRenderPass
{
    public HiZCullTestPass(RenderPassEvent rpe)
    {
        renderPassEvent = rpe;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (HizMgr.Instance != null)
            HizMgr.Instance.ExecuteCull(context, renderPassEvent, ref renderingData);
    }
}