using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the "Forward Position" mission scene: rolling ground, a supply road
/// with a patrolled loop, dug-in vehicles and tents, and scattered woodland.
///
/// There are deliberately no buildings. A staging area behind a front line is
/// tents, earth berms, camouflage netting and a graded road — warehouses and
/// factories belong to a different game, and putting them here made the map
/// read as an industrial estate rather than a military position.
///
/// The position sits away from the launch point, so the first thing the pilot
/// does is fly — long enough to learn the controls before anything is at stake,
/// short enough not to be boring.
///
///     launch ●
///             ╲
///              ╲  ~180 m over open ground and trees
///               ╲
///                ═══  road loop: armour, trucks, tents, mast
/// </summary>
public static class IndustrialZoneBuilder
{
    const float MapSize = 700f;
    const int TerrainResolution = 129;
    const float HillAmplitude = 34f;
    const int TerrainSeed = 20260823;

    /// <summary>
    /// Ground inside this radius of the centre is levelled. It has to cover the
    /// whole road loop — a patrol truck driving a graded road up and down hills
    /// looks broken, and the corner of the loop is 121 m out.
    /// </summary>
    const float FlatRadius = 130f;

    static TargetProps.Palette palette;

    [MenuItem("Tools/Drone Strike/2 - Build Industrial Zone")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        palette = LoadPalette();
        claims.Clear();

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
        RenderSettings.fogStartDistance = 260f;
        RenderSettings.fogEndDistance = 680f;
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

    // ---------- placement bookkeeping ----------

    /// <summary>
    /// Ground already spoken for, as circles on the XZ plane.
    ///
    /// There is no editor to eyeball this map in, so every overlap has to be
    /// caught arithmetically or it ships. Everything placed registers the
    /// footprint it occupies; everything placed afterwards checks against the
    /// list. That is what stops a scattered fuel drum landing inside a tank, a
    /// tree growing through the road, or a target ending up somewhere the
    /// patrol drives straight over.
    /// </summary>
    static readonly List<Vector3> claims = new List<Vector3>();   // x, z, radius

    static void Claim(float x, float z, float radius)
    {
        claims.Add(new Vector3(x, z, radius));
    }

    static bool IsFree(float x, float z, float radius)
    {
        foreach (Vector3 claim in claims)
        {
            float gap = new Vector2(x - claim.x, z - claim.y).magnitude;
            if (gap < radius + claim.z) return false;
        }

        return true;
    }

    /// <summary>
    /// Places a claim at a hand-picked position, complaining loudly rather than
    /// silently overlapping. A warning in the console during the build is worth
    /// far more than a target found embedded in a truck during play.
    /// </summary>
    static void ClaimChecked(string what, float x, float z, float radius)
    {
        if (!IsFree(x, z, radius))
            Debug.LogWarning("Drone Strike: " + what + " at (" + x + ", " + z
                             + ") overlaps something already placed.");

        Claim(x, z, radius);
    }

    /// <summary>
    /// Claims a long thin object as a chain of small circles down its length
    /// rather than one circle around the whole thing.
    ///
    /// A single circle sized to a thirty-metre berm reserves a fifteen-metre
    /// radius of ground, most of which the berm is nowhere near — and since the
    /// whole point of a berm is to stand a few metres from the vehicle it
    /// shields, that one circle would reject every layout worth having.
    /// </summary>
    static void ClaimLine(string what, float x, float z, float yaw, float length, float halfWidth)
    {
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        int steps = Mathf.Max(2, Mathf.CeilToInt(length / halfWidth));

        // Every point is checked against what was already on the map before any
        // of them is registered. Checking and claiming in one pass would have
        // each circle in the chain report an overlap with the circle behind it
        // — the chain is deliberately continuous, so consecutive circles always
        // touch — and bury the real warnings under dozens of false ones.
        var points = new List<Vector2>(steps + 1);
        bool clash = false;

        for (int i = 0; i <= steps; i++)
        {
            float along = -length * 0.5f + length * i / steps;
            var point = new Vector2(x + direction.x * along, z + direction.z * along);

            if (!IsFree(point.x, point.y, halfWidth)) clash = true;
            points.Add(point);
        }

        if (clash)
            Debug.LogWarning("Drone Strike: " + what + " at (" + x + ", " + z
                             + ") overlaps something already placed.");

        foreach (Vector2 point in points) Claim(point.x, point.y, halfWidth);
    }

    // ---------- the position ----------

    static void BuildCompound()
    {
        var group = new GameObject("Position");

        BuildRoad(group.transform);
        BuildFieldWorks(group.transform);
        targetCount = BuildTargets(group.transform);
        BuildClutter(group.transform);
    }

    /// <summary>Set by BuildCompound, read by BuildManagers when sizing the drone rack.</summary>
    static int targetCount;

    // ---------- road ----------

    /// <summary>
    /// The supply road, and the loop the patrol trucks drive.
    ///
    /// A rectangle rather than anything cleverer: the patrol has to be able to
    /// follow it exactly, and every prop on the map has to be checked against
    /// it, both of which are far easier against four straight segments. It is
    /// the one man-made line on the map, so it also does the work the buildings
    /// used to do — giving the pilot something to navigate by from altitude.
    /// </summary>
    static readonly Vector3[] PatrolWaypoints =
    {
        new Vector3(-95f, 0f, -75f),
        new Vector3(95f, 0f, -75f),
        new Vector3(95f, 0f, 75f),
        new Vector3(-95f, 0f, 75f)
    };

    /// <summary>Half the paved width. Nothing else may come within this of the centreline.</summary>
    const float RoadHalfWidth = 4.5f;

    /// <summary>Clearance every other prop keeps from the road centreline.</summary>
    const float RoadClearance = 9f;

    static void BuildRoad(Transform parent)
    {
        var group = new GameObject("Road");
        group.transform.SetParent(parent, false);

        Material asphalt = DroneMaterials.Load("Mat_Asphalt");
        Material line = DroneMaterials.Load("Mat_RoadLine");

        for (int i = 0; i < PatrolWaypoints.Length; i++)
        {
            Vector3 from = PatrolWaypoints[i];
            Vector3 to = PatrolWaypoints[(i + 1) % PatrolWaypoints.Length];

            BuildRoadSegment(group.transform, from, to, asphalt, line);
        }

        // Square patches of asphalt under the corners, where two segments meet
        // at a right angle and would otherwise leave a notch of grass showing.
        foreach (Vector3 corner in PatrolWaypoints)
        {
            var patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patch.name = "Corner";
            patch.transform.SetParent(group.transform, false);
            patch.transform.position = OnGround(corner.x, corner.z, 0.03f);
            patch.transform.localScale = new Vector3(RoadHalfWidth * 2f, 0.12f, RoadHalfWidth * 2f);
            patch.GetComponent<Renderer>().sharedMaterial = asphalt;
            Object.DestroyImmediate(patch.GetComponent<Collider>());
        }

        // The road is not a claim in the circle sense — it is a line — so it is
        // enforced by ClearOfRoad instead, which every scatter position checks.
    }

    static void BuildRoadSegment(Transform parent, Vector3 from, Vector3 to,
                                 Material asphalt, Material line)
    {
        Vector3 direction = (to - from).normalized;
        float length = Vector3.Distance(from, to);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 sideways = Quaternion.Euler(0f, 90f, 0f) * direction;

        Vector3 midpoint = (from + to) * 0.5f;

        // The carriageway.
        var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "Surface";
        surface.transform.SetParent(parent, false);
        surface.transform.position = OnGround(midpoint.x, midpoint.z, 0.03f);
        surface.transform.rotation = rotation;
        surface.transform.localScale = new Vector3(RoadHalfWidth * 2f, 0.12f, length);
        surface.GetComponent<Renderer>().sharedMaterial = asphalt;
        Object.DestroyImmediate(surface.GetComponent<Collider>());

        // Solid edge lines down both sides, inset so they sit on the asphalt.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 offset = sideways * (side * (RoadHalfWidth - 0.45f));

            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "EdgeLine";
            edge.transform.SetParent(parent, false);
            edge.transform.position = OnGround(midpoint.x + offset.x, midpoint.z + offset.z, 0.10f);
            edge.transform.rotation = rotation;
            edge.transform.localScale = new Vector3(0.28f, 0.06f, length - 2f);
            edge.GetComponent<Renderer>().sharedMaterial = line;
            Object.DestroyImmediate(edge.GetComponent<Collider>());
        }

        // Broken centreline. Dashes, not a solid stripe: a dashed line is what
        // actually reads as a road from three hundred metres up, and it gives
        // the eye a sense of the speed the drone is carrying along it.
        const float dashLength = 4f;
        const float dashGap = 5f;
        int dashes = Mathf.FloorToInt((length - 6f) / (dashLength + dashGap));

        for (int i = 0; i < dashes; i++)
        {
            float along = 3f + i * (dashLength + dashGap) + dashLength * 0.5f;
            Vector3 point = from + direction * along;

            var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Dash";
            dash.transform.SetParent(parent, false);
            dash.transform.position = OnGround(point.x, point.z, 0.10f);
            dash.transform.rotation = rotation;
            dash.transform.localScale = new Vector3(0.26f, 0.06f, dashLength);
            dash.GetComponent<Renderer>().sharedMaterial = line;
            Object.DestroyImmediate(dash.GetComponent<Collider>());
        }
    }

    /// <summary>
    /// Distance from a point to the road loop, measured against each segment as
    /// a line segment rather than an infinite line — otherwise the corners
    /// report clearance they do not have.
    /// </summary>
    static bool ClearOfRoad(float x, float z, float margin)
    {
        var point = new Vector2(x, z);

        for (int i = 0; i < PatrolWaypoints.Length; i++)
        {
            Vector3 a3 = PatrolWaypoints[i];
            Vector3 b3 = PatrolWaypoints[(i + 1) % PatrolWaypoints.Length];

            var a = new Vector2(a3.x, a3.z);
            var b = new Vector2(b3.x, b3.z);

            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            float distance = Vector2.Distance(point, a + ab * t);

            if (distance < margin) return false;
        }

        return true;
    }

    /// <summary>Patrol waypoints projected onto the ground, for handing to PatrolMover.</summary>
    static Vector3[] GroundedPatrolWaypoints()
    {
        var grounded = new Vector3[PatrolWaypoints.Length];
        for (int i = 0; i < PatrolWaypoints.Length; i++)
            grounded[i] = OnGround(PatrolWaypoints[i].x, PatrolWaypoints[i].z, 0.05f);
        return grounded;
    }

    // ---------- field works ----------

    /// <summary>
    /// What a position like this is actually made of: earth berms thrown up as
    /// blast walls, sandbag revetments, and camouflage netting stretched over
    /// the vehicle parks. All cover, none of it a target — the pilot has to fly
    /// around and under it to reach what is worth hitting, which is where the
    /// flying gets interesting.
    /// </summary>
    static void BuildFieldWorks(Transform parent)
    {
        var group = new GameObject("FieldWorks");
        group.transform.SetParent(parent, false);

        Material dirt = DroneMaterials.Load("Mat_Dirt");
        Material sandbag = DroneMaterials.Load("Mat_Sandbag");
        Material net = DroneMaterials.Load("Mat_CamoNet");

        // Berms standing between the open approach and what they shield. Each
        // one runs parallel to its charge rather than around it, ten metres off,
        // which is where a real blast wall goes and leaves the pilot a gap to
        // thread rather than a box to clear.
        Berm(group.transform, dirt, -58f, 44f, 26f, 90f);
        Berm(group.transform, dirt, 62f, -42f, 24f, 90f);
        Berm(group.transform, dirt, -20f, -62f, 22f, 90f);
        Berm(group.transform, dirt, 8f, 32f, 28f, 90f);
        Berm(group.transform, dirt, -40f, -22f, 20f, 90f);
        Berm(group.transform, dirt, 74f, 34f, 20f, 90f);

        // Sandbag revetments: lower and tighter, dotted between the berms.
        SandbagWall(group.transform, sandbag, 16f, 12f, 12f, 0f);
        SandbagWall(group.transform, sandbag, -30f, -44f, 12f, 45f);
        SandbagWall(group.transform, sandbag, 56f, 8f, 14f, 0f);
        SandbagWall(group.transform, sandbag, -66f, 26f, 12f, 90f);

        // Netting on poles over the two vehicle parks. Only the poles claim
        // ground: the sheet is five metres up, so what is parked underneath is
        // exactly what is supposed to be there.
        CamoNet(group.transform, net, -58f, 28f, 18f, 0f);
        CamoNet(group.transform, net, 62f, -30f, 18f, 0f);
    }

    /// <summary>A long low mound of earth. Solid: the drone has to fly over it.</summary>
    static void Berm(Transform parent, Material dirt, float x, float z, float length, float yaw)
    {
        ClaimLine("Berm", x, z, yaw, length, 3.2f);

        var berm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        berm.name = "Berm";
        berm.transform.SetParent(parent, false);
        berm.transform.position = OnGround(x, z, 1.1f);
        berm.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        berm.transform.localScale = new Vector3(4.5f, 2.2f, length);
        berm.GetComponent<Renderer>().sharedMaterial = dirt;
    }

    static void SandbagWall(Transform parent, Material sandbag, float x, float z,
                            float length, float yaw)
    {
        ClaimLine("Sandbags", x, z, yaw, length, 1.2f);

        var group = new GameObject("Sandbags");
        group.transform.SetParent(parent, false);
        group.transform.position = OnGround(x, z);
        group.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Three courses, each one shorter than the one below, so it tapers the
        // way a stacked wall really does instead of reading as a plain slab.
        const float bagHeight = 0.42f;
        for (int course = 0; course < 3; course++)
        {
            float courseLength = length - course * 1.6f;
            if (courseLength < 2f) break;

            int bags = Mathf.Max(2, Mathf.RoundToInt(courseLength / 1.1f));
            for (int i = 0; i < bags; i++)
            {
                float along = -courseLength * 0.5f + (i + 0.5f) * (courseLength / bags);

                var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bag.name = "Bag";
                bag.transform.SetParent(group.transform, false);
                bag.transform.localPosition =
                    new Vector3(0f, bagHeight * (course + 0.5f), along);
                bag.transform.localRotation =
                    Quaternion.Euler(0f, Random.Range(-6f, 6f), Random.Range(-4f, 4f));
                bag.transform.localScale = new Vector3(0.95f, bagHeight, 1.05f);
                bag.GetComponent<Renderer>().sharedMaterial = sandbag;

                // One collider for the wall would be cleaner, but a per-bag box
                // is what makes a drone clipping the top of it tumble instead of
                // sliding along an invisible flat plane.
            }
        }
    }

    /// <summary>
    /// Camouflage netting: a dark sheet on four poles, high enough for a drone
    /// to fly under and low enough to hide what is parked beneath it from above.
    /// </summary>
    static void CamoNet(Transform parent, Material net, float x, float z, float size, float yaw)
    {
        var group = new GameObject("CamoNet");
        group.transform.SetParent(parent, false);
        group.transform.position = OnGround(x, z);
        group.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        const float poleHeight = 5.2f;
        Material metal = DroneMaterials.Load("Mat_RustMetal");

        foreach (float px in new[] { -size * 0.5f, size * 0.5f })
        {
            foreach (float pz in new[] { -size * 0.35f, size * 0.35f })
            {
                // Only the poles reserve ground; the sheet is overhead.
                Vector3 world = group.transform.TransformPoint(new Vector3(px, 0f, pz));
                ClaimChecked("CamoNet pole", world.x, world.z, 0.9f);

                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(group.transform, false);
                pole.transform.localPosition = new Vector3(px, poleHeight * 0.5f, pz);
                pole.transform.localScale = new Vector3(0.22f, poleHeight * 0.5f, 0.22f);
                pole.GetComponent<Renderer>().sharedMaterial = metal;
                Object.DestroyImmediate(pole.GetComponent<Collider>());
            }
        }

        var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sheet.name = "Net";
        sheet.transform.SetParent(group.transform, false);
        sheet.transform.localPosition = new Vector3(0f, poleHeight, 0f);
        sheet.transform.localRotation = Quaternion.Euler(4f, 0f, 3f);   // slack, not taut
        sheet.transform.localScale = new Vector3(size + 1f, 0.1f, size * 0.7f + 1f);
        sheet.GetComponent<Renderer>().sharedMaterial = net;
    }

    // ---------- targets ----------

    /// <summary>
    /// The targets. Dug in behind the berms, tucked under the netting and spread
    /// right across the position rather than parked in a row: no two are visible
    /// from the same spot, so clearing the map means flying it rather than
    /// orbiting one point and picking things off.
    ///
    /// Every position here is checked against the road and against everything
    /// already placed, so a warning appears at build time if one of these
    /// numbers is ever edited into something that overlaps.
    /// </summary>
    static int BuildTargets(Transform parent)
    {
        var group = new GameObject("Targets");
        group.transform.SetParent(parent, false);

        // Armour, one per revetment, facing out along its own berm.
        PlaceTarget("Armour", group.transform, TargetProps.ArmouredVehicle, -58f, 34f, 118f, 6f);
        PlaceTarget("Armour", group.transform, TargetProps.ArmouredVehicle, 62f, -30f, -62f, 6f);
        PlaceTarget("Armour", group.transform, TargetProps.ArmouredVehicle, -20f, -52f, 14f, 6f);

        // Parked transport, well away from the armour.
        PlaceTarget("Truck", group.transform, TargetProps.Truck, 30f, 46f, 202f, 5f);
        PlaceTarget("Truck", group.transform, TargetProps.Truck, -72f, -18f, 96f, 5f);

        // Tents: the supply end of the position.
        PlaceTarget("Tent", group.transform, TargetProps.SupplyDepot, 8f, 20f, 28f, 5f);
        PlaceTarget("Tent", group.transform, TargetProps.SupplyDepot, -40f, -34f, -18f, 5f);
        PlaceTarget("Tent", group.transform, TargetProps.SupplyDepot, 74f, 22f, 8f, 5f);

        // Masts, the two landmarks visible from anywhere on the map.
        PlaceTarget("Antenna", group.transform, TargetProps.Antenna, 0f, 62f, 0f, 4f);
        PlaceTarget("Antenna", group.transform, TargetProps.Antenna, -80f, 6f, 0f, 4f);

        // Two trucks running the loop, starting from opposite corners so they
        // are never side by side.
        BuildPatrolTruck(group.transform, 0);
        BuildPatrolTruck(group.transform, 2);

        return group.transform.childCount;
    }

    delegate Target TargetBuilder(Transform parent, Vector3 position, float yaw,
                                  TargetProps.Palette palette);

    static void PlaceTarget(string what, Transform parent, TargetBuilder builder,
                            float x, float z, float yaw, float radius)
    {
        if (!ClearOfRoad(x, z, RoadClearance))
            Debug.LogWarning("Drone Strike: " + what + " at (" + x + ", " + z
                             + ") sits on the patrol road.");

        ClaimChecked(what, x, z, radius);
        builder(parent, OnGround(x, z), yaw, palette);
    }

    /// <summary>
    /// A truck placed at one corner of the patrol loop and set to drive it.
    ///
    /// It is not claimed against the placement list: it moves, so the ground it
    /// occupies is the whole road, which ClearOfRoad already keeps everything
    /// else off.
    /// </summary>
    static void BuildPatrolTruck(Transform parent, int startCorner)
    {
        Vector3[] waypoints = GroundedPatrolWaypoints();
        Vector3 start = waypoints[startCorner % waypoints.Length];

        // Faced along the leg it is about to drive, so it does not spend its
        // first seconds pivoting on the spot.
        Vector3 next = waypoints[(startCorner + 1) % waypoints.Length];
        Vector3 heading = next - start;
        float yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;

        Target target = TargetProps.Truck(parent, start, yaw, palette);

        var mover = target.gameObject.AddComponent<PatrolMover>();
        mover.waypoints = waypoints;
        mover.speed = 6f;
    }

    // ---------- clutter ----------

    /// <summary>
    /// The small stuff: fuel drums, crate stacks and concrete blocks scattered
    /// across the position. None of it is a target and none of it is required —
    /// it is there so the ground has something on it between the targets, which
    /// is most of what makes a place look occupied.
    ///
    /// Scattered by rejection sampling against the claim list rather than by
    /// hand, because a hundred hand-placed drums is a hundred chances to bury
    /// one inside a tank.
    /// </summary>
    static void BuildClutter(Transform parent)
    {
        var group = new GameObject("Clutter");
        group.transform.SetParent(parent, false);

        Material metal = DroneMaterials.Load("Mat_RustMetal");
        Material crate = DroneMaterials.Load("Mat_Crate");
        Material concrete = DroneMaterials.Load("Mat_Concrete");

        Random.State previous = Random.state;
        Random.InitState(TerrainSeed + 7);

        const int wanted = 34;
        int placed = 0;

        for (int attempt = 0; attempt < 600 && placed < wanted; attempt++)
        {
            float x = Random.Range(-105f, 105f);
            float z = Random.Range(-88f, 88f);

            const float radius = 2.6f;
            if (!ClearOfRoad(x, z, RoadClearance)) continue;
            if (!IsFree(x, z, radius)) continue;

            Claim(x, z, radius);
            placed++;

            float roll = Random.value;
            if (roll < 0.45f) FuelDrums(group.transform, metal, x, z);
            else if (roll < 0.8f) CrateStack(group.transform, crate, x, z);
            else ConcreteBlocks(group.transform, concrete, x, z);
        }

        Random.state = previous;
    }

    static void FuelDrums(Transform parent, Material metal, float x, float z)
    {
        int count = Random.Range(3, 7);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float spread = Random.Range(0f, 1.7f);
            float dx = Mathf.Cos(angle) * spread;
            float dz = Mathf.Sin(angle) * spread;

            var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drum.name = "Drum";
            drum.transform.SetParent(parent, false);
            drum.transform.position = OnGround(x + dx, z + dz, 0.45f);
            drum.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            drum.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
            drum.GetComponent<Renderer>().sharedMaterial = metal;
        }
    }

    static void CrateStack(Transform parent, Material crate, float x, float z)
    {
        int count = Random.Range(2, 6);
        for (int i = 0; i < count; i++)
        {
            float dx = Random.Range(-1.2f, 1.2f);
            float dz = Random.Range(-1.2f, 1.2f);
            float height = Random.value < 0.35f ? 1.35f : 0.45f;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Crate";
            box.transform.SetParent(parent, false);
            box.transform.position = OnGround(x + dx, z + dz, height);
            box.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            box.transform.localScale = new Vector3(0.9f, 0.85f, 1.25f);
            box.GetComponent<Renderer>().sharedMaterial = crate;
        }
    }

    static void ConcreteBlocks(Transform parent, Material concrete, float x, float z)
    {
        int count = Random.Range(2, 5);
        for (int i = 0; i < count; i++)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Block";
            block.transform.SetParent(parent, false);
            block.transform.position = OnGround(x + Random.Range(-1.5f, 1.5f),
                                                z + Random.Range(-1.5f, 1.5f), 0.55f);
            block.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            block.transform.localScale = new Vector3(1.1f, 1.1f, 2.4f);
            block.GetComponent<Renderer>().sharedMaterial = concrete;
        }
    }

    // ---------- woodland ----------

    /// <summary>
    /// Trees over the approach, thinning out near the position. They give the
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

        const int attempts = 520;
        for (int i = 0; i < attempts; i++)
        {
            float x = Random.Range(-MapSize * 0.45f, MapSize * 0.45f);
            float z = Random.Range(-MapSize * 0.45f, MapSize * 0.45f);

            // Keep the position and its road clear. The radius has to cover the
            // whole loop now that there is no fence line marking the edge.
            if (new Vector2(x, z).magnitude < 140f) continue;
            if (!ClearOfRoad(x, z, 16f)) continue;

            TargetProps.Tree(group.transform, OnGround(x, z), Random.Range(0.8f, 1.6f),
                             trunk, foliage);
        }

        Random.state = previous;
    }

    // ---------- gameplay objects ----------

    static Transform BuildLaunchPoint()
    {
        // South-west of the position: far enough that the pilot has to fly to
        // the target, close enough that the whole position sits inside clean
        // signal range and only the far corners of the map degrade.
        var launch = new GameObject("LaunchPoint");
        launch.transform.position = OnGround(-100f, -120f, 2f);
        launch.transform.rotation = Quaternion.LookRotation(
            new Vector3(100f, 0f, 120f).normalized, Vector3.up);

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
        // made the mission unwinnable — three drones cannot ever clear eleven
        // targets no matter how well they are flown.
        mission.droneCount = Mathf.Max(4, Mathf.CeilToInt(targetCount * 1.3f));

        managers.AddComponent<DroneHUD>();
    }
}
