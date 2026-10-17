using DG.Tweening;
using UnityEngine;

public class InazumaCube : MonoBehaviour
{
    [Header("Effects")]
    public GameObject[] effectsToToggle;

    public bool isRotating { get; private set; }
    public bool IsBusy => isRotating || queuedRotationCount > 0;

    private int queuedRotationCount;

    public void ExecuteRotation()
    {
        queuedRotationCount++;
        TryStartNextRotation();
    }

    private void ToggleEffects(bool status)
    {
        foreach (GameObject effect in effectsToToggle)
        {
            if (effect != null)
            {
                effect.SetActive(status);
            }
        }
    }

    private void TryStartNextRotation()
    {
        if (isRotating || queuedRotationCount <= 0)
        {
            return;
        }

        queuedRotationCount--;
        isRotating = true;
        ToggleEffects(false);

        transform.DORotate(new Vector3(0f, 90f, 0f), 0.5f, RotateMode.LocalAxisAdd)
            .SetEase(Ease.OutBack)
            .OnComplete(OnRotationComplete);
    }

    private void OnRotationComplete()
    {
        isRotating = false;

        if (queuedRotationCount > 0)
        {
            TryStartNextRotation();
            return;
        }

        ToggleEffects(true);
    }
}
