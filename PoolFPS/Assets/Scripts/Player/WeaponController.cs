using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// The player's pistol: instant-hit fire on left mouse, limited magazine, R to reload.
/// </summary>
public class WeaponController : MonoBehaviour
{
    public Camera playerCamera;

    [Header("Ballistics")]
    public int damage = 25;
    public float range = 100f;
    public float spreadDegrees = 0.5f;

    [Header("Magazine")]
    public int magazineSize = 12;
    public float reloadTime = 1.5f;
    public float fireCooldown = 0.25f;

    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }

    /// <summary>Fired with (current, magazineSize) whenever ammo changes.</summary>
    public event Action<int, int> OnAmmoChanged;

    float nextFireTime;

    void Awake()
    {
        CurrentAmmo = magazineSize;
    }

    void Update()
    {
        if (!MatchManager.IsMatchRunning) return;
        if (IsReloading) return;

        if (Input.GetKeyDown(KeyCode.R) && CurrentAmmo < magazineSize)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
            Fire();
    }

    void Fire()
    {
        if (CurrentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return;
        }

        nextFireTime = Time.time + fireCooldown;
        CurrentAmmo--;
        if (OnAmmoChanged != null) OnAmmoChanged(CurrentAmmo, magazineSize);

        Hitscan.Fire(gameObject,
                     playerCamera.transform.position,
                     playerCamera.transform.forward,
                     range, damage, spreadDegrees);
    }

    IEnumerator Reload()
    {
        IsReloading = true;
        yield return new WaitForSeconds(reloadTime);
        CurrentAmmo = magazineSize;
        if (OnAmmoChanged != null) OnAmmoChanged(CurrentAmmo, magazineSize);
        IsReloading = false;
    }
}
