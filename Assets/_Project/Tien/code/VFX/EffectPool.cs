using System.Collections.Generic;
using UnityEngine;

public static class EffectPool
{
    private static readonly Dictionary<GameObject, Queue<GameObject>> Pools = new Dictionary<GameObject, Queue<GameObject>>();
    private static readonly Dictionary<GameObject, GameObject> PrefabsByInstance = new Dictionary<GameObject, GameObject>();
    private static readonly Dictionary<GameObject, int> SpawnVersions = new Dictionary<GameObject, int>();
    private static readonly HashSet<GameObject> InPool = new HashSet<GameObject>();

    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
        {
            return;
        }

        Queue<GameObject> pool = GetPool(prefab);
        int readyCount = 0;
        foreach (GameObject instance in pool)
        {
            if (instance != null)
            {
                readyCount++;
            }
        }

        for (int i = readyCount; i < count; i++)
        {
            GameObject created = CreateInstance(prefab);
            created.SetActive(false);
            pool.Enqueue(created);
            InPool.Add(created);
        }
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Vector3 localScale = prefab != null ? prefab.transform.localScale : Vector3.one;
        return Spawn(prefab, position, rotation, localScale);
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 localScale)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = GetInstance(prefab);
        instance.transform.SetParent(null, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = localScale;
        InPool.Remove(instance);
        SpawnVersions[instance] = GetVersion(instance) + 1;
        instance.SetActive(true);

        foreach (PooledEffect effect in instance.GetComponentsInChildren<PooledEffect>(true))
        {
            effect.Play();
        }

        return instance;
    }

    public static int GetVersion(GameObject instance)
    {
        return instance != null && SpawnVersions.TryGetValue(instance, out int version)
            ? version
            : 0;
    }

    public static bool ReleaseFromChild(GameObject child)
    {
        Transform current = child != null ? child.transform : null;
        while (current != null)
        {
            if (PrefabsByInstance.ContainsKey(current.gameObject))
            {
                Release(current.gameObject);
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    public static float GetRootScaleFromChild(GameObject child, float fallback)
    {
        Transform current = child != null ? child.transform : null;
        while (current != null)
        {
            if (PrefabsByInstance.ContainsKey(current.gameObject))
            {
                return Mathf.Abs(current.localScale.x);
            }

            current = current.parent;
        }

        return fallback;
    }

    public static void Release(GameObject instance, int spawnVersion)
    {
        if (instance == null || GetVersion(instance) != spawnVersion)
        {
            return;
        }

        Release(instance);
    }

    public static void Release(GameObject instance)
    {
        if (instance == null || InPool.Contains(instance))
        {
            return;
        }

        if (!PrefabsByInstance.TryGetValue(instance, out GameObject prefab) || prefab == null)
        {
            Object.Destroy(instance);
            return;
        }

        instance.transform.SetParent(null, false);
        instance.SetActive(false);
        GetPool(prefab).Enqueue(instance);
        InPool.Add(instance);
    }

    private static GameObject GetInstance(GameObject prefab)
    {
        Queue<GameObject> pool = GetPool(prefab);
        while (pool.Count > 0)
        {
            GameObject instance = pool.Dequeue();
            if (instance != null)
            {
                return instance;
            }
        }

        return CreateInstance(prefab);
    }

    private static GameObject CreateInstance(GameObject prefab)
    {
        GameObject created = Object.Instantiate(prefab);
        PrefabsByInstance[created] = prefab;
        SpawnVersions[created] = 0;
        return created;
    }

    private static Queue<GameObject> GetPool(GameObject prefab)
    {
        if (!Pools.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            Pools[prefab] = pool;
        }

        return pool;
    }
}
