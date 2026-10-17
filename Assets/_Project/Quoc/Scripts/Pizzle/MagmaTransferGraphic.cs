using UnityEngine;
using UnityEngine.UI;

internal sealed class MagmaTransferGraphic : MaskableGraphic
{
    private readonly Vector2[] vesselConnections =
    {
        new Vector2(-305f, 190f), new Vector2(0f, 190f), new Vector2(305f, 190f), new Vector2(0f, 285f)
    };
    private float progress;
    private int sourceVessel;
    private int destinationVessel = 1;

    public void SetFlow(int source, int destination)
    {
        sourceVessel = Mathf.Clamp(source, 0, vesselConnections.Length - 1);
        destinationVessel = Mathf.Clamp(destination, 0, vesselConnections.Length - 1);
        SetVerticesDirty();
    }

    public void SetProgress(float value) { progress = Mathf.Clamp01(value); SetVerticesDirty(); }

    private void Update()
    {
        if (isActiveAndEnabled) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Color pipe = new Color(0.22f, 0.09f, 0.035f, 0.96f);
        Color pipeRim = new Color(0.72f, 0.30f, 0.08f, 0.95f);
        for (int index = 0; index < vesselConnections.Length - 1; index++)
        {
            FireUiMesh.AddLine(helper, vesselConnections[index], vesselConnections[3], 19f, pipe);
            FireUiMesh.AddLine(helper, vesselConnections[index], vesselConnections[3], 4f, pipeRim);
        }

        Vector2 start = vesselConnections[sourceVessel];
        Vector2 end = vesselConnections[destinationVessel];
        float pulse = 0.78f + Mathf.Sin(Time.unscaledTime * 16f) * 0.18f;
        FireUiMesh.AddLine(helper, start, Vector2.Lerp(start, end, progress), 20f, new Color(0.08f, 0.55f, 0.92f, 0.28f * pulse));
        FireUiMesh.AddLine(helper, start, Vector2.Lerp(start, end, progress), 7f, new Color(0.32f, 0.86f, 1f, 0.96f));
    }
}
