using UnityEngine;

/// <summary>
/// Creates the bots for the match. The count comes from <see cref="MatchSettings"/>,
/// which the player set in the main menu, so bots cannot be baked into the scene.
/// </summary>
public class BotSpawner : MonoBehaviour
{
    void Start()
    {
        int count = Mathf.Clamp(MatchSettings.BotCount, MatchSettings.MinBots, MatchSettings.MaxBots);

        for (int i = 0; i < count; i++)
        {
            Vector3 position;
            if (!TryPickStartPosition(i, out position)) continue;

            BotFactory.Create("Bot " + (i + 1), position, i);
        }
    }

    /// <summary>
    /// Spreads the starting bots over the available spawn points instead of
    /// stacking them all on the first one.
    ///
    /// Returns false rather than falling back to this object's own position:
    /// the managers object sits at the world origin, which on this map is the
    /// middle of the pool, so that fallback would drop the entire bot roster
    /// into the water on top of each other.
    /// </summary>
    bool TryPickStartPosition(int index, out Vector3 position)
    {
        position = Vector3.zero;

        if (SpawnManager.Instance == null)
        {
            Debug.LogError("BotSpawner: no SpawnManager in the scene, bots not spawned.");
            return false;
        }

        Transform point = SpawnManager.Instance.GetSpawnByIndex(index);
        if (point == null)
        {
            Debug.LogError("BotSpawner: no usable spawn points, bots not spawned.");
            return false;
        }

        // Small offset so two bots sharing a spawn point do not start inside each other.
        Vector2 jitter = Random.insideUnitCircle * 1.5f;
        position = point.position + new Vector3(jitter.x, 0f, jitter.y);
        return true;
    }
}
