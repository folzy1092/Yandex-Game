using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the arena's respawn points and picks one that is not in an enemy's face.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    public List<Transform> spawnPoints = new List<Transform>();

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Returns the spawn point whose nearest living combatant is farthest away,
    /// so a respawning fighter does not appear next to someone who is already there.
    /// </summary>
    public Transform GetSafeSpawn(GameObject forWhom)
    {
        if (spawnPoints.Count == 0) return null;
        if (spawnPoints.Count == 1) return spawnPoints[0];

        Transform best = null;
        float bestDistance = -1f;

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            Transform candidate = spawnPoints[i];
            if (candidate == null) continue;

            float nearest = NearestCombatantDistance(candidate.position, forWhom);
            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                best = candidate;
            }
        }

        return best != null ? best : spawnPoints[0];
    }

    float NearestCombatantDistance(Vector3 point, GameObject ignore)
    {
        if (MatchManager.Instance == null) return float.MaxValue;

        float nearest = float.MaxValue;
        List<Health> combatants = MatchManager.Instance.Combatants;

        for (int i = 0; i < combatants.Count; i++)
        {
            Health other = combatants[i];
            if (other == null || other.gameObject == ignore || !other.IsAlive) continue;

            float distance = Vector3.Distance(point, other.transform.position);
            if (distance < nearest) nearest = distance;
        }

        return nearest;
    }
}
