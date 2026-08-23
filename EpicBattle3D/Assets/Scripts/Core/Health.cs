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

    /// <summary>True while dead and counting down to respawn.</summary>
    public bool IsWaitingToRespawn { get; private set; }

    /// <summary>Seconds left before respawn, for the death screen countdown.</summary>
    public float RespawnTimeRemaining { get; private set; }

    /// <summary>Fired with (current, max) whenever health changes, including on respawn.</summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>Fired when health reaches zero. The argument is the killer, or null if unknown.</summary>
    public event Action<GameObject> OnDied;

    /// <summary>
    /// Fired with (attacker, amount) on every hit that lands. Bots listen to this
    /// so being shot from behind makes them turn round instead of walking on.
    /// </summary>
    public event Action<GameObject, int> OnDamaged;

    public event Action OnRespawned;

    [Header("Regeneration")]
    /// <summary>Seconds of not being shot before health starts coming back.</summary>
    public float regenerationDelay = 6f;

    /// <summary>Health per second once regeneration starts. Zero disables it.</summary>
    public float regenerationRate = 11f;

    CharacterController controller;
    Renderer[] renderers;
    float lastDamageTime;
    float regenerationCarry;

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

    void Update()
    {
        Regenerate();
    }

    /// <summary>
    /// Slow health recovery out of combat, applied to everyone equally. Without
    /// it a bot that survives a fight on 8 health is simply doomed, which makes
    /// its decision to back off pointless.
    /// </summary>
    void Regenerate()
    {
        if (!IsAlive || regenerationRate <= 0f) return;
        if (CurrentHealth >= maxHealth) return;
        if (Time.time - lastDamageTime < regenerationDelay) return;

        regenerationCarry += regenerationRate * Time.deltaTime;
        int whole = Mathf.FloorToInt(regenerationCarry);
        if (whole <= 0) return;

        regenerationCarry -= whole;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + whole);
        if (OnHealthChanged != null) OnHealthChanged(CurrentHealth, maxHealth);
    }

    public void TakeDamage(int amount, GameObject attacker)
    {
        if (!IsAlive || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        lastDamageTime = Time.time;
        regenerationCarry = 0f;

        if (OnHealthChanged != null) OnHealthChanged(CurrentHealth, maxHealth);
        if (OnDamaged != null) OnDamaged(attacker, amount);

        if (CurrentHealth == 0)
            StartCoroutine(DieThenRespawn(attacker));
    }

    /// <summary>
    /// Ends the respawn wait immediately. Used by the "respawn now" reward the
    /// player can earn by watching an ad.
    /// </summary>
    public void SkipRespawnWait()
    {
        if (IsWaitingToRespawn) RespawnTimeRemaining = 0f;
    }

    IEnumerator DieThenRespawn(GameObject killer)
    {
        if (OnDied != null) OnDied(killer);

        SetAliveState(false);

        IsWaitingToRespawn = true;
        RespawnTimeRemaining = respawnDelay;
        while (RespawnTimeRemaining > 0f)
        {
            RespawnTimeRemaining -= Time.deltaTime;
            yield return null;
        }
        IsWaitingToRespawn = false;

        MoveToSpawn();
        CurrentHealth = maxHealth;
        // A fresh life should not inherit the previous one's regeneration timer.
        lastDamageTime = Time.time;
        regenerationCarry = 0f;
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
