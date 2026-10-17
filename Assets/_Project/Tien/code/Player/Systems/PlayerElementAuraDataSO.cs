using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerElementAuraData", menuName = "Duy/ScriptableObjects/Player/PlayerElementAuraData")]
public class PlayerElementAuraDataSO : ScriptableObject
{
    [System.Serializable]
    public class ElementAuraConfig
    {
        public SkillSetSO skillSet;
        public GameObject auraPrefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }

    public List<ElementAuraConfig> auraConfigs = new List<ElementAuraConfig>();
}
