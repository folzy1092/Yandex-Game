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
            Vector3 position = PickStartPosition(i);
            BotFactory.Create("Bot " + (i + 1), position, i);
        }
    }

    /// <summary>
    /// Spreads the starting bots over the available spawn points instead of
    /// stacking them all on the first one.
    /// </summary>
    Vector3 PickStartPosition(int index)
    {
        if (SpawnManager.Instance == null || SpawnManager.Instance.spawnPoints.Count == 0)
            return transform.position;

        var points = SpawnManager.Instance.spawnPoints;
        Transform point = points[index % points.Count];
        if (point == null) return transform.position;

        // Small offset so two bots sharing a spawn point do not start inside each other.
        Vector2 jitter = Random.insideUnitCircle * 1.5f;
        return point.position + new Vector3(jitter.x, 0f, jitter.y);
    }
}
