using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BamaoUIPack.Scripts
{
    // <summary>
    // Use To Activate/Deactivate Another Game Object
    // </summary>
    public class BtnActivator : MonoBehaviour
    {
        public List<GameObject> ActiveTarget;
        public List<GameObject> DeactiveTarget;

        private Button _btn;

        private void Start()
        {
            _btn = GetComponent<Button>();
            _btn.onClick.AddListener(() =>
            {
                if (ActiveTarget != null) ActiveTarget.ForEach(o => { if (o != null) o.SetActive(true); });
                if (DeactiveTarget != null) DeactiveTarget.ForEach(o => { if (o != null) o.SetActive(false); });
            });
        }
    }
}