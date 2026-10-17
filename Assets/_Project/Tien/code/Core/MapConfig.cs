using UnityEngine;

namespace Tien.Core
{
    [CreateAssetMenu(fileName = "New Map Config", menuName = "Tien/Loading/Map Config")]
    public class MapConfig : ScriptableObject
    {
        [Header("Scene Settings")]
        [Tooltip("The exact name of the scene in Build Settings")]
        public string sceneName;

        [Header("Loading Screen Visuals")]
        public Sprite loadingBackground;
        public Color loadingBarColor = Color.white;
    }
}
