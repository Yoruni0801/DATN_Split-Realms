using UnityEngine;

public class WorldCutController : MonoBehaviour
{
    public Material mat;

    public Vector2 cutStart;
    public Vector2 cutDir;
    public float shift = 0.1f;
    public bool isCutting;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            cutStart = Camera.main.ScreenToViewportPoint(Input.mousePosition);
        }

        if (Input.GetMouseButton(0))
        {
            Vector2 current = Camera.main.ScreenToViewportPoint(Input.mousePosition);
            cutDir = (current - cutStart).normalized;

            isCutting = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isCutting = false;
        }

        mat.SetVector("_CutStart", cutStart);
        mat.SetVector("_CutNormal", cutDir);
        mat.SetFloat("_ShiftAmount", shift);
        mat.SetFloat("_isCutting", isCutting ? 1 : 0);
    }
}