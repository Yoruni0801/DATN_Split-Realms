using UnityEngine;

namespace Bao.Enemies
{
    public static class VFXContainerUtility
    {
        private static Transform vfxContainer;

        public static Transform GetContainer()
        {
            if (vfxContainer == null)
            {
                GameObject go = GameObject.Find("--- Bao_Temporary_VFX ---");
                if (go == null)
                {
                    go = new GameObject("--- Bao_Temporary_VFX ---");
                }
                vfxContainer = go.transform;
            }
            return vfxContainer;
        }
    }
}
