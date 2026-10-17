using UnityEngine;

public class BoneElementAura : MonoBehaviour
{
    [SerializeField] private PlayerElementAuraDataSO auraData;
    [SerializeField] private Transform[] auraAnchors;
    [SerializeField] private bool useThisTransformWhenNoAnchors = true;
    [SerializeField] private bool restartParticlesOnChange = true;

    private Player player;
    private GameObject[] currentAuraInstances;
    private SkillSetSO currentSkillSet;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.OnElementChanged += HandleElementChanged;

            if (player.currentState != null)
            {
                HandleElementChanged(player.currentState, 0, 0);
            }
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.OnElementChanged -= HandleElementChanged;
        }
    }

    private void HandleElementChanged(SkillSetSO newSkillSet, int index, int count)
    {
        if (currentSkillSet == newSkillSet && HasAuraInstances()) return;
        
        currentSkillSet = newSkillSet;
        ChangeAura(newSkillSet);
    }

    private void ChangeAura(SkillSetSO skillSet)
    {
        ClearAuraInstances();

        if (skillSet == null || auraData == null) return;

        PlayerElementAuraDataSO.ElementAuraConfig matchedConfig = auraData.auraConfigs.Find(config => config.skillSet == skillSet);

        if (matchedConfig != null && matchedConfig.auraPrefab != null)
        {
            Transform[] targets = ResolveAuraAnchors();
            currentAuraInstances = new GameObject[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                Transform target = targets[i];
                if (target == null) continue;

                GameObject auraInstance = Instantiate(matchedConfig.auraPrefab, target);
                auraInstance.transform.localPosition = matchedConfig.localPosition;
                auraInstance.transform.localRotation = Quaternion.Euler(matchedConfig.localEulerAngles);
                auraInstance.transform.localScale = matchedConfig.localScale == Vector3.zero
                    ? Vector3.one
                    : matchedConfig.localScale;

                if (restartParticlesOnChange)
                {
                    RestartParticles(auraInstance);
                }

                currentAuraInstances[i] = auraInstance;
            }
        }
    }

    private Transform[] ResolveAuraAnchors()
    {
        if (auraAnchors != null && auraAnchors.Length > 0)
        {
            return auraAnchors;
        }

        return useThisTransformWhenNoAnchors
            ? new[] { transform }
            : new Transform[0];
    }

    private bool HasAuraInstances()
    {
        if (currentAuraInstances == null || currentAuraInstances.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < currentAuraInstances.Length; i++)
        {
            if (currentAuraInstances[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearAuraInstances()
    {
        if (currentAuraInstances == null)
        {
            return;
        }

        for (int i = 0; i < currentAuraInstances.Length; i++)
        {
            if (currentAuraInstances[i] != null)
            {
                Destroy(currentAuraInstances[i]);
            }
        }

        currentAuraInstances = null;
    }

    private static void RestartParticles(GameObject auraInstance)
    {
        if (auraInstance == null)
        {
            return;
        }

        ParticleSystem[] particles = auraInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }
}
