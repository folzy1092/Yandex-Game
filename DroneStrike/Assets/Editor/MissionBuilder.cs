using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the mission scenes: rolling ground, a paved road with a patrolled
/// loop, dug-in vehicles and tents, scattered woodland, and the pads the drones
/// launch from.
///
/// There are deliberately no buildings. A staging area behind a line is tents,
/// earth berms, camouflage netting and a graded road — warehouses and factories
/// belong to a different game.
///
/// One builder produces all three maps. They differ by profile — size, terrain,
/// how many of each target, how thick the woodland, what the light looks like —
/// rather than by three copies of this file, because three copies is three
/// places for a fix to land in one of.
///
/// Nothing is hand-placed. Every position comes out of rejection sampling
/// against a list of claimed ground, so a target cannot spawn inside another
/// one, on the road, or under a tree, whatever the seed does.
/// </summary>
public static class MissionBuilder
{
    // ---------- profiles ----------

    public struct Profile
    {
        public string sceneName;
        public int seed;

        public float mapSize;
        public float hillAmplitude;

        /// <summary>Half-extents of the road loop, in metres.</summary>
        public float roadHalfX;
        public float roadHalfZ;

        /// <summary>A straight road across the middle, turning the loop into a crossroads.</summary>
        public bool crossroads;

        public int armour;
        public int trucks;
        public int tents;
        public int antennas;
        public int patrols;

        /// <summary>Woodland attempts. More means denser trees on the approach.</summary>
        public int treeAttempts;

        public Color sunColour;
        public float sunIntensity;
        public Vector3 sunAngles;
        public Color fogColour;
        public float fogStart;
        public float fogEnd;
    }

    public static Profile Outpost()
    {
        return new Profile
        {
            sceneName = "Mission1",
            seed = 20260824,
            mapSize = 700f,
            hillAmplitude = 30f,
            roadHalfX = 95f,
            roadHalfZ = 75f,
            crossroads = false,
            armour = 3, trucks = 3, tents = 3, antennas = 2, patrols = 2,
            treeAttempts = 520,
            sunColour = new Color(1f, 0.96f, 0.88f),
            sunIntensity = 1.25f,
            sunAngles = new Vector3(48f, 30f, 0f),
            fogColour = new Color(0.68f, 0.74f, 0.82f),
            fogStart = 260f,
            fogEnd = 680f
        };
    }

    public static Profile Woodline()
    {
        return new Profile
        {
            sceneName = "Mission2",
            seed = 20260825,
            mapSize = 720f,
            hillAmplitude = 46f,          // properly rolling, not a table
            roadHalfX = 72f,
            roadHalfZ = 100f,             // a long road rather than a square
            crossroads = false,
            armour = 4, trucks = 4, tents = 3, antennas = 1, patrols = 2,
            treeAttempts = 1100,          // the woods are the point of this one
            sunColour = new Color(0.96f, 0.94f, 0.86f),
            sunIntensity = 1.05f,
            sunAngles = new Vector3(34f, 200f, 0f),
            fogColour = new Color(0.62f, 0.70f, 0.68f),
            fogStart = 180f,
            fogEnd = 540f
        };
    }

    public static Profile Crossroads()
    {
        return new Profile
        {
            sceneName = "Mission3",
            seed = 20260826,
            mapSize = 780f,
            hillAmplitude = 34f,
            roadHalfX = 115f,
            roadHalfZ = 85f,
            crossroads = true,
            armour = 5, trucks = 5, tents = 4, antennas = 2, patrols = 3,
            treeAttempts = 620,
            sunColour = new Color(1f, 0.78f, 0.58f),   // low sun, long shadows
            sunIntensity = 1.05f,
            sunAngles = new Vector3(16f, 118f, 0f),
            fogColour = new Color(0.55f, 0.50f, 0.52f),
            fogStart = 220f,
            fogEnd = 620f
        };
    }

    // ---------- entry points ----------

    [MenuItem("Tools/Drone Strike/2 - Build All Missions")]
    public static void BuildAll()
    {
        Build(Outpost());
        Build(Woodline());
        Build(Crossroads());
    }

    static Profile profile;
    static TargetProps.Palette palette;
    static int targetCount;

    public static void Build(Profile which)
    {
        profile = which;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        palette = LoadPalette();
        claims.Clear();
        BuildRoadGeometry();

        Random.State previous = Random.state;
        Random.InitState(profile.seed);

        BuildLighting();
        BuildTerrain();

        var position = new GameObject("Position");
        BuildRoad(position.transform);
        targetCount = BuildTargets(position.transform);
        BuildFieldWorks(position.transform);
        BuildClutter(position.transform);

        BuildWoodland();

        Transform[] pads = BuildLaunchPads();
        BuildManagers(pads);

        Random.state = previous;

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + profile.sceneName + ".unity");
        Debug.Log("Drone Strike: " + profile.sceneName + " built with " + targetCount + " targets.");
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

    /// <summary>
    /// Ground inside this radius is levelled. It has to cover the whole road, or
    /// a patrol truck drives a graded road up and down hills.
    /// </summary>
    static float FlatRadius
    {
        get { return new Vector2(profile.roadHalfX, profile.roadHalfZ).magnitude + 14f; }
    }

    // ---------- environment ----------

    static void BuildLighting()
    {
        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = profile.sunIntensity;
        sun.color = profile.sunColour;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.6f;
        sunGO.transform.rotation = Quaternion.Euler(profile.sunAngles);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = profile.fogColour * 0.9f;
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.50f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.28f, 0.24f);

        // Haze on the horizon: it gives the open map a sense of scale and hides
        // the edge of the terrain mesh.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = profile.fogColour;
        RenderSettings.fogStartDistance = profile.fogStart;
        RenderSettings.fogEndDistance = profile.fogEnd;
    }

    static void BuildTerrain()
    {
        var ground = new GameObject("Terrain");

        Mesh mesh = TerrainMesh.Build(profile.mapSize, 129, profile.hillAmplitude,
                                      profile.seed, FlatRadius);

        ground.AddComponent<MeshFilter>().sharedMesh = mesh;
        ground.AddComponent<MeshRenderer>().sharedMaterial = DroneMaterials.Load("Mat_Ground");
        ground.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static float GroundAt(float x, float z)
    {
        float height = TerrainMesh.SampleHeight(x, z, profile.hillAmplitude, profile.seed);

        float flat = FlatRadius;
        float distance = new Vector2(x, z).magnitude;
        if (distance < flat * 2f)
            height *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(flat, flat * 2f, distance));

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
    /// There is no editor to eyeball these maps in, and there are three of them
    /// now, so overlaps have to be impossible by construction rather than caught
    /// by inspection. Everything registers what it occupies; everything placed
    /// afterwards samples until it finds ground nobody has taken.
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
    /// Finds unclaimed ground inside the position, or reports failure rather
    /// than placing something on top of something else.
    /// </summary>
    static bool TryFindSpot(float radius, float margin, out Vector2 spot)
    {
        float halfX = profile.roadHalfX - RoadClearance - radius;
        float halfZ = profile.roadHalfZ - RoadClearance - radius;

        for (int attempt = 0; attempt < 400; attempt++)
        {
            var candidate = new Vector2(Random.Range(-halfX, halfX), Random.Range(-halfZ, halfZ));

            if (!ClearOfRoad(candidate.x, candidate.y, RoadClearance + radius)) continue;
            if (!IsFree(candidate.x, candidate.y, radius + margin)) continue;

            spot = candidate;
            return true;
        }

        spot = Vector2.zero;
        return false;
    }

    /// <summary>
    /// Claims a long thin object as a chain of small circles down its length
    /// rather than one circle around the whole thing.
    ///
    /// A single circle sized to a thirty-metre berm reserves a fifteen-metre
    /// radius of ground, most of which the berm is nowhere near — and since the
    /// point of a berm is to stand a few metres from what it shields, that one
    /// circle rejects every layout worth having.
    ///
    /// Every point is tested before any is registered: the chain is deliberately
    /// continuous, so each circle overlaps the one behind it and a check-as-you-go
    /// pass would reject the object against itself.
    /// </summary>
    static bool TryClaimLine(float x, float z, float yaw, float length, float halfWidth)
    {
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        int steps = Mathf.Max(2, Mathf.CeilToInt(length / halfWidth));

        var points = new List<Vector2>(steps + 1);

        for (int i = 0; i <= steps; i++)
        {
            float along = -length * 0.5f + length * i / steps;
            var point = new Vector2(x + direction.x * along, z + direction.z * along);

            if (!IsFree(point.x, point.y, halfWidth)) return false;
            if (!ClearOfRoad(point.x, point.y, RoadClearance)) return false;

            points.Add(point);
        }

        foreach (Vector2 point in points) Claim(point.x, point.y, halfWidth);
        return true;
    }

    // ---------- road ----------

    /// <summary>Half the paved width.</summary>
    const float RoadHalfWidth = 5f;

    /// <summary>Clearance everything else keeps from the road centreline.</summary>
    const float RoadClearance = 10f;

    /// <summary>The loop the patrol trucks drive, anticlockwise from the south-west.</summary>
    static Vector3[] patrolWaypoints;

    /// <summary>Every paved centreline, including any that is not patrolled.</summary>
    static readonly List<Vector4> roadSegments = new List<Vector4>();   // ax, az, bx, bz

    static void BuildRoadGeometry()
    {
        float hx = profile.roadHalfX;
        float hz = profile.roadHalfZ;

        patrolWaypoints = new[]
        {
            new Vector3(-hx, 0f, -hz),
            new Vector3(hx, 0f, -hz),
            new Vector3(hx, 0f, hz),
            new Vector3(-hx, 0f, hz)
        };

        roadSegments.Clear();
        for (int i = 0; i < patrolWaypoints.Length; i++)
        {
            Vector3 a = patrolWaypoints[i];
            Vector3 b = patrolWaypoints[(i + 1) % patrolWaypoints.Length];
            roadSegments.Add(new Vector4(a.x, a.z, b.x, b.z));
        }

        // A road straight through the middle turns the loop into a junction, and
        // gives the pilot a line to follow across the position rather than only
        // around it.
        // Split at the middle rather than run straight through it. Two full-length
        // runs crossing at the centre lay two coplanar slabs on top of each
        // other, and coplanar slabs z-fight; four half-runs leave a gap at the
        // centre that the junction patch fills cleanly.
        if (profile.crossroads)
        {
            roadSegments.Add(new Vector4(-hx, 0f, 0f, 0f));
            roadSegments.Add(new Vector4(0f, 0f, hx, 0f));
            roadSegments.Add(new Vector4(0f, -hz, 0f, 0f));
            roadSegments.Add(new Vector4(0f, 0f, 0f, hz));
        }
    }

    static void BuildRoad(Transform parent)
    {
        var group = new GameObject("Road");
        group.transform.SetParent(parent, false);

        Material asphalt = DroneMaterials.Load("Mat_Asphalt");
        Material line = DroneMaterials.Load("Mat_RoadLine");
        Material dirt = DroneMaterials.Load("Mat_Dirt");

        foreach (Vector4 segment in roadSegments)
        {
            var from = new Vector3(segment.x, 0f, segment.y);
            var to = new Vector3(segment.z, 0f, segment.w);
            BuildRoadSegment(group.transform, from, to, asphalt, line, dirt);
        }

        // Square patches where two runs meet, so the joins are paved rather than
        // showing a notch of grass. Every run is shortened by exactly this much
        // at each end, so the patch fills the gap instead of stacking on top of
        // the surface — overlapping slabs at the corners is what made the road
        // look like it had been cut with scissors.
        var corners = new List<Vector2>();
        foreach (Vector3 waypoint in patrolWaypoints) corners.Add(new Vector2(waypoint.x, waypoint.z));

        if (profile.crossroads)
        {
            corners.Add(Vector2.zero);
            corners.Add(new Vector2(-profile.roadHalfX, 0f));
            corners.Add(new Vector2(profile.roadHalfX, 0f));
            corners.Add(new Vector2(0f, -profile.roadHalfZ));
            corners.Add(new Vector2(0f, profile.roadHalfZ));
        }

        foreach (Vector2 corner in corners)
        {
            Slab(group.transform, "Junction", corner.x, corner.y, 0f,
                 RoadHalfWidth * 2f, RoadHalfWidth * 2f, 0.03f, 0.12f, asphalt);
        }
    }

    static void BuildRoadSegment(Transform parent, Vector3 from, Vector3 to,
                                 Material asphalt, Material line, Material dirt)
    {
        Vector3 direction = (to - from).normalized;
        float fullLength = Vector3.Distance(from, to);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        Vector3 sideways = Quaternion.Euler(0f, 90f, 0f) * direction;

        // Shortened by a junction patch at each end.
        float length = fullLength - RoadHalfWidth * 2f;
        if (length <= 1f) return;

        Vector3 midpoint = (from + to) * 0.5f;

        // Gravel shoulders under the edges, a little wider than the asphalt and
        // a little lower. Cheap, and it stops the road reading as a black
        // rectangle dropped onto grass.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 offset = sideways * (side * (RoadHalfWidth + 0.9f));

            // Same length as the carriageway, not the full run: shoulders that
            // reach into the junctions overlap each other at every corner, and
            // two coplanar slabs of the same material z-fight. A verge that
            // breaks at a junction is what a real one does anyway.
            Slab(parent, "Shoulder", midpoint.x + offset.x, midpoint.z + offset.z, yaw,
                 2.6f, length, 0.015f, 0.09f, dirt);
        }

        Slab(parent, "Surface", midpoint.x, midpoint.z, yaw,
             RoadHalfWidth * 2f, length, 0.03f, 0.12f, asphalt);

        // Solid edge lines, inset so they sit on the asphalt rather than on its
        // very lip.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 offset = sideways * (side * (RoadHalfWidth - 0.55f));
            Slab(parent, "EdgeLine", midpoint.x + offset.x, midpoint.z + offset.z, yaw,
                 0.3f, length - 1.5f, 0.10f, 0.06f, line);
        }

        // Broken centreline. Dashes rather than a solid stripe: a dashed line is
        // what actually reads as a road from two hundred metres up, and it gives
        // the eye something to measure the drone's speed against.
        const float dashLength = 4.5f;
        const float dashGap = 5.5f;
        float span = length - 4f;
        int dashes = Mathf.FloorToInt(span / (dashLength + dashGap));

        // Centred in the run, so the pattern is symmetrical about the middle
        // instead of crowding one end.
        float used = dashes * (dashLength + dashGap) - dashGap;
        float start = -used * 0.5f + dashLength * 0.5f;

        for (int i = 0; i < dashes; i++)
        {
            Vector3 point = midpoint + direction * (start + i * (dashLength + dashGap));
            Slab(parent, "Dash", point.x, point.z, yaw, 0.28f, dashLength, 0.10f, 0.06f, line);
        }
    }

    /// <summary>A flat slab laid on the ground: road surface, marking or shoulder.</summary>
    static void Slab(Transform parent, string name, float x, float z, float yaw,
                     float width, float length, float lift, float thickness, Material material)
    {
        var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = name;
        slab.transform.SetParent(parent, false);
        slab.transform.position = OnGround(x, z, lift);
        slab.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        slab.transform.localScale = new Vector3(width, thickness, length);

        if (material != null) slab.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(slab.GetComponent<Collider>());
    }

    /// <summary>
    /// Distance from a point to the nearest road, measured against each run as a
    /// segment rather than an infinite line — otherwise the corners report
    /// clearance they do not have.
    /// </summary>
    static bool ClearOfRoad(float x, float z, float margin)
    {
        var point = new Vector2(x, z);

        foreach (Vector4 segment in roadSegments)
        {
            var a = new Vector2(segment.x, segment.y);
            var b = new Vector2(segment.z, segment.w);

            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);

            if (Vector2.Distance(point, a + ab * t) < margin) return false;
        }

        return true;
    }

    static Vector3[] GroundedPatrolWaypoints()
    {
        var grounded = new Vector3[patrolWaypoints.Length];
        for (int i = 0; i < patrolWaypoints.Length; i++)
            grounded[i] = OnGround(patrolWaypoints[i].x, patrolWaypoints[i].z, 0.05f);
        return grounded;
    }

    // ---------- targets ----------

    /// <summary>
    /// Where each target ended up, so the field works can be built against them
    /// afterwards rather than guessed at.
    /// </summary>
    static readonly List<Vector3> targetSpots = new List<Vector3>();   // x, z, radius

    static int BuildTargets(Transform parent)
    {
        var group = new GameObject("Targets");
        group.transform.SetParent(parent, false);

        targetSpots.Clear();

        Scatter(group.transform, TargetProps.ArmouredVehicle, profile.armour, 6f);
        Scatter(group.transform, TargetProps.Truck, profile.trucks, 5f);
        Scatter(group.transform, TargetProps.SupplyDepot, profile.tents, 6.5f);
        Scatter(group.transform, TargetProps.Antenna, profile.antennas, 4.5f);

        for (int i = 0; i < profile.patrols; i++)
            BuildPatrolTruck(group.transform, i * (patrolWaypoints.Length / Mathf.Max(1, profile.patrols)));

        return group.transform.childCount;
    }

    delegate Target TargetBuilder(Transform parent, Vector3 position, float yaw,
                                  TargetProps.Palette palette);

    static void Scatter(Transform parent, TargetBuilder builder, int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            // A wide margin between targets: two of them close enough to be
            // caught by one blast turns a mission into a much shorter one, and
            // the map is meant to be flown rather than orbited.
            if (!TryFindSpot(radius, 9f, out spot)) continue;

            Claim(spot.x, spot.y, radius);
            targetSpots.Add(new Vector3(spot.x, spot.y, radius));

            builder(parent, OnGround(spot.x, spot.y), Random.Range(0f, 360f), palette);
        }
    }

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

        // Not claimed: it moves, so the ground it occupies is the whole road,
        // which ClearOfRoad already keeps everything else off.
    }

    // ---------- field works ----------

    /// <summary>
    /// What a position like this is actually made of: earth berms thrown up as
    /// blast walls, sandbag revetments, and netting stretched over the vehicle
    /// parks. All cover, none of it a target — the pilot has to fly around and
    /// under it to reach what is worth hitting.
    ///
    /// Built after the targets and positioned against them, so each berm stands
    /// between something worth shielding and the open ground, rather than in a
    /// field on its own.
    /// </summary>
    static void BuildFieldWorks(Transform parent)
    {
        var group = new GameObject("FieldWorks");
        group.transform.SetParent(parent, false);

        Material dirt = DroneMaterials.Load("Mat_Dirt");
        Material sandbag = DroneMaterials.Load("Mat_Sandbag");
        Material net = DroneMaterials.Load("Mat_CamoNet");

        foreach (Vector3 spot in targetSpots)
        {
            // Berms for the bigger things, sandbags for the rest.
            bool heavy = spot.z >= 6f;
            float standoff = spot.z + (heavy ? 5.5f : 3.5f);
            float length = heavy ? Random.Range(20f, 28f) : Random.Range(10f, 14f);
            float halfWidth = heavy ? 3.2f : 1.2f;

            // Try a few bearings before giving up: the first one is often taken.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float bearing = Random.Range(0f, 360f);
                float x = spot.x + Mathf.Sin(bearing * Mathf.Deg2Rad) * standoff;
                float z = spot.y + Mathf.Cos(bearing * Mathf.Deg2Rad) * standoff;

                // Run it broadside to the target it shields, which is the only
                // orientation that actually covers anything.
                float yaw = bearing + 90f;

                if (!TryClaimLine(x, z, yaw, length, halfWidth)) continue;

                if (heavy) Berm(group.transform, dirt, x, z, length, yaw);
                else SandbagWall(group.transform, sandbag, x, z, length, yaw);
                break;
            }
        }

        // Netting over a couple of the vehicle parks. Only the poles claim
        // ground: the sheet is five metres up, so whatever is parked underneath
        // is exactly what is supposed to be there.
        int nets = Mathf.Min(2, targetSpots.Count);
        for (int i = 0; i < nets; i++)
        {
            Vector3 spot = targetSpots[Random.Range(0, targetSpots.Count)];
            CamoNet(group.transform, net, spot.x, spot.y, 20f, Random.Range(0f, 360f));
        }
    }

    /// <summary>A long low mound of earth. Solid: the drone has to fly over it.</summary>
    static void Berm(Transform parent, Material dirt, float x, float z, float length, float yaw)
    {
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
        var group = new GameObject("Sandbags");
        group.transform.SetParent(parent, false);
        group.transform.position = OnGround(x, z);
        group.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Three courses, each shorter than the one below, so it tapers the way a
        // stacked wall really does instead of reading as a plain slab.
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
                bag.transform.localPosition = new Vector3(0f, bagHeight * (course + 0.5f), along);
                bag.transform.localRotation =
                    Quaternion.Euler(0f, Random.Range(-6f, 6f), Random.Range(-4f, 4f));
                bag.transform.localScale = new Vector3(0.95f, bagHeight, 1.05f);
                bag.GetComponent<Renderer>().sharedMaterial = sandbag;
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
                Vector3 world = group.transform.TransformPoint(new Vector3(px, 0f, pz));
                if (!IsFree(world.x, world.z, 0.9f)) continue;
                if (!ClearOfRoad(world.x, world.z, RoadClearance)) continue;

                Claim(world.x, world.z, 0.9f);

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

    // ---------- clutter ----------

    /// <summary>
    /// Fuel drums, crate stacks and concrete blocks scattered across the
    /// position. None of it is a target and none of it is required — it is there
    /// so the ground has something on it between the targets, which is most of
    /// what makes a place look occupied.
    /// </summary>
    static void BuildClutter(Transform parent)
    {
        var group = new GameObject("Clutter");
        group.transform.SetParent(parent, false);

        Material metal = DroneMaterials.Load("Mat_RustMetal");
        Material crate = DroneMaterials.Load("Mat_Crate");
        Material concrete = DroneMaterials.Load("Mat_Concrete");

        int wanted = Mathf.RoundToInt(profile.roadHalfX * profile.roadHalfZ / 240f);
        int placed = 0;

        for (int attempt = 0; attempt < wanted * 25 && placed < wanted; attempt++)
        {
            Vector2 spot;
            if (!TryFindSpot(2.6f, 0.5f, out spot)) break;

            Claim(spot.x, spot.y, 2.6f);
            placed++;

            float roll = Random.value;
            if (roll < 0.45f) FuelDrums(group.transform, metal, spot.x, spot.y);
            else if (roll < 0.8f) CrateStack(group.transform, crate, spot.x, spot.y);
            else ConcreteBlocks(group.transform, concrete, spot.x, spot.y);
        }
    }

    static void FuelDrums(Transform parent, Material metal, float x, float z)
    {
        int count = Random.Range(3, 7);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float spread = Random.Range(0f, 1.7f);

            var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drum.name = "Drum";
            drum.transform.SetParent(parent, false);
            drum.transform.position = OnGround(x + Mathf.Cos(angle) * spread,
                                               z + Mathf.Sin(angle) * spread, 0.45f);
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
            float height = Random.value < 0.35f ? 1.35f : 0.45f;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Crate";
            box.transform.SetParent(parent, false);
            box.transform.position = OnGround(x + Random.Range(-1.2f, 1.2f),
                                              z + Random.Range(-1.2f, 1.2f), height);
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

        float keepClear = FlatRadius + 12f;
        float reach = profile.mapSize * 0.45f;

        for (int i = 0; i < profile.treeAttempts; i++)
        {
            float x = Random.Range(-reach, reach);
            float z = Random.Range(-reach, reach);

            if (new Vector2(x, z).magnitude < keepClear) continue;
            if (!ClearOfRoad(x, z, 18f)) continue;

            TargetProps.Tree(group.transform, OnGround(x, z), Random.Range(0.8f, 1.6f),
                             trunk, foliage);
        }
    }

    // ---------- launch pads ----------

    /// <summary>
    /// A ring of pads around the position, all facing the middle.
    ///
    /// One fixed launch point means every run starts on the same approach from
    /// the same angle, and by the fourth drone the player is repeating a line
    /// they have memorised instead of flying. Spreading the pads around the ring
    /// makes each life a fresh problem without touching the map.
    ///
    /// The radius is set from the road rather than typed in, so the whole
    /// position stays inside a usable signal range on every map.
    /// </summary>
    static Transform[] BuildLaunchPads()
    {
        var group = new GameObject("LaunchPads");

        const int count = 6;
        float radius = Mathf.Max(profile.roadHalfX, profile.roadHalfZ) + 55f;
        float offset = Random.Range(0f, 360f);

        var pads = new Transform[count];
        Material asphalt = DroneMaterials.Load("Mat_Asphalt");

        for (int i = 0; i < count; i++)
        {
            float bearing = offset + i * (360f / count) + Random.Range(-12f, 12f);
            float distance = radius + Random.Range(-12f, 18f);

            float x = Mathf.Sin(bearing * Mathf.Deg2Rad) * distance;
            float z = Mathf.Cos(bearing * Mathf.Deg2Rad) * distance;

            var pad = new GameObject("Pad" + i);
            pad.transform.SetParent(group.transform, false);
            pad.transform.position = OnGround(x, z, 2f);
            pad.transform.rotation = Quaternion.LookRotation(
                new Vector3(-x, 0f, -z).normalized, Vector3.up);

            var deck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            deck.name = "Deck";
            deck.transform.SetParent(pad.transform, false);
            deck.transform.localPosition = new Vector3(0f, -1.9f, 0f);
            deck.transform.localScale = new Vector3(6f, 0.06f, 6f);
            deck.GetComponent<Renderer>().sharedMaterial = asphalt;
            Object.DestroyImmediate(deck.GetComponent<Collider>());

            pads[i] = pad.transform;
        }

        return pads;
    }

    static void BuildManagers(Transform[] pads)
    {
        var managers = new GameObject("Managers");
        managers.transform.position = pads.Length > 0 ? pads[0].position : Vector3.zero;

        var mission = managers.AddComponent<MissionManager>();
        mission.launchPoints = pads;
        mission.launchPoint = pads.Length > 0 ? pads[0] : null;

        // A kamikaze drone clears roughly one target per run, so the rack has to
        // hold at least one per target plus a margin for crashes and misses. A
        // flat count regardless of the map's actual targets is what makes a
        // mission unwinnable.
        mission.droneCount = Mathf.Max(4, Mathf.CeilToInt(targetCount * 1.3f));

        managers.AddComponent<DroneHUD>();
    }
}
