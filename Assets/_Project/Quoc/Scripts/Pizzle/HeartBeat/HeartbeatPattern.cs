using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public struct HeartbeatPattern
{
    [SerializeField] private KeyCode[] keys;

    public KeyCode[] Keys => keys ?? new KeyCode[0];

    public int Length => Keys.Length;

    public string DisplayText
    {
        get
        {
            if (Length == 0)
            {
                return string.Empty;
            }

            if (Length == 1)
            {
                return Keys[0].ToString().ToUpperInvariant();
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(" + ");
                }

                builder.Append(Keys[i].ToString().ToUpperInvariant());
            }

            return builder.ToString();
        }
    }

    public static HeartbeatPattern Single(KeyCode key)
    {
        return new HeartbeatPattern
        {
            keys = new[] { key }
        };
    }

    public static HeartbeatPattern Combo(KeyCode first, KeyCode second)
    {
        return new HeartbeatPattern
        {
            keys = new[] { first, second }
        };
    }

    public bool ContainsAny(HashSet<KeyCode> pressedKeys)
    {
        if (pressedKeys == null || Length == 0)
        {
            return false;
        }

        for (int i = 0; i < Length; i++)
        {
            if (pressedKeys.Contains(Keys[i]))
            {
                return true;
            }
        }

        return false;
    }

    public bool MatchesExactPressed(HashSet<KeyCode> pressedKeys)
    {
        if (pressedKeys == null || pressedKeys.Count != Length || Length == 0)
        {
            return false;
        }

        for (int i = 0; i < Length; i++)
        {
            if (!pressedKeys.Contains(Keys[i]))
            {
                return false;
            }
        }

        return true;
    }

    public bool HasAllRecentPresses(Dictionary<KeyCode, float> keyPressTimes, float windowSeconds)
    {
        if (keyPressTimes == null || Length == 0)
        {
            return false;
        }

        float newestPressTime = float.MinValue;
        float oldestPressTime = float.MaxValue;

        for (int i = 0; i < Length; i++)
        {
            KeyCode key = Keys[i];
            float pressTime;
            if (!keyPressTimes.TryGetValue(key, out pressTime))
            {
                return false;
            }

            if (pressTime > newestPressTime)
            {
                newestPressTime = pressTime;
            }

            if (pressTime < oldestPressTime)
            {
                oldestPressTime = pressTime;
            }
        }

        return newestPressTime - oldestPressTime <= windowSeconds;
    }

    public bool ContainsOnlyValidPressedKeys(HashSet<KeyCode> pressedKeys)
    {
        if (pressedKeys == null)
        {
            return false;
        }

        if (pressedKeys.Count != Length)
        {
            return false;
        }

        for (int i = 0; i < Length; i++)
        {
            if (!pressedKeys.Contains(Keys[i]))
            {
                return false;
            }
        }

        return true;
    }
}
