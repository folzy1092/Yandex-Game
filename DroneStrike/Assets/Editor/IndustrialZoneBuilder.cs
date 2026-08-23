using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the "Industrial Zone" mission scene: rolling ground, a fenced
/// compound of warehouses, scattered woodland, and the targets to destroy.
///
/// The compound sits away from the launch point, so the first thing the pilot
/// does is fly — long enough to learn the controls before anything is at stake,
/// short enough not to be boring.
///
///     launch ●
///             ╲
///              ╲  ~180 m over open ground and trees
///               ╲
///                ▣▣▣  compound: warehouses, vehicles, depot, antenna
/// </summary>
public static class IndustrialZoneBuilder
{
    const float MapSize = 700f;
    const int TerrainResolution = 129;
    const float HillAmplitude = 34f;
    const int TerrainSeed = 20260823;

    /// <summary>Ground inside this radius of the centre is levelled for the compound.</summary>
    const float FlatRadius = 110f;

    static TargetProps.Palette palette;

    [MenuItem("Tools/Drone Strike/2 - Build Industrial Zone")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        palette = LoadPalette();

        BuildLighting();
        BuildTerrain();
        BuildCompound();
        BuildWoodland();

        Transform launch = BuildLaunchPoint();
        BuildManagers(launch);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/IndustrialZone.unity");
        Debug.Log("Drone Strike: mission saved to Assets/Scenes/IndustrialZone.unity");
    }

    static TargetProps.Palette LoadPalette()
    {
        return new TargetProps.Palette
        {
            vehicle = DroneMaterials.Load("Mat_Vehicle"),
            vehicleDark = DroneMaterials.Load("Mat_VehicleDark"),
            crate = DroneMaterials.Load("Mat_Crate"),
            concrete = DroneMaterials.Load("Mat_Concrete"),
            metal = DroneMaterials.Load("Mat_RustMetal"),
            roof = DroneMaterials.Load("Mat_Roof")
        };
    }

    // ---------- environment ----------

    static void BuildLighting()
    {
        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.25f;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.6f;
        sunGO.transform.rotation = Quaternion.Euler(48f, 30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.64f, 0.76f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.50f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.28f, 0.24f);

        // Light haze on the horizon: it gives the open map a sense of scale and
        // hides the edge of the terrain mesh.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.68f, 0.74f, 0.82f);
        RenderSettings.fogStartDistance = 220f;
        RenderSettings.fogEndDistance = 640f;
    }

    static void BuildTerrain()
    {
        var ground = new GameObject("Terrain");

        Mesh mesh = TerrainMesh.Build(MapSize, TerrainResolution, HillAmplitude,
                                      TerrainSeed, FlatRadius);

        var filter = ground.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = ground.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = DroneMaterials.Load("Mat_Ground");

        var collider = ground.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    /// <summary>Height of the generated ground at a point, for placing props on it.</summary>
    static float GroundAt(float x, float z)
    {
        float height = TerrainMesh.SampleHeight(x, z, HillAmplitude, TerrainSeed);

        float distance = new Vector2(x, z).magnitude;
        if (distance < FlatRadius * 2f)
        {
            float blend = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(FlatRadius, FlatRadius * 2f, distance));
            height *= blend;
        }

        return height;
    }

    static Vector3 OnGround(float x, float z, float lift = 0f)
    {
        return new Vector3(x, GroundAt(x, z) + lift, z);
    }

    // ---------- the compound ----------

    static void BuildCompound()
    {
        var group = new GameObject("Compound");

        BuildApron(group.transform);
        BuildFence(group.transform);
        BuildBuildings(group.transform);
        BuildPatrolRoad(group.transform);
        targetCount = BuildTargets(group.transform);
    }

    /// <summary>
    /// The rectangular loop two of the trucks drive, marked with a concrete curb
    /// so it reads as a road rather than just open apron. Kept well inside the
    /// fence line so a patrolling truck never clips through it mid-turn.
    /// </summary>
    static readonly Vector3[] PatrolWaypoints =
    {
        new Vector3(-50f, 0f, -38f),
        new Vector3(50f, 0f, -38f),
        new Vector3(50f, 0f, 38f),
        new Vector3(-50f, 0f, 38f)
    };

    static void BuildPatrolRoad(Transform parent)
    {
        var group = new GameObject("PatrolRoad");
        group.transform.SetParent(parent, false);

        Material concrete = DroneMaterials.Load("Mat_Concrete");

        for (int i = 0; i < PatrolWaypoints.Length; i++)
        {
            Vector3 from = PatrolWaypoints[i];
            Vector3 to = PatrolWaypoints[(i + 1) % PatrolWaypoints.Length];

            Vector3 direction = (to - from).normalized;
            float length = Vector3.Distance(from, to);
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            foreach (float side in new[] { -3.2f, 3.2f })
            {
                Vector3 offset = Quaternion.Euler(0f, 90f, 0f) * direction * side;
                Vector3 midpoint = OnGround((from.x + to.x) * 0.5f + offset.x,
                                            (from.z + to.z) * 0.5f + offset.z, 0.08f);

                var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                curb.name = "Curb";
                curb.transform.SetParent(group.transform, false);
                curb.transform.position = midpoint;
                curb.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                curb.transform.localScale = new Vector3(0.3f, 0.12f, length + 6f);
                curb.GetComponent<Renderer>().sharedMaterial = concrete;
                Object.DestroyImmediate(curb.GetComponent<Collider>());
            }
        }
    }

    /// <summary>Patrol waypoints projected onto the ground, for handing to PatrolMover.</summary>
    static Vector3[] GroundedPatrolWaypoints()
    {
        var grounded = new Vector3[PatrolWaypoints.Length];
        for (int i = 0; i < PatrolWaypoints.Length; i++)
            grounded[i] = OnGround(PatrolWaypoints[i].x, PatrolWaypoints[i].z, 0.05f);
        return grounded;
    }

    /// <summary>Set by BuildCompound, read by BuildManagers when sizing the drone rack.</summary>
    static int targetCount;

    /// <summary>A concrete apron under the compound, so vehicles are not parked on grass.</summary>
    static void BuildApron(Transform parent)
    {
        var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
        apron.name = "Apron";
        apron.transform.SetParent(parent, false);
        apron.transform.position = OnGround(0f, 0f, 0.05f);
        apron.transform.localScale = new Vector3(130f, 0.4f, 110f);
        apron.GetComponent<Renderer>().sharedMaterial = DroneMaterials.Load("Mat_Asphalt");
    }

    static void BuildFence(Transform parent)
    {
        var group = new GameObject("Fence");
        group.transform.SetParent(parent, false);

        const float halfX = 66f;
        const float halfZ = 56f;
        const float postSpacing = 8f;

        BuildFenceRun(group.transform, new Vector3(-halfX, 0f, -halfZ), new Vector3(halfX, 0f, -halfZ), postSpacing);
        BuildFenceRun(group.transform, new Vector3(-halfX, 0f, halfZ), new Vector3(halfX, 0f, halfZ), postSpacing);
        BuildFenceRun(group.transform, new Vector3(-halfX, 0f, -halfZ), new Vector3(-halfX, 0f, halfZ), postSpacing);
        BuildFenceRun(group.transform, new Vector3(halfX, 0f, -halfZ), new Vector3(halfX, 0f, halfZ), postSpacing);
    }

    static void BuildFenceRun(Transform parent, Vector3 from, Vector3 to, float spacing)
    {
        Material metal = DroneMaterials.Load("Mat_RustMetal");

        float length = Vector3.Distance(from, to);
        int posts = Mathf.Max(2, Mathf.RoundToInt(length / spacing));
        Vector3 direction = (to - from).normalized;
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        for (int i = 0; i <= posts; i++)
        {
            Vector3 flat = Vector3.Lerp(from, to, (float)i / posts);
            Vector3 position = OnGround(flat.x, flat.z, 1.2f);

            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            post.transform.SetParent(parent, false);
            post.transform.position = position;
            post.transform.localScale = new Vector3(0.2f, 2.4f, 0.2f);
            post.GetComponent<Renderer>().sharedMaterial = metal;
        }

        // The mesh panel between the posts. One box for the whole run: a fence
        // is a wall as far as a drone is concerned.
        Vector3 midpoint = OnGround((from.x + to.x) * 0.5f, (from.z + to.z) * 0.5f, 1.1f);

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Panel";
        panel.transform.SetParent(parent, false);
        panel.transform.position = midpoint;
        panel.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        panel.transform.localScale = new Vector3(0.08f, 2.1f, length);
        panel.GetComponent<Renderer>().sharedMaterial = metal;
    }

    static void BuildBuildings(Transform parent)
    {
        var group = new GameObject("Buildings");
        group.transform.SetParent(parent, false);

        TargetProps.Warehouse(group.transform, OnGround(-38f, 26f), new Vector3(34f, 9f, 20f), 0f, palette);
        TargetProps.Warehouse(group.transform, OnGround(-38f, -18f), new Vector3(28f, 7f, 18f), 0f, palette);
        TargetProps.Warehouse(group.transform, OnGround(34f, 30f), new Vector3(22f, 8f, 26f), 90f, palette);
        TargetProps.Warehouse(group.transform, OnGround(44f, -24f), new Vector3(18f, 6f, 16f), 0f, palette);

        // Storage tanks: tall round landmarks among all the boxes. Kept south
        // of the patrol road (which runs along z = -38) rather than under it —
        // they used to sit right on the road line, which put them directly in
        // a patrolling truck's path.
        Material metal = DroneMaterials.Load("Mat_RustMetal");
        for (int i = 0; i < 3; i++)
        {
            var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = "Tank" + i;
            tank.transform.SetParent(group.transform, false);
            tank.transform.position = OnGround(6f + i * 13f, -48f, 5f);
            tank.transform.localScale = new Vector3(10f, 5f, 10f);
            tank.GetComponent<Renderer>().sharedMaterial = metal;
        }
    }

    /// <summary>
    /// The targets: eight of them, no two visible from the same spot at once.
    ///
    /// Most sit tucked against something — a warehouse wall, the treeline, the
    /// fence — rather than out in the open apron, the way real cover would
    /// actually be used rather than staged for a shooting gallery. Two of the
    /// trucks patrol the road loop instead of sitting parked, so the compound
    /// still has movement in it even before the first shot is fired.
    /// </summary>
    static int BuildTargets(Transform parent)
    {
        var group = new GameObject("Targets");
        group.transform.SetParent(parent, false);

        // Armour, tucked into the gaps between warehouses rather than sitting
        // in the open middle of the apron. Positions checked against every
        // building footprint by hand — there is no live editor to eyeball this
        // in, so getting it wrong here means a target embedded in a wall.
        TargetProps.ArmouredVehicle(group.transform, OnGround(-38f, 2f), 70f, palette);
        TargetProps.ArmouredVehicle(group.transform, OnGround(42f, -6f), -110f, palette);

        // One truck parked south of the buildings — an ambush, not a display.
        TargetProps.Truck(group.transform, OnGround(-10f, -45f), 20f, palette);

        // Two more running the patrol loop, starting from opposite corners so
        // they are never side by side.
        BuildPatrolTruck(group.transform, 0);
        BuildPatrolTruck(group.transform, 2);

        // Depots tucked against the fence, clear of the buildings and the road.
        TargetProps.SupplyDepot(group.transform, OnGround(-58f, -30f), 40f, palette);
        TargetProps.SupplyDepot(group.transform, OnGround(58f, 10f), -15f, palette);

        // The antenna stays the exposed landmark it always was.
        TargetProps.Antenna(group.transform, OnGround(54f, 44f), 0f, palette);

        return group.transform.childCount;
    }

    /// <summary>
    /// A truck placed at one corner of the patrol loop and set to drive it,
    /// starting from a different corner so the two patrol trucks are never
    /// side by side.
    /// </summary>
    static void BuildPatrolTruck(Transform parent, int startCorner)
    {
        Vector3[] waypoints = GroundedPatrolWaypoints();
        Vector3 start = waypoints[startCorner % waypoints.Length];

        Target target = TargetProps.Truck(parent, start, 0f, palette);

        var mover = target.gameObject.AddComponent<PatrolMover>();
        mover.waypoints = waypoints;
        mover.speed = 4f;
    }

    // ---------- woodland ----------

    /// <summary>
    /// Trees over the approach, thinning out near the compound. They give the
    /// flight in something to fly through and a sense of speed close to the
    /// ground, which open grass cannot.
    /// </summary>
    static void BuildWoodland()
    {
        var group = new GameObject("Woodland");

        Material trunk = DroneMaterials.Load("Mat_TreeTrunk");
        Material foliage = DroneMaterials.Load("Mat_Foliage");

        Random.State previous = Random.state;
        Random.InitState(TerrainSeed);

        const int attempts = 420;
        for (int i = 0; i < attempts; i++)
        {
            float x = Random.Range(-MapSize * 0.45f, MapSize * 0.45f);
            float z = Random.Range(-MapSize * 0.45f, MapSize * 0.45f);

            // Keep the compound and its immediate surroundings clear.
            if (new Vector2(x, z).magnitude < 95f) continue;

            TargetProps.Tree(group.transform, OnGround(x, z), Random.Range(0.8f, 1.6f), trunk, foliage);
        }

        Random.state = previous;
    }

    // ---------- gameplay objects ----------

    static Transform BuildLaunchPoint()
    {
        // South-west of the compound: far enough that the pilot has to fly to
        // the target, close enough to stay well inside signal range.
        var launch = new GameObject("LaunchPoint");
        launch.transform.position = OnGround(-120f, -140f, 2f);
        launch.transform.rotation = Quaternion.LookRotation(
            new Vector3(120f, 0f, 140f).normalized, Vector3.up);

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "Pad";
        pad.transform.SetParent(launch.transform, false);
        pad.transform.localPosition = new Vector3(0f, -1.9f, 0f);
        pad.transform.localScale = new Vector3(6f, 0.06f, 6f);
        pad.GetComponent<Renderer>().sharedMaterial = DroneMaterials.Load("Mat_Asphalt");
        Object.DestroyImmediate(pad.GetComponent<Collider>());

        return launch.transform;
    }

    static void BuildManagers(Transform launch)
    {
        var managers = new GameObject("Managers");
        managers.transform.position = launch.position;

        var mission = managers.AddComponent<MissionManager>();
        mission.launchPoint = launch;

        // A kamikaze drone clears roughly one target per run, so the rack has
        // to hold at least one per target plus a margin for crashes and misses.
        // A flat "3" here regardless of the map's actual target count is what
        // made the mission unwinnable — three drones cannot ever clear eight
        // targets no matter how well they are flown.
        mission.droneCount = Mathf.Max(4, Mathf.CeilToInt(targetCount * 1.3f));

        managers.AddComponent<DroneHUD>();
    }
}
