using System.Collections.Generic;
using UnityEngine;

public sealed class NPCQuestObjectiveTracker
{
    private readonly NPCQuestGiver owner;

    public bool LumberyardMonstersSpawned { get; private set; }

    public NPCQuestObjectiveTracker(NPCQuestGiver owner)
    {
        this.owner = owner;
    }

    public void DisableLumberyardMonsters()
    {
        if (owner.lumberyardMonsters == null)
        {
            return;
        }

        foreach (GameObject monster in owner.lumberyardMonsters)
        {
            if (monster != null)
            {
                monster.SetActive(false);
            }
        }
    }

    public bool TrySpawnLumberyardMonsters()
    {
        if (owner.lumberyardTransform == null || owner.playerTransform == null)
        {
            return false;
        }

        float distance = Vector2.Distance(owner.playerTransform.position, owner.lumberyardTransform.position);
        if (distance > owner.lumberyardSpawnRadius)
        {
            return false;
        }

        if (owner.lumberyardMonsters != null)
        {
            foreach (GameObject monster in owner.lumberyardMonsters)
            {
                if (monster != null)
                {
                    monster.SetActive(true);
                }
            }
        }

        LumberyardMonstersSpawned = true;
        return true;
    }

    public bool IsLumberyardCleared()
    {
        return AreMonstersDefeated(owner.lumberyardMonsters);
    }

    public bool AreMonstersDefeated(List<GameObject> monsters)
    {
        if (monsters == null || monsters.Count == 0)
        {
            return true;
        }

        foreach (GameObject monster in monsters)
        {
            if (monster != null)
            {
                return false;
            }
        }

        return true;
    }
}
