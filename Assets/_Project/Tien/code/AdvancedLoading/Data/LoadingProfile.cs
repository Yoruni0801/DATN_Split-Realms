using UnityEngine;

namespace Tien.AdvancedLoading.Data
{
    [CreateAssetMenu(fileName = "New Loading Profile", menuName = "Tien/Advanced Loading/Loading Profile")]
    public class LoadingProfile : ScriptableObject
    {
        [Header("Scene Binding")]
        public string targetSceneName;

        [Header("Visuals")]
        public Sprite background;
        public Color progressColor = Color.white;
        public Color textColor = Color.white;

        [Header("Tips")]
        [TextArea(2, 4)]
        public string tipText = "Loading...";
    }
}
