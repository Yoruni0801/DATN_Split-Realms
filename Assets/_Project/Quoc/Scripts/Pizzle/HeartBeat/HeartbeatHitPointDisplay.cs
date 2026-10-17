using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeartbeatHitPointDisplay : MonoBehaviour
{
    [Serializable]
    private class KeyImageBinding
    {
        [Tooltip("Phím mà image này đại diện.")]
        public KeyCode key;

        [Tooltip("Kéo Image UI tương ứng của nút vào đây.")]
        public Image image;
    }

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private List<KeyImageBinding> keyImages = new List<KeyImageBinding>();
    [SerializeField] private RectTransform hitWindowFrame;
    [SerializeField] private Vector2 hitWindowPadding = new Vector2(18f, 18f);
    [SerializeField] private Color hitWindowBorderColor = new Color(1f, 0.92f, 0.2f, 0.9f);
    [SerializeField] private Color hitWindowFillColor = new Color(0f, 0f, 0f, 0.01f);
    [SerializeField] private float hitWindowBorderThickness = 3f;

    private void Awake()
    {
        SetupHitWindowFrame();
        Hide();
    }

    private void Reset()
    {
        keyImages = new List<KeyImageBinding>
        {
            new KeyImageBinding { key = KeyCode.A },
            new KeyImageBinding { key = KeyCode.S },
            new KeyImageBinding { key = KeyCode.D },
            new KeyImageBinding { key = KeyCode.W }
        };
    }

    public void Show(HeartbeatPattern pattern)
    {
        if (root != null)
        {
            root.SetActive(true);
        }

        if (hitWindowFrame != null)
        {
            hitWindowFrame.gameObject.SetActive(true);
        }

        HideAllKeys();

        KeyCode[] keys = pattern.Keys;
        for (int i = 0; i < keys.Length; i++)
        {
            SetKeyVisible(keys[i], true);
        }
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        if (hitWindowFrame != null)
        {
            hitWindowFrame.gameObject.SetActive(false);
        }

        HideAllKeys();
    }

    public void PlayHitFeedback(HeartbeatPattern pattern)
    {
        // No transform animation here; keep the hitpoint UI exactly where it was placed.
    }

    public void PlayMissFeedback(HeartbeatPattern currentPattern)
    {
        if (root == null || keyImages == null || keyImages.Count == 0)
        {
            return;
        }

        root.SetActive(true);
        HideAllKeys();

        List<KeyCode> originalKeys = new List<KeyCode>(currentPattern.Keys);
        if (originalKeys.Count == 0)
        {
            return;
        }

        List<KeyCode> swappedKeys = GetDifferentKeys(originalKeys);
        if (swappedKeys.Count == 0)
        {
            SetKeysVisible(originalKeys, true);
            return;
        }

        SetKeysVisible(swappedKeys, true);
    }

    private void HideAllKeys()
    {
        if (keyImages == null)
        {
            return;
        }

        for (int i = 0; i < keyImages.Count; i++)
        {
            SetBindingVisible(keyImages[i], false);
        }
    }

    private void SetupHitWindowFrame()
    {
        if (root == null)
        {
            return;
        }

        if (hitWindowFrame == null)
        {
            Transform existing = root.transform.Find("HitWindowFrame");
            if (existing != null)
            {
                hitWindowFrame = existing as RectTransform;
            }
        }

        if (hitWindowFrame == null)
        {
            GameObject frameObject = new GameObject("HitWindowFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            frameObject.transform.SetParent(root.transform, false);
            frameObject.transform.SetAsFirstSibling();
            hitWindowFrame = frameObject.GetComponent<RectTransform>();
        }

        if (hitWindowFrame == null)
        {
            return;
        }

        hitWindowFrame.anchorMin = Vector2.zero;
        hitWindowFrame.anchorMax = Vector2.one;
        hitWindowFrame.pivot = new Vector2(0.5f, 0.5f);
        hitWindowFrame.offsetMin = new Vector2(hitWindowPadding.x, hitWindowPadding.y);
        hitWindowFrame.offsetMax = new Vector2(-hitWindowPadding.x, -hitWindowPadding.y);

        Image image = hitWindowFrame.GetComponent<Image>();
        if (image != null)
        {
            image.color = hitWindowFillColor;
            image.raycastTarget = false;
        }

        Outline outline = hitWindowFrame.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = hitWindowBorderColor;
            outline.effectDistance = new Vector2(hitWindowBorderThickness, -hitWindowBorderThickness);
        }
    }

    private void SetKeyVisible(KeyCode key, bool visible)
    {
        if (keyImages == null)
        {
            return;
        }

        for (int i = 0; i < keyImages.Count; i++)
        {
            KeyImageBinding binding = keyImages[i];
            if (binding != null && binding.key == key)
            {
                SetBindingVisible(binding, visible);
            }
        }
    }

    private void SetKeysVisible(List<KeyCode> keys, bool visible)
    {
        if (keys == null)
        {
            return;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            SetKeyVisible(keys[i], visible);
        }
    }

    private List<KeyCode> GetDifferentKeys(List<KeyCode> originalKeys)
    {
        List<KeyCode> availableKeys = new List<KeyCode>();
        if (keyImages == null)
        {
            return availableKeys;
        }

        for (int i = 0; i < keyImages.Count; i++)
        {
            KeyImageBinding binding = keyImages[i];
            if (binding == null || binding.image == null)
            {
                continue;
            }

            if (!originalKeys.Contains(binding.key))
            {
                availableKeys.Add(binding.key);
            }
        }

        if (availableKeys.Count == 0)
        {
            return originalKeys;
        }

        List<KeyCode> result = new List<KeyCode>();
        int targetCount = originalKeys.Count;
        for (int i = 0; i < targetCount; i++)
        {
            if (availableKeys.Count == 0)
            {
                break;
            }

            int index = UnityEngine.Random.Range(0, availableKeys.Count);
            result.Add(availableKeys[index]);
            availableKeys.RemoveAt(index);
        }

        if (result.Count == 0)
        {
            return originalKeys;
        }

        return result;
    }

    private static void SetBindingVisible(KeyImageBinding binding, bool visible)
    {
        if (binding == null || binding.image == null)
        {
            return;
        }

        binding.image.gameObject.SetActive(visible);
    }
}
