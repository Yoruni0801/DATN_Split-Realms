using System.Collections.Generic;
using UnityEngine;

namespace Tien.AdvancedLoading.Data
{
    [CreateAssetMenu(fileName = "Loading Database", menuName = "Tien/Advanced Loading/Loading Database")]
    public class LoadingDatabase : ScriptableObject
    {
        public List<LoadingProfile> profiles = new List<LoadingProfile>();
        public LoadingProfile defaultProfile;

        public LoadingProfile GetProfileForScene(string sceneName)
        {
            foreach (var profile in profiles)
            {
                if (profile.targetSceneName == sceneName)
                    return profile;
            }
            return defaultProfile;
        }
    }
}
