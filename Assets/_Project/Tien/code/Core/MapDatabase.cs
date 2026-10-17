using System.Collections.Generic;
using UnityEngine;

namespace Tien.Core
{
    [CreateAssetMenu(fileName = "Map Database", menuName = "Tien/Loading/Map Database")]
    public class MapDatabase : ScriptableObject
    {
        public List<MapConfig> mapConfigs = new List<MapConfig>();

        public MapConfig GetConfigForScene(string sceneName)
        {
            foreach (var config in mapConfigs)
            {
                if (config.sceneName == sceneName)
                {
                    return config;
                }
            }
            return null;
        }
    }
}
