using UnityEngine;
using Quoc.QuyVuong;

namespace Quoc.QuyVuong.Boss_Phase3
{
    /// <summary>
    /// Component hỗ trợ test ngay lập tức Phase 2 (Thương thủ lance_m9) và các kỹ năng trong Unity Scene
    /// Phím tắt trong Play Mode:
    /// - Phím [2]: Chuyển ngay sang Phase 2
    /// - Phím [1]: Test Kích hoạt Skill 1 (Ground Line & Triệu hồi Dusthorn)
    /// - Phím [3]: Test Kích hoạt Skill 2 (Sky Leap & Spear Meteor Slam)
    /// </summary>
    public class QuyVuongPhase2Tester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuyVuongPhaseSwitcher phaseSwitcher;
        [SerializeField] private QuyVuongPhase3Boss phase2Boss;

        [Header("Test UI")]
        [SerializeField] private bool showOnScreenGUI = true;

        private void Awake()
        {
            FindReferences();
        }

        private void FindReferences()
        {
            if (phaseSwitcher == null)
                phaseSwitcher = FindAnyObjectByType<QuyVuongPhaseSwitcher>();

            if (phase2Boss == null)
                phase2Boss = FindAnyObjectByType<QuyVuongPhase3Boss>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (phaseSwitcher == null || phase2Boss == null)
            {
                FindReferences();
            }

            // Phím [2] hoặc Numpad 2: Chuyển ngay sang Phase 2 (lance_m9)
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SwitchToPhase2();
            }

            // Phím [1] hoặc Numpad 1: Kích hoạt Skill 1 (Ground Line & Dusthorn)
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                TriggerSkill1();
            }

            // Phím [3] hoặc Numpad 3: Kích hoạt Skill 2 (Sky Leap & Meteor Slam)
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                TriggerSkill2();
            }
        }

        public void SwitchToPhase2()
        {
            if (phaseSwitcher != null)
            {
                // Phase 2 là index 1 trong phaseObjects array của QuyVuongPhaseSwitcher
                phaseSwitcher.ActivatePhase(1);
                Debug.Log("<color=yellow>[QuyVuongPhase2Tester]</color> Đã kích hoạt Phase 2 (Thương thủ lance_m9)!");
            }
            else if (phase2Boss != null)
            {
                phase2Boss.gameObject.SetActive(true);
                Debug.Log("<color=yellow>[QuyVuongPhase2Tester]</color> Đã kích hoạt GameObject Phase2 trực tiếp!");
            }
        }

        public void TriggerSkill1()
        {
            if (phase2Boss == null) FindReferences();
            if (phase2Boss != null)
            {
                phase2Boss.TriggerSkill1();
                Debug.Log("<color=cyan>[QuyVuongPhase2Tester]</color> Ép kích hoạt Skill 1: Ground Line & Triệu hồi Dusthorn!");
            }
        }

        public void TriggerSkill2()
        {
            if (phase2Boss == null) FindReferences();
            if (phase2Boss != null)
            {
                phase2Boss.TriggerSkill2();
                Debug.Log("<color=cyan>[QuyVuongPhase2Tester]</color> Ép kích hoạt Skill 2: Sky Leap & Spear Meteor Slam!");
            }
        }

        private void OnGUI()
        {
            if (!showOnScreenGUI) return;

            GUI.Box(new Rect(10, 10, 280, 160), "<b>TEST BOSS PHASE 2 (lance_m9)</b>");
            if (GUI.Button(new Rect(20, 35, 260, 30), "Kích hoạt Phase 2 [Phím 2]"))
            {
                SwitchToPhase2();
            }

            if (GUI.Button(new Rect(20, 72, 260, 30), "Skill 1: Ground Line & Mob [Phím 1]"))
            {
                TriggerSkill1();
            }

            if (GUI.Button(new Rect(20, 110, 260, 30), "Skill 2: Sky Leap & Slam [Phím 3]"))
            {
                TriggerSkill2();
            }
        }
    }
}
