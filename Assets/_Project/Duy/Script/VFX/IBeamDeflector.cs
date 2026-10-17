using UnityEngine;
public interface IBeamDeflector
{
    Vector3 GetDeflectDirection(Vector3 incomingDirection, Vector3 hitPoint, Vector3 hitNormal);
}
