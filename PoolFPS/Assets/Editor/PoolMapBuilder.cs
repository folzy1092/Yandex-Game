using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Pool arena scene from primitives. Generating the level from code
/// means the whole map can be changed, reviewed and re-created without any
/// manual editor work.
///
/// Layout, seen from above (40 x 24 metres):
///
///     +--------------------------------------+
///     |          |   north   |               |
///     |   west   +-----------+     east      |
///     |   side   |   POOL    |     side      |
///     |          +-----------+               |
///     |          |   south   |               |
///     +--------------------------------------+
///
/// The pool is a shallow pit in the middle with a ramp at each end so bots,
/// which cannot jump, are always able to walk back out.
/// </summary>
public static class PoolMapBuilder
{
    const float ArenaWidth = 40f;   // along X
    const float ArenaDepth = 24f;   // along Z
    const float WallHeight = 5f;

    const float PitMinX = -5f;
    const float PitMaxX = 5f;
    const float PitMinZ = -4f;
    const float PitMaxZ = 4f;
    const float PitDepth = 1f;      // shallower than the player's jump height

    const float SlabThickness = 3f;

    [MenuItem("Tools/Pool FPS/2 - Build Pool Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        BuildGround();
        BuildWalls();
        BuildPool();
        BuildCover();

        List<Transform> spawns = BuildSpawnPoints();
        BuildManagers(spawns);
        BuildPlayer(spawns[0]);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Pool.unity");
        Debug.Log("Pool FPS: arena saved to Assets/Scenes/Pool.unity");
    }

    // ---------- environment ----------

    static void BuildLighting()
    {
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.color = new Color(1f, 0.97f, 0.90f);
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.23f);
    }

    /// <summary>
    /// The floor is four slabs arranged around the pit rather than one big plane,
    /// which is what creates the hole the pool sits in.
    /// </summary>
    static void BuildGround()
    {
        Material floor = GeneratedMaterials.Load("Mat_Floor");

        float halfWidth = ArenaWidth * 0.5f;
        float halfDepth = ArenaDepth * 0.5f;
        float slabCentreY = -SlabThickness * 0.5f;

        float westWidth = PitMinX + halfWidth;
        CreateBox("Floor_West", new Vector3(-halfWidth + westWidth * 0.5f, slabCentreY, 0f),
                  new Vector3(westWidth, SlabThickness, ArenaDepth), floor);

        float eastWidth = halfWidth - PitMaxX;
        CreateBox("Floor_East", new Vector3(halfWidth - eastWidth * 0.5f, slabCentreY, 0f),
                  new Vector3(eastWidth, SlabThickness, ArenaDepth), floor);

        float northDepth = halfDepth - PitMaxZ;
        CreateBox("Floor_North", new Vector3(0f, slabCentreY, halfDepth - northDepth * 0.5f),
                  new Vector3(PitMaxX - PitMinX, SlabThickness, northDepth), floor);

        float southDepth = PitMinZ + halfDepth;
        CreateBox("Floor_South", new Vector3(0f, slabCentreY, -halfDepth + southDepth * 0.5f),
                  new Vector3(PitMaxX - PitMinX, SlabThickness, southDepth), floor);
    }

    static void BuildWalls()
    {
        Material wall = GeneratedMaterials.Load("Mat_Wall");

        float halfWidth = ArenaWidth * 0.5f;
        float halfDepth = ArenaDepth * 0.5f;
        float centreY = WallHeight * 0.5f;

        CreateBox("Wall_North", new Vector3(0f, centreY, halfDepth + 0.5f),
                  new Vector3(ArenaWidth + 2f, WallHeight, 1f), wall);
        CreateBox("Wall_South", new Vector3(0f, centreY, -halfDepth - 0.5f),
                  new Vector3(ArenaWidth + 2f, WallHeight, 1f), wall);
        CreateBox("Wall_East", new Vector3(halfWidth + 0.5f, centreY, 0f),
                  new Vector3(1f, WallHeight, ArenaDepth), wall);
        CreateBox("Wall_West", new Vector3(-halfWidth - 0.5f, centreY, 0f),
                  new Vector3(1f, WallHeight, ArenaDepth), wall);
    }

    static void BuildPool()
    {
        Material tile = GeneratedMaterials.Load("Mat_PoolTile");
        Material water = GeneratedMaterials.Load("Mat_Water");

        float pitWidth = PitMaxX - PitMinX;
        float pitDepthZ = PitMaxZ - PitMinZ;

        CreateBox("Pool_Bottom", new Vector3(0f, -PitDepth - 0.5f, 0f),
                  new Vector3(pitWidth, 1f, pitDepthZ), tile);

        // Ramps let the bots walk out of the pool; the player can also just jump.
        const float rampRun = 2.5f;
        float rampAngle = Mathf.Atan2(PitDepth, rampRun) * Mathf.Rad2Deg;
        float rampLength = Mathf.Sqrt(rampRun * rampRun + PitDepth * PitDepth) + 0.4f;

        CreateBox("Pool_RampWest",
                  new Vector3(PitMinX + rampRun * 0.5f, -PitDepth * 0.5f, 0f),
                  new Vector3(rampLength, 0.3f, pitDepthZ), tile,
                  Quaternion.Euler(0f, 0f, -rampAngle));

        CreateBox("Pool_RampEast",
                  new Vector3(PitMaxX - rampRun * 0.5f, -PitDepth * 0.5f, 0f),
                  new Vector3(rampLength, 0.3f, pitDepthZ), tile,
                  Quaternion.Euler(0f, 0f, rampAngle));

        // Water is decoration only — no collider, so you wade through it.
        var surface = CreateBox("Pool_Water", new Vector3(0f, -PitDepth + 0.35f, 0f),
                                new Vector3(pitWidth - 0.1f, 0.06f, pitDepthZ - 0.1f), water);
        Object.DestroyImmediate(surface.GetComponent<Collider>());
    }

    static void BuildCover()
    {
        Material crate = GeneratedMaterials.Load("Mat_Crate");
        Material wall = GeneratedMaterials.Load("Mat_Wall");

        Vector3[] cratePositions =
        {
            new Vector3(-14f, 0.75f, 6f),
            new Vector3(-14f, 0.75f, -6f),
            new Vector3(14f, 0.75f, 6f),
            new Vector3(14f, 0.75f, -6f),
            new Vector3(-8f, 0.75f, 0f),
            new Vector3(8f, 0.75f, 0f),
            new Vector3(0f, 0.75f, 9f),
            new Vector3(0f, 0.75f, -9f)
        };

        for (int i = 0; i < cratePositions.Length; i++)
        {
            CreateBox("Crate_" + i, cratePositions[i], new Vector3(1.5f, 1.5f, 1.5f), crate,
                      Quaternion.Euler(0f, i * 17f, 0f));
        }

        Vector3[] pillarPositions =
        {
            new Vector3(-10f, 2.5f, 10f),
            new Vector3(10f, 2.5f, 10f),
            new Vector3(-10f, 2.5f, -10f),
            new Vector3(10f, 2.5f, -10f)
        };

        for (int i = 0; i < pillarPositions.Length; i++)
            CreateBox("Pillar_" + i, pillarPositions[i], new Vector3(1.2f, 5f, 1.2f), wall);
    }

    static List<Transform> BuildSpawnPoints()
    {
        Material marker = GeneratedMaterials.Load("Mat_SpawnMarker");

        Vector3[] positions =
        {
            new Vector3(-17f, 0.2f, 9f),
            new Vector3(17f, 0.2f, -9f),
            new Vector3(-17f, 0.2f, -9f),
            new Vector3(17f, 0.2f, 9f),
            new Vector3(0f, 0.2f, 10.5f),
            new Vector3(0f, 0.2f, -10.5f)
        };

        var spawns = new List<Transform>();
        var root = new GameObject("SpawnPoints");

        for (int i = 0; i < positions.Length; i++)
        {
            var spawn = new GameObject("Spawn_" + i);
            spawn.transform.SetParent(root.transform);
            spawn.transform.position = positions[i];
            // Face the middle of the arena so a fresh spawn looks at the action.
            spawn.transform.rotation = Quaternion.LookRotation(
                new Vector3(-positions[i].x, 0f, -positions[i].z).normalized, Vector3.up);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Marker";
            disc.transform.SetParent(spawn.transform, false);
            disc.transform.localPosition = new Vector3(0f, -0.19f, 0f);
            disc.transform.localScale = new Vector3(1.8f, 0.02f, 1.8f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            if (marker != null) disc.GetComponent<Renderer>().sharedMaterial = marker;

            spawns.Add(spawn.transform);
        }

        return spawns;
    }

    // ---------- gameplay objects ----------

    static void BuildManagers(List<Transform> spawns)
    {
        var managers = new GameObject("Managers");

        var spawnManager = managers.AddComponent<SpawnManager>();
        spawnManager.spawnPoints = spawns;

        managers.AddComponent<MatchManager>();
        managers.AddComponent<BotSpawner>();
    }

    static void BuildPlayer(Transform spawn)
    {
        Material bodyMaterial = GeneratedMaterials.Load("Mat_Player");

        var player = new GameObject("Игрок");
        player.transform.position = spawn.position;
        player.transform.rotation = spawn.rotation;

        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.slopeLimit = 50f;
        controller.stepOffset = 0.4f;

        var cameraGO = new GameObject("PlayerCamera");
        cameraGO.transform.SetParent(player.transform, false);
        cameraGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var camera = cameraGO.AddComponent<Camera>();
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        cameraGO.AddComponent<AudioListener>();

        // A body so the bots have something to shoot at. Standard shader culls
        // back faces, so it is invisible from inside in first person.
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(player.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        if (bodyMaterial != null) body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

        var fps = player.AddComponent<FirstPersonController>();
        fps.cameraTransform = cameraGO.transform;

        var weapon = player.AddComponent<WeaponController>();
        weapon.playerCamera = camera;

        var health = player.AddComponent<Health>();
        health.maxHealth = 100;
        health.respawnDelay = 3f;
        health.disableOnDeath = new MonoBehaviour[] { fps, weapon };

        player.AddComponent<CursorRelease>();

        var playerHUD = player.AddComponent<PlayerHUD>();
        playerHUD.health = health;
        playerHUD.weapon = weapon;

        var matchHUD = player.AddComponent<MatchHUD>();
        matchHUD.player = player;
    }

    // ---------- helpers ----------

    static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material)
    {
        return CreateBox(name, position, scale, material, Quaternion.identity);
    }

    static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material,
                                Quaternion rotation)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = position;
        box.transform.rotation = rotation;
        box.transform.localScale = scale;

        if (material != null)
            box.GetComponent<Renderer>().sharedMaterial = material;

        return box;
    }
}
