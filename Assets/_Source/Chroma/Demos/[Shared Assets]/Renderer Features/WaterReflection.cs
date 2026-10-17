using UnityEngine.Rendering.Universal;

namespace Dustyroom {
public class WaterReflection : GenericRendererFeature {
    public WaterReflection() {
        requirements = ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;
        injectionPoint = InjectionPoint.BeforeRenderingPostProcessing;
#if UNITY_6000_0_OR_NEWER
        fetchColorBuffer = true;
#endif
    }
}
}
