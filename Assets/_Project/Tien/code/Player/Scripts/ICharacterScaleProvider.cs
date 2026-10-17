using UnityEngine;

public interface ICharacterScaleProvider
{
    float CurrentScaleMultiplier { get; }

    Vector3 ScaleOffset(Vector3 baseOffset);

    float ScaleDistance(float baseDistance);
}
