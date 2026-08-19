# Offline FPS Prototype (Pool map) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Playable offline Unity FPS prototype on one map ("Pool") — move, shoot, take damage, die, respawn. No networking, no Yandex SDK.

**Architecture:** Everything lives in one Unity scene (`Assets/Scenes/Pool.unity`), generated procedurally by an Editor script rather than hand-built in the Editor, so the whole prototype can be produced from code alone. Player is a single GameObject with `CharacterController` + four scripts (movement, health, weapon, UI). World geometry is primitive cubes/cylinders (no ProBuilder, no imported asset packs) — this keeps the whole prototype dependency-free and buildable without Unity Asset Store access, which matters because whoever runs this plan may not have Asset Store credentials configured.

**Tech Stack:** Unity (2022 LTS or Unity 6, Built-in Render Pipeline), C#, legacy Input Manager (`Input.GetAxis`/`GetButton` — do not add the new Input System package, it's an unnecessary dependency for this scope).

## Global Constraints

- Deviation from spec (`docs/superpowers/specs/2026-08-20-offline-prototype-design.md`): that spec named "Unity Starter Assets" and "ProBuilder" for the controller and map. This plan replaces both with hand-written CharacterController code and primitive geometry, because this plan is meant to be executed by a coding agent with no Unity Editor GUI interaction and no guaranteed Asset Store login — self-contained C# avoids that dependency entirely. End-user experience (WASD/sprint/jump/crouch/mouse-look, symmetric Pool map with Blue/Red spawns) is unchanged.
- No automated test framework — spec scopes testing as manual playtest only (Editor Play mode + WebGL browser build). Do not add NUnit/Unity Test Framework; it's out of scope.
- Render pipeline: Built-in (not URP/HDRP) — lighter for WebGL, matches spec's performance constraints.
- Everything under `Assets/Scripts/` except the map-builder, which is Editor-only and must live under `Assets/Editor/` (Unity excludes that folder from player builds automatically).
- Out of scope (do not implement): networking, Yandex SDK, second weapon, bots/AI, server browser. See spec for the full exclusion list.

---

### Task 1: Unity project setup

**Files:** none (Unity project creation, no code yet)

**Interfaces:** N/A — this task produces the empty project every later task adds files into.

- [ ] **Step 1: Create the project**

Open Unity Hub → New Project → **3D (Built-in Render Pipeline)** template → name it `PoolFPS` → location: `C:\Users\Folzy\Desktop\projects\ya_game\PoolFPS` (keep it inside the existing `ya_game` repo so it's version-controlled alongside the spec/plan docs already committed there).

- [ ] **Step 2: Switch build target to WebGL**

In Unity: `File > Build Settings` → select **WebGL** → `Switch Platform`. This can take a few minutes the first time (Unity downloads the WebGL module if missing — if the module isn't installed, install it via Unity Hub first: `Installs > (your version) > Add Modules > WebGL Build Support`).

- [ ] **Step 3: Commit the empty project**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
git add PoolFPS
git commit -m "Initialize Unity project (WebGL, Built-in RP)"
```

---

### Task 2: PlayerHealth script

**Files:**
- Create: `PoolFPS/Assets/Scripts/Player/PlayerHealth.cs`

**Interfaces:**
- Produces: `PlayerHealth` — public fields `int maxHealth`, `int currentHealth`, `Transform spawnPoint`; public method `TakeDamage(int amount)`; public events `Action<int,int> OnHealthChanged` (current, max), `Action OnDied`.
- Consumes: `FirstPersonController` (Task 4) and `WeaponController` (Task 5) components on the same GameObject, disabled/enabled on death/respawn — written to compile even before those files exist (uses `GetComponent<T>()`, no compile-time dependency issue since both scripts get added in this same plan).

- [ ] **Step 1: Write the script**

```csharp
using UnityEngine;
using System;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    public float respawnDelay = 3f;
    public Transform spawnPoint;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    CharacterController controller;

    void Awake()
    {
        currentHealth = maxHealth;
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // DEBUG: manual test hook for death/respawn flow (single-player prototype
        // has no other damage source yet). Remove once multiplayer damage exists.
        if (Input.GetKeyDown(KeyCode.H))
            TakeDamage(34);
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth == 0) Die();
    }

    void Die()
    {
        OnDied?.Invoke();
        SetControlsEnabled(false);
        Invoke(nameof(Respawn), respawnDelay);
    }

    void Respawn()
    {
        currentHealth = maxHealth;
        if (spawnPoint != null)
        {
            controller.enabled = false;
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
            controller.enabled = true;
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        SetControlsEnabled(true);
    }

    void SetControlsEnabled(bool value)
    {
        var fps = GetComponent<FirstPersonController>();
        if (fps != null) fps.enabled = value;
        var weapon = GetComponent<WeaponController>();
        if (weapon != null) weapon.enabled = value;
    }
}
```

- [ ] **Step 2: Verify it compiles**

Save the file, switch to Unity — check the Console panel shows no red compiler errors (it will show errors referencing `FirstPersonController`/`WeaponController` only if those class names are misspelled elsewhere; at this point in the plan those classes don't exist yet, which is fine — `GetComponent<T>()` compiles against any class name Unity can resolve once the other scripts exist, so this file alone won't error).

- [ ] **Step 3: Commit**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
git add PoolFPS/Assets/Scripts/Player/PlayerHealth.cs
git commit -m "Add PlayerHealth: damage, death, respawn"
```

---

### Task 3: TargetDummy script

**Files:**
- Create: `PoolFPS/Assets/Scripts/World/TargetDummy.cs`

**Interfaces:**
- Produces: `TargetDummy` — public method `TakeDamage(int amount)`.
- Consumes: nothing from other tasks.

- [ ] **Step 1: Write the script**

```csharp
using UnityEngine;
using System.Collections;

public class TargetDummy : MonoBehaviour
{
    public int maxHealth = 100;
    int currentHealth;
    Renderer rend;
    Color originalColor;

    void Awake()
    {
        currentHealth = maxHealth;
        rend = GetComponent<Renderer>();
        if (rend != null) originalColor = rend.material.color;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (rend != null) StartCoroutine(FlashRed());
        if (currentHealth <= 0) Respawn();
    }

    IEnumerator FlashRed()
    {
        rend.material.color = Color.red;
        yield return new WaitForSeconds(0.15f);
        rend.material.color = originalColor;
    }

    void Respawn()
    {
        currentHealth = maxHealth;
    }
}
```

- [ ] **Step 2: Verify it compiles** (Console has no red errors)

- [ ] **Step 3: Commit**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
git add PoolFPS/Assets/Scripts/World/TargetDummy.cs
git commit -m "Add TargetDummy: static shootable test target"
```

---

### Task 4: FirstPersonController script

**Files:**
- Create: `PoolFPS/Assets/Scripts/Player/FirstPersonController.cs`

**Interfaces:**
- Produces: `FirstPersonController` — `[RequireComponent(typeof(CharacterController))]`; public fields `float walkSpeed, sprintSpeed, crouchSpeed, jumpHeight, gravity, mouseSensitivity, standHeight, crouchHeight`; public field `Transform cameraTransform` (must be assigned by whoever instantiates the player — Task 7 does this).
- Consumes: `CharacterController` (built-in Unity component).

- [ ] **Step 1: Write the script**

```csharp
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.5f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    public float mouseSensitivity = 2f;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public Transform cameraTransform;

    CharacterController controller;
    Vector3 velocity;
    float pitch;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);
        pitch = Mathf.Clamp(pitch - mouseY, -85f, 85f);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMove()
    {
        bool isCrouching = Input.GetKey(KeyCode.LeftControl);
        controller.height = isCrouching ? crouchHeight : standHeight;

        float speed = isCrouching
            ? crouchSpeed
            : (Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed);

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;

        if (controller.isGrounded)
        {
            if (velocity.y < 0f) velocity.y = -2f;
            if (Input.GetKeyDown(KeyCode.Space) && !isCrouching)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move((move * speed + Vector3.up * velocity.y) * Time.deltaTime);
    }
}
```

- [ ] **Step 2: Verify it compiles** (Console has no red errors)

- [ ] **Step 3: Commit**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
git add PoolFPS/Assets/Scripts/Player/FirstPersonController.cs
git commit -m "Add FirstPersonController: WASD, sprint, jump, crouch, mouse look"
```

---

### Task 5: WeaponController script

**Files:**
- Create: `PoolFPS/Assets/Scripts/Player/WeaponController.cs`

**Interfaces:**
- Produces: `WeaponController` — public fields `Camera playerCamera, int damage, float range, int maxAmmo, int currentAmmo, float reloadTime, float fireCooldown`; public event `Action<int,int> OnAmmoChanged` (current, max).
- Consumes: `PlayerHealth.TakeDamage(int)` (Task 2) and `TargetDummy.TakeDamage(int)` (Task 3) on whatever the raycast hits.

- [ ] **Step 1: Write the script**

```csharp
using UnityEngine;
using System;
using System.Collections;

public class WeaponController : MonoBehaviour
{
    public Camera playerCamera;
    public int damage = 25;
    public float range = 100f;
    public int maxAmmo = 12;
    public int currentAmmo;
    public float reloadTime = 1.5f;
    public float fireCooldown = 0.25f;

    bool isReloading;
    float nextFireTime;

    public event Action<int, int> OnAmmoChanged;

    void Awake()
    {
        currentAmmo = maxAmmo;
    }

    void Update()
    {
        if (isReloading) return;

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Input.GetButtonDown("Fire1") && Time.time >= nextFireTime)
        {
            Fire();
        }
    }

    void Fire()
    {
        if (currentAmmo <= 0) return;
        nextFireTime = Time.time + fireCooldown;
        currentAmmo--;
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, range))
        {
            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health != null) health.TakeDamage(damage);

            var dummy = hit.collider.GetComponentInParent<TargetDummy>();
            if (dummy != null) dummy.TakeDamage(damage);
        }
    }

    IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        isReloading = false;
    }
}
```

- [ ] **Step 2: Verify it compiles** (Console has no red errors)

- [ ] **Step 3: Commit**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
git add PoolFPS/Assets/Scripts/Player/WeaponController.cs
git commit -m "Add WeaponController: hitscan pistol, ammo, reload"
```

---

### Task 6: PlayerUI script

**Files:**
- Create: `PoolFPS/Assets/Scripts/Player/PlayerUI.cs`

**Interfaces:**
- Produces: `PlayerUI` — public fields `PlayerHealth health, WeaponController weapon` (assigned by Task 7). Builds its own Canvas/Slider/Text at runtime in `Start()` — no manual Editor UI setup needed.
- Consumes: `PlayerHealth.OnHealthChanged`, `WeaponController.OnAmmoChanged`.

- [ ] **Step 1: Write the script**

```csharp
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    public PlayerHealth health;
    public WeaponController weapon;

    Slider healthSlider;
    Text ammoText;

    void Start()
    {
        BuildUI();
        health.OnHealthChanged += UpdateHealth;
        weapon.OnAmmoChanged += UpdateAmmo;
        UpdateHealth(health.currentHealth, health.maxHealth);
        UpdateAmmo(weapon.currentAmmo, weapon.maxAmmo);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("HUDCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var crosshairGO = new GameObject("Crosshair");
        crosshairGO.transform.SetParent(canvasGO.transform, false);
        var crosshairImg = crosshairGO.AddComponent<Image>();
        crosshairImg.color = Color.white;
        var crt = crosshairGO.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(4, 4);
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;

        var sliderGO = new GameObject("HealthSlider");
        sliderGO.transform.SetParent(canvasGO.transform, false);
        healthSlider = sliderGO.AddComponent<Slider>();
        var sliderRt = sliderGO.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0f, 0f);
        sliderRt.anchorMax = new Vector2(0f, 0f);
        sliderRt.pivot = new Vector2(0f, 0f);
        sliderRt.anchoredPosition = new Vector2(20, 20);
        sliderRt.sizeDelta = new Vector2(200, 20);

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.sizeDelta = Vector2.zero;

        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRt = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero; fillAreaRt.anchorMax = Vector2.one; fillAreaRt.sizeDelta = Vector2.zero;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = Color.red;
        var fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one; fillRt.sizeDelta = Vector2.zero;

        healthSlider.fillRect = fillRt;
        healthSlider.targetGraphic = fillImg;
        healthSlider.minValue = 0;
        healthSlider.maxValue = 100;

        var ammoGO = new GameObject("AmmoText");
        ammoGO.transform.SetParent(canvasGO.transform, false);
        ammoText = ammoGO.AddComponent<Text>();
        ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ammoText.fontSize = 24;
        ammoText.alignment = TextAnchor.LowerRight;
        ammoText.color = Color.white;
        var ammoRt = ammoGO.GetComponent<RectTransform>();
        ammoRt.anchorMin = new Vector2(1f, 0f);
        ammoRt.anchorMax = new Vector2(1f, 0f);
        ammoRt.pivot = new Vector2(1f, 0f);
        ammoRt.anchoredPosition = new Vector2(-20, 20);
        ammoRt.sizeDelta = new Vector2(150, 30);
    }

    void UpdateHealth(int current, int max)
    {
        healthSlider.maxValue = max;
        healthSlider.value = current;
    }

    void UpdateAmmo(int current, int max)
    {
        ammoText.text = current + " / " + max;
    }
}
```

- [ ] **Step 2: Verify it compiles** (Console has no red errors)

- [ ] **Step 3: Commit**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
git add PoolFPS/Assets/Scripts/Player/PlayerUI.cs
git commit -m "Add PlayerUI: runtime-built crosshair, HP bar, ammo counter"
```

---

### Task 7: Pool map builder (Editor script) + generate the scene

**Files:**
- Create: `PoolFPS/Assets/Editor/PoolMapBuilder.cs`

**Interfaces:**
- Consumes: `FirstPersonController` (Task 4), `PlayerHealth` (Task 2), `WeaponController` (Task 5), `PlayerUI` (Task 6), `TargetDummy` (Task 3) — attaches all of them to a generated `Player` GameObject.
- Produces: `Assets/Scenes/Pool.unity` (written when the menu command below is run inside the Editor, not by this script's source code alone).

- [ ] **Step 1: Write the script**

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PoolMapBuilder
{
    [MenuItem("Tools/Pool FPS/Build Pool Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildGround();
        BuildWalls();
        BuildPool();
        var blueSpawn = CreateSpawn("BlueSpawn", new Vector3(-15, 1, 0), Color.blue);
        CreateSpawn("RedSpawn", new Vector3(15, 1, 0), Color.red);
        BuildLight();
        BuildDummies();
        BuildPlayer(blueSpawn);

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Pool.unity");
        Debug.Log("Pool scene built at Assets/Scenes/Pool.unity");
    }

    static void BuildGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -0.5f, 0);
        ground.transform.localScale = new Vector3(40, 1, 24);
    }

    static void BuildWalls()
    {
        CreateWall("Wall_North", new Vector3(0, 2, 12), new Vector3(40, 4, 1));
        CreateWall("Wall_South", new Vector3(0, 2, -12), new Vector3(40, 4, 1));
        CreateWall("Wall_East", new Vector3(20, 2, 0), new Vector3(1, 4, 24));
        CreateWall("Wall_West", new Vector3(-20, 2, 0), new Vector3(1, 4, 24));
    }

    static void CreateWall(string name, Vector3 pos, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
    }

    static void BuildPool()
    {
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pool.name = "PoolBasin";
        pool.transform.position = new Vector3(0, -0.6f, 0);
        pool.transform.localScale = new Vector3(10, 0.8f, 8);
        var rend = pool.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.1f, 0.4f, 0.8f);
        rend.sharedMaterial = mat;
    }

    static Transform CreateSpawn(string name, Vector3 pos, Color color)
    {
        var go = new GameObject(name);
        go.transform.position = pos;

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = name + "_Marker";
        marker.transform.SetParent(go.transform);
        marker.transform.localPosition = Vector3.zero;
        marker.transform.localScale = new Vector3(2f, 0.05f, 2f);
        var rend = marker.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        rend.sharedMaterial = mat;
        Object.DestroyImmediate(marker.GetComponent<CapsuleCollider>());

        return go.transform;
    }

    static void BuildLight()
    {
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
    }

    static void BuildDummies()
    {
        CreateDummy(new Vector3(-5, 1, 3));
        CreateDummy(new Vector3(5, 1, -3));
    }

    static void CreateDummy(Vector3 pos)
    {
        var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        dummy.name = "TargetDummy";
        dummy.transform.position = pos;
        dummy.AddComponent<TargetDummy>();
    }

    static void BuildPlayer(Transform spawn)
    {
        var player = new GameObject("Player");
        player.transform.position = spawn.position;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.center = new Vector3(0, 0.9f, 0);

        var camGO = new GameObject("PlayerCamera");
        camGO.transform.SetParent(player.transform);
        camGO.transform.localPosition = new Vector3(0, 1.6f, 0);
        var cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();

        var fps = player.AddComponent<FirstPersonController>();
        fps.cameraTransform = camGO.transform;

        var health = player.AddComponent<PlayerHealth>();
        health.spawnPoint = spawn;

        var weapon = player.AddComponent<WeaponController>();
        weapon.playerCamera = cam;

        var ui = player.AddComponent<PlayerUI>();
        ui.health = health;
        ui.weapon = weapon;
    }
}
```

- [ ] **Step 2: Verify it compiles** (Console has no red errors)

- [ ] **Step 3: Run the menu command to generate the scene**

In Unity: `Tools > Pool FPS > Build Pool Scene`. Confirm the Console logs `Pool scene built at Assets/Scenes/Pool.unity` and the Hierarchy panel shows `Ground`, four `Wall_*`, `PoolBasin`, `BlueSpawn`, `RedSpawn`, `Directional Light`, two `TargetDummy`, and `Player`.

- [ ] **Step 4: Commit**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
git add PoolFPS/Assets/Editor/PoolMapBuilder.cs PoolFPS/Assets/Scenes/Pool.unity
git commit -m "Add Pool map builder, generate Pool.unity scene"
```

---

### Task 8: Manual playtest (Editor Play mode)

**Files:** none — verification only.

- [ ] **Step 1: Open `Assets/Scenes/Pool.unity` and press Play**

- [ ] **Step 2: Verify movement**

WASD moves the player; Shift sprints faster than default walk; Space jumps; Ctrl crouches (camera height visibly drops). Mouse moves the camera look direction (pitch clamped, doesn't flip past vertical).

- [ ] **Step 3: Verify shooting**

Left-click fires at the crosshair. Aim at a `TargetDummy` capsule and click — it should flash red. Ammo counter (bottom-right) decrements per shot; pressing R with ammo below max plays out a ~1.5s reload then resets ammo to max; firing at 0 ammo does nothing until reloaded.

- [ ] **Step 4: Verify damage/death/respawn**

Press H (debug key) repeatedly — HP bar (bottom-left) drops; at 0 HP, movement and shooting stop, and after ~3 seconds the player teleports back to `BlueSpawn` with HP bar full again and controls re-enabled.

- [ ] **Step 5: Fix any issues found, re-test, then commit if anything changed**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
git add PoolFPS
git commit -m "Fix issues found during playtest"
```

(Skip the commit if nothing needed changing.)

---

### Task 9: WebGL build and browser check

**Files:** none — build + verification only.

- [ ] **Step 1: Build**

`File > Build Settings` → confirm platform is **WebGL** and `Assets/Scenes/Pool.unity` is the only scene in the build list (checkbox it if not already, via `Add Open Scenes` while the scene is open) → `Build` → choose an output folder, e.g. `PoolFPS/Build/WebGL`.

- [ ] **Step 2: Serve and open in a browser**

WebGL builds must be served over HTTP, not opened as a local `file://` path. From the build output folder:

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game\PoolFPS\Build\WebGL"
python -m http.server 8000
```

Open `http://localhost:8000` in Chrome and in Yandex Browser. Repeat the Task 8 checklist (movement, shooting, damage/death/respawn) in both.

- [ ] **Step 3: Check performance**

Open the browser's dev tools performance/FPS counter (or Unity's in-build stats if enabled) while moving/shooting. Target: 60 FPS on the machine you're testing on; if it's a low-end/integrated-graphics machine, 30–45 FPS is acceptable per spec.

- [ ] **Step 4: Note the build output size**

Check the total size of `PoolFPS/Build/WebGL` — Yandex Games has load-time expectations for WebGL builds; a bloated build hurts retention (and therefore ad revenue). No hard number to hit at this prototype stage, just note it for comparison once real art assets are added later.

- [ ] **Step 5: Commit build config only (not the build output itself)**

```bash
cd "C:\Users\Folzy\Desktop\projects\ya_game"
echo "PoolFPS/Build/" >> .gitignore
echo "PoolFPS/Library/" >> .gitignore
echo "PoolFPS/Temp/" >> .gitignore
echo "PoolFPS/obj/" >> .gitignore
git add .gitignore
git commit -m "Ignore Unity build/cache output"
```

---

## Definition of done

All nine tasks checked off, `Assets/Scenes/Pool.unity` exists and matches the spec's acceptance criterion: run around the Pool map, shoot a target, see HP/ammo UI update, kill yourself via the debug key, respawn at your spawn point — in both the Editor and a WebGL build opened in a browser.
