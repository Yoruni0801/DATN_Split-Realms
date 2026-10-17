using UnityEngine;

public class SoundTester : MonoBehaviour
{
    public SoundPillar[] pillars;
    [Tooltip("Legacy numeric testing. Disabled so 1-6 are reserved for note jumps.")]
    public bool enableLegacyNumericInput;

    void Start()
    {
        // Tự động tìm tất cả các cột âm thanh nếu chưa gán
        if (pillars == null || pillars.Length == 0)
        {
            pillars = new SoundPillar[10];
            for (int i = 0; i < 10; i++)
            {
                GameObject go = GameObject.Find("cot am thanh_" + i);
                if (go != null)
                {
                    pillars[i] = go.GetComponent<SoundPillar>();
                }
            }
        }
    }

    void Update()
    {
        if (!enableLegacyNumericInput) return;
        // Bấm phím 1 đến 9, 0 để phát thử âm thanh
        if (Input.GetKeyDown(KeyCode.Alpha1)) PlayPillar(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayPillar(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) PlayPillar(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) PlayPillar(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) PlayPillar(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) PlayPillar(5);
        if (Input.GetKeyDown(KeyCode.Alpha7)) PlayPillar(6);
        if (Input.GetKeyDown(KeyCode.Alpha8)) PlayPillar(7);
        if (Input.GetKeyDown(KeyCode.Alpha9)) PlayPillar(8);
        if (Input.GetKeyDown(KeyCode.Alpha0)) PlayPillar(9);
    }

    void PlayPillar(int index)
    {
        if (index >= 0 && index < pillars.Length && pillars[index] != null)
        {
            pillars[index].InteractPillar();
            Debug.Log($"Đang phát thử: {pillars[index].gameObject.name} | Volume: {pillars[index].volumeMultiplier}");
        }
    }

    void OnGUI()
    {
        if (!enableLegacyNumericInput) return;
        GUILayout.BeginArea(new Rect(10, 10, 300, 500));
        
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.normal.textColor = Color.white;
        style.fontSize = 14;

        GUILayout.Box("--- CÔNG CỤ TEST ÂM THANH ---", style);
        GUILayout.Label("Bạn có thể bấm phím 1, 2... 9, 0 trên bàn phím\nhoặc dùng các nút dưới đây để test nhanh.");
        
        for (int i = 0; i < 10; i++)
        {
            if (i < pillars.Length && pillars[i] != null)
            {
                if (GUILayout.Button($"Phát {pillars[i].gameObject.name} (Phím {(i == 9 ? 0 : i + 1)})", GUILayout.Height(30)))
                {
                    PlayPillar(i);
                }
            }
        }
        GUILayout.EndArea();
    }
}
