using UnityEngine;

[DisallowMultipleComponent]
public class MirrorBeamDeflector : MonoBehaviour, IBeamDeflector
{
    [Header("Cấu hình phản xạ")]
    [SerializeField] private Vector3 localNormal = Vector3.up;

    public Vector3 GetDeflectDirection(Vector3 incomingDirection, Vector3 hitPoint, Vector3 hitNormal)
    {
        incomingDirection.z = 0f; // Game 2D: ép Z = 0
        Vector3 surfaceNormal = transform.TransformDirection(localNormal);
        surfaceNormal.z = 0f;
        surfaceNormal.Normalize();

        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = transform.up;
        }

        // Luôn luôn phản xạ: tia chiếu từ mặt nào cũng được phản chiếu lại
        float dot = Vector3.Dot(incomingDirection, surfaceNormal);
        if (dot > 0f)
        {
            surfaceNormal = -surfaceNormal;
        }

        Vector3 result = Vector3.Reflect(incomingDirection.normalized, surfaceNormal).normalized;
        result.z = 0f;
        return result;
    }
}
