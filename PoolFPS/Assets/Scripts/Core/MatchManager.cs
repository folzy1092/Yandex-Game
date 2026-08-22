using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Free-for-all scoring. Every <see cref="Health"/> registers itself here on Start,
/// and this class awards a frag to whoever landed the killing shot. When someone
/// reaches the frag limit the match stops.
/// </summary>
public class MatchManager : MonoBehaviour
{
    public static MatchManager Instance { get; private set; }

    /// <summary>False once someone has won. Weapons check this so shooting stops on the results screen.</summary>
    public static bool IsMatchRunning { get; private set; }

    public int fragLimit = MatchSettings.DefaultFrags;

    public List<Health> Combatants { get { return combatants; } }

    /// <summary>Fired with (combatant, newScore) after every frag.</summary>
    public event Action<GameObject, int> OnScoreChanged;

    /// <summary>Fired with (winnerName, winnerScore) when the frag limit is reached.</summary>
    public event Action<string, int> OnMatchEnded;

    readonly List<Health> combatants = new List<Health>();
    readonly Dictionary<GameObject, int> scores = new Dictionary<GameObject, int>();

    void Awake()
    {
        Instance = this;
        IsMatchRunning = true;
        fragLimit = MatchSettings.FragLimit;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(Health combatant)
    {
        if (combatant == null || combatants.Contains(combatant)) return;

        combatants.Add(combatant);
        scores[combatant.gameObject] = 0;

        GameObject victim = combatant.gameObject;
        combatant.OnDied += killer => AwardFrag(killer, victim);
    }

    public int GetScore(GameObject combatant)
    {
        int score;
        return scores.TryGetValue(combatant, out score) ? score : 0;
    }

    /// <summary>The combatant with the most frags right now, or null if nobody has scored.</summary>
    public GameObject GetLeader()
    {
        GameObject leader = null;
        int best = 0;

        foreach (KeyValuePair<GameObject, int> entry in scores)
        {
            if (entry.Key == null) continue;
            if (entry.Value > best)
            {
                best = entry.Value;
                leader = entry.Key;
            }
        }

        return leader;
    }

    void AwardFrag(GameObject killer, GameObject victim)
    {
        if (!IsMatchRunning) return;

        // No point for falling over on your own or for shooting yourself.
        if (killer == null || killer == victim) return;
        if (!scores.ContainsKey(killer)) return;

        int score = scores[killer] + 1;
        scores[killer] = score;

        if (OnScoreChanged != null) OnScoreChanged(killer, score);

        if (score >= fragLimit)
            EndMatch(killer.name, score);
    }

    void EndMatch(string winnerName, int winnerScore)
    {
        IsMatchRunning = false;
        if (OnMatchEnded != null) OnMatchEnded(winnerName, winnerScore);
    }

    public void RestartMatch()
    {
        IsMatchRunning = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMenu()
    {
        IsMatchRunning = true;
        SceneManager.LoadScene("MainMenu");
    }
}
