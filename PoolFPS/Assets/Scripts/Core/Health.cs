using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Hit points, death and respawn. Used by both the player and the bots, so it
/// must not reference anything player-specific. Components that should stop
/// running while dead are listed in <see cref="disableOnDeath"/> by whoever
/// builds the object.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Health : MonoBehaviour
{
    public int maxHealth = 100;
    public float respawnDelay = 3f;
    public MonoBehaviour[] disableOnDeath;

    public int CurrentHealth { get; private set; }
    public bool IsAlive { get { return CurrentHealth > 0; } }

    /// <summary>Fired with (current, max) whenever health changes, including on respawn.</summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>Fired when health reaches zero. The argument is the killer, or null if unknown.</summary>
    public event Action<GameObject> OnDied;

    public event Action OnRespawned;

    CharacterController controller;
    Renderer[] renderers;

    void Awake()
    {
        CurrentHealth = maxHealth;
        controller = GetComponent<CharacterController>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Start()
    {
        if (MatchManager.Instance != null)
            MatchManager.Instance.Register(this);
    }

    public void TakeDamage(int amount, GameObject attacker)
    {
        if (!IsAlive || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (OnHealthChanged != null) OnHealthChanged(CurrentHealth, maxHealth);

        if (CurrentHealth == 0)
            StartCoroutine(DieThenRespawn(attacker));
    }

    IEnumerator DieThenRespawn(GameObject killer)
    {
        if (OnDied != null) OnDied(killer);

        SetAliveState(false);
        yield return new WaitForSeconds(respawnDelay);

        MoveToSpawn();
        CurrentHealth = maxHealth;
        if (OnHealthChanged != null) OnHealthChanged(CurrentHealth, maxHealth);

        SetAliveState(true);
        if (OnRespawned != null) OnRespawned();
    }

    void MoveToSpawn()
    {
        if (SpawnManager.Instance == null) return;

        Transform spawn = SpawnManager.Instance.GetSafeSpawn(gameObject);
        if (spawn == null) return;

        // The CharacterController overrides direct transform writes, so it has
        // to be switched off for the teleport to stick.
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = spawn.position;
        transform.rotation = spawn.rotation;
        controller.enabled = wasEnabled;
    }

    void SetAliveState(bool alive)
    {
        controller.enabled = alive;

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = alive;

        if (disableOnDeath == null) return;
        for (int i = 0; i < disableOnDeath.Length; i++)
            if (disableOnDeath[i] != null) disableOnDeath[i].enabled = alive;
    }
}
