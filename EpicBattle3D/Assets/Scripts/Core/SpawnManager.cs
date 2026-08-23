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
        EnsureSpawnPoints();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Falls back to finding the spawn markers in the scene if the serialised
    /// list is empty or has gone stale.
    ///
    /// Without this, an empty list silently sends everyone to the world origin —
    /// which on this map is the middle of the pool, so the whole match would
    /// start piled into the water.
    /// </summary>
    void EnsureSpawnPoints()
    {
        spawnPoints.RemoveAll(point => point == null);
        if (spawnPoints.Count > 0) return;

        var root = GameObject.Find("SpawnPoints");
        if (root == null)
        {
            Debug.LogError("SpawnManager: no spawn points assigned and no SpawnPoints object "
                           + "in the scene. Re-run Tools > Epic Battle 3D > BUILD EVERYTHING.");
            return;
        }

        foreach (Transform child in root.transform)
            spawnPoints.Add(child);

        Debug.LogWarning("SpawnManager: spawn list was empty, recovered "
                         + spawnPoints.Count + " points from the scene.");
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

    /// <summary>A spawn point chosen by index, used to place bots at match start.</summary>
    public Transform GetSpawnByIndex(int index)
    {
        if (spawnPoints.Count == 0) return null;

        for (int offset = 0; offset < spawnPoints.Count; offset++)
        {
            Transform candidate = spawnPoints[(index + offset) % spawnPoints.Count];
            if (candidate != null) return candidate;
        }

        return null;
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
