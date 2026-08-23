using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// The player's pistol: instant-hit fire on left mouse, limited magazine, R to reload.
/// Owns the feedback for a shot too — recoil, muzzle flash, tracer, impact and sound.
/// </summary>
public class WeaponController : MonoBehaviour
{
    public Camera playerCamera;
    public WeaponView view;

    [Header("Ballistics")]
    public int damage = 25;
    public float range = 100f;
    public float spreadDegrees = 0.6f;

    [Header("Magazine")]
    public int magazineSize = 12;
    public float reloadTime = 1.5f;
    public float fireCooldown = 0.18f;

    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }

    /// <summary>Fired with (current, magazineSize) whenever ammo changes.</summary>
    public event Action<int, int> OnAmmoChanged;

    /// <summary>Fired when a shot damages someone. The flag marks a headshot.</summary>
    public event Action<bool> OnHitConfirmed;

    /// <summary>Fired when a shot kills someone.</summary>
    public event Action OnKillConfirmed;

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

        // Deliberately not Input.GetButton("Fire1"): Unity's default Input Manager
        // maps Fire1 to both the left mouse button and left Ctrl, so crouching
        // would fire the gun.
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
            Fire();
    }

    void Fire()
    {
        if (CurrentAmmo <= 0)
        {
            nextFireTime = Time.time + fireCooldown;
            if (GameAudio.Instance != null) GameAudio.Instance.PlayEmptyClick(transform.position);
            StartCoroutine(Reload());
            return;
        }

        nextFireTime = Time.time + fireCooldown;
        CurrentAmmo--;
        if (OnAmmoChanged != null) OnAmmoChanged(CurrentAmmo, magazineSize);

        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;

        ShotResult shot = Hitscan.Fire(gameObject, origin, direction, range, damage, spreadDegrees);

        PlayShotFeedback(shot);
    }

    void PlayShotFeedback(ShotResult shot)
    {
        if (view != null) view.PlayRecoil();

        Vector3 muzzlePosition = view != null && view.muzzle != null
            ? view.muzzle.position
            : playerCamera.transform.position + playerCamera.transform.forward * 0.4f;

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayPlayerShot(transform.position);

        if (GameEffects.Instance != null)
        {
            Transform attachTo = view != null && view.muzzle != null ? view.muzzle : null;
            GameEffects.Instance.MuzzleFlash(muzzlePosition, playerCamera.transform.forward, attachTo);
            GameEffects.Instance.Tracer(muzzlePosition, shot.point, new Color(1f, 0.92f, 0.6f));
        }

        if (!shot.hitSomething) return;

        if (shot.hitCharacter)
        {
            if (GameEffects.Instance != null) GameEffects.Instance.FleshImpact(shot.point, shot.normal);
            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.PlayFleshImpact(shot.point);
                GameAudio.Instance.PlayHitmarker(shot.wasHeadshot);
            }

            if (OnHitConfirmed != null) OnHitConfirmed(shot.wasHeadshot);
            if (shot.wasKill && OnKillConfirmed != null) OnKillConfirmed();
            return;
        }

        if (GameEffects.Instance != null) GameEffects.Instance.HardImpact(shot.point, shot.normal);
        if (GameAudio.Instance != null) GameAudio.Instance.PlayHardImpact(shot.point);
    }

    IEnumerator Reload()
    {
        IsReloading = true;
        if (view != null) view.SetReloading(true);

        if (GameAudio.Instance != null) GameAudio.Instance.PlayMagOut(transform.position);

        yield return new WaitForSeconds(reloadTime * 0.6f);
        if (GameAudio.Instance != null) GameAudio.Instance.PlayMagIn(transform.position);

        yield return new WaitForSeconds(reloadTime * 0.4f);

        CurrentAmmo = magazineSize;
        if (OnAmmoChanged != null) OnAmmoChanged(CurrentAmmo, magazineSize);

        if (view != null) view.SetReloading(false);
        IsReloading = false;
    }
}
