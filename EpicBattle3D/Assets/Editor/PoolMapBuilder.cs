using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Pool arena from primitives, following the deathmatch brief.
///
/// 56 x 40 m, three parallel routes, no team sides and no bases:
///
///     Z+20 ┌──────────────┬───────────────┬──────────────┐
///          │ CHANGING     │  north deck   │  STANDS      │
///          │ ROOMS        │               │  (raised)    │
///     Z+7  ├─ ─ ─ ─ ─ ─ ─ ┼───────────────┼─ ─ ─ ─ ─ ─ ─ ┤
///          │ SHOWERS      │   THE POOL    │  CANOPY      │
///          │ (tight)      │   (sunken)    │  (open)      │
///     Z-7  ├─ ─ ─ ─ ─ ─ ─ ┼───────────────┼─ ─ ─ ─ ─ ─ ─ ┤
///          │ PLANT ROOM   │  south deck   │  LOUNGE      │
///     Z-20 └──────────────┴───────────────┴──────────────┘
///          X-28         X-10            X+10          X+28
///
/// Left wing is close quarters: locker banks and shower dividers set as
/// staggered corners rather than parallel lanes. Centre is the landmark — the
/// pool is a short low route that costs you your sightlines while you are in
/// it. Right wing is open, for longer shots, with a low stand that cannot see
/// into the left wing or across the map.
///
/// Both dividing walls are pierced by three gaps, so every route has at least
/// two ways in and out and nobody can be sealed into a fight.
///
/// The pool is shallow with ramps on all four sides rather than a deep pit with
/// stairs. Bots cannot jump, and a deep basin with narrow stepped exits is
/// exactly where they pile up and get stuck.
/// </summary>
public static class PoolMapBuilder
{
    // Arena bounds
    const float HalfWidth = 28f;    // along X
    const float HalfDepth = 20f;    // along Z
    const float WallHeight = 6f;
    const float SlabThickness = 3f;

    // The pool basin
    const float PoolMinX = -8f;
    const float PoolMaxX = 8f;
    const float PoolMinZ = -6f;
    const float PoolMaxZ = 6f;

    /// <summary>
    /// Shallow on purpose. Deep enough to break sightlines from the deck, but
    /// with a gentle ramp on every side so nothing can ever be trapped in it.
    /// </summary>
    const float PoolDepth = 0.9f;

    /// <summary>How far each ramp reaches into the basin from its edge.</summary>
    const float RampRun = 3.2f;

    // Route boundaries
    const float LeftWingX = -10f;
    const float RightWingX = 10f;

    static PoolProps.Palette palette;

    [MenuItem("Tools/Epic Battle 3D/2 - Build Pool Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        palette = LoadPalette();

        BuildLighting();
        BuildFloor();
        BuildPerimeter();
        BuildWingRoofs();
        BuildPool();
        BuildRouteDividers();
        BuildLeftWing();
        BuildCentre();
        BuildRightWing();

        List<Transform> spawns = BuildSpawnPoints();
        BuildManagers(spawns);
        BuildPlayer(spawns[0]);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Pool.unity");
        Debug.Log("Epic Battle 3D: arena saved to Assets/Scenes/Pool.unity");
    }

    static PoolProps.Palette LoadPalette()
    {
        return new PoolProps.Palette
        {
            tile = GeneratedMaterials.Load("Mat_PoolTile"),
            wallTile = GeneratedMaterials.Load("Mat_WallTile"),
            wall = GeneratedMaterials.Load("Mat_Wall"),
            concrete = GeneratedMaterials.Load("Mat_Concrete"),
            metal = GeneratedMaterials.Load("Mat_Metal"),
            plastic = GeneratedMaterials.Load("Mat_Plastic"),
            fabric = GeneratedMaterials.Load("Mat_Fabric"),
            wood = GeneratedMaterials.Load("Mat_Wood"),
            plant = GeneratedMaterials.Load("Mat_Plant"),
            accent = GeneratedMaterials.Load("Mat_Accent"),
            window = GeneratedMaterials.Load("Mat_Window")
        };
    }

    // ---------- shell ----------

    static void BuildLighting()
    {
        var lightGO = new GameObject("Sun");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        light.color = new Color(1f, 0.97f, 0.90f);
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.55f;   // soft shadows, no pitch-black corners
        lightGO.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        // Strong ambient from every direction. The brief asks for good visibility
        // and no dark corners, and the roofed wings get no sun at all.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.78f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.63f, 0.64f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.40f, 0.38f);
        RenderSettings.fog = false;

        BuildInteriorLights();
    }

    /// <summary>
    /// Ceiling lights for the roofed wings, where the sun cannot reach. Shadows
    /// are off: ten shadow-casting point lights would cost far more in a WebGL
    /// build than they are worth.
    /// </summary>
    static void BuildInteriorLights()
    {
        var group = MapBlocks.Group("InteriorLights");

        Vector3[] positions =
        {
            new Vector3(-24f, 4.4f, 14f),
            new Vector3(-16f, 4.4f, 14f),
            new Vector3(-24f, 4.4f, 2f),
            new Vector3(-16f, 4.4f, 0f),
            new Vector3(-24f, 4.4f, -11f),
            new Vector3(-15f, 4.4f, -14f),
            new Vector3(24f, 4.4f, 13f),
            new Vector3(16f, 4.4f, 2f),
            new Vector3(24f, 4.4f, -6f),
            new Vector3(16f, 4.4f, -16f)
        };

        foreach (Vector3 position in positions)
        {
            var lightGO = new GameObject("CeilingLight");
            lightGO.transform.SetParent(group.transform, false);
            lightGO.transform.position = position;

            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.98f, 0.93f);
            light.range = 20f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
        }
    }

    /// <summary>
    /// Four slabs arranged around the basin rather than one plane, which is what
    /// creates the hole the pool sits in.
    /// </summary>
    static void BuildFloor()
    {
        var group = MapBlocks.Group("Floor");
        Material floor = GeneratedMaterials.Load("Mat_Floor");
        float centreY = -SlabThickness * 0.5f;

        float westWidth = PoolMinX + HalfWidth;
        MapBlocks.BoxAt(group.transform, "Floor_West",
                        new Vector3(-HalfWidth + westWidth * 0.5f, centreY, 0f),
                        new Vector3(westWidth, SlabThickness, HalfDepth * 2f), floor);

        float eastWidth = HalfWidth - PoolMaxX;
        MapBlocks.BoxAt(group.transform, "Floor_East",
                        new Vector3(HalfWidth - eastWidth * 0.5f, centreY, 0f),
                        new Vector3(eastWidth, SlabThickness, HalfDepth * 2f), floor);

        float northDepth = HalfDepth - PoolMaxZ;
        MapBlocks.BoxAt(group.transform, "Floor_North",
                        new Vector3(0f, centreY, HalfDepth - northDepth * 0.5f),
                        new Vector3(PoolMaxX - PoolMinX, SlabThickness, northDepth), floor);

        float southDepth = PoolMinZ + HalfDepth;
        MapBlocks.BoxAt(group.transform, "Floor_South",
                        new Vector3(0f, centreY, -HalfDepth + southDepth * 0.5f),
                        new Vector3(PoolMaxX - PoolMinX, SlabThickness, southDepth), floor);
    }

    /// <summary>
    /// The hall's outer walls, tiled the way a real swimming baths is: a darker
    /// splash band around the bottom, white tiling above, and clerestory windows
    /// near the top. Only the structural wall carries a collider.
    /// </summary>
    static void BuildPerimeter()
    {
        var group = MapBlocks.Group("Perimeter");

        float length = HalfWidth * 2f + 2f;
        float depth = HalfDepth * 2f;

        BuildTiledWall(group.transform, "Wall_North", new Vector3(0f, 0f, HalfDepth + 0.5f),
                       new Vector2(length, 1f), true);
        BuildTiledWall(group.transform, "Wall_South", new Vector3(0f, 0f, -HalfDepth - 0.5f),
                       new Vector2(length, 1f), true);
        BuildTiledWall(group.transform, "Wall_East", new Vector3(HalfWidth + 0.5f, 0f, 0f),
                       new Vector2(1f, depth), false);
        BuildTiledWall(group.transform, "Wall_West", new Vector3(-HalfWidth - 0.5f, 0f, 0f),
                       new Vector2(1f, depth), false);
    }

    /// <param name="footprint">Wall footprint as (x size, z size).</param>
    /// <param name="runsAlongX">True for a wall running along X, false along Z.</param>
    static void BuildTiledWall(Transform parent, string name, Vector3 basePosition,
                               Vector2 footprint, bool runsAlongX)
    {
        const float splashBandHeight = 2.2f;

        MapBlocks.BoxAt(parent, name, basePosition + Vector3.up * (WallHeight * 0.5f),
                        new Vector3(footprint.x, WallHeight, footprint.y), palette.wallTile);

        float runLength = runsAlongX ? footprint.x : footprint.y;

        Vector3 bandSize = runsAlongX
            ? new Vector3(runLength, splashBandHeight, footprint.y + 0.12f)
            : new Vector3(footprint.x + 0.12f, splashBandHeight, runLength);

        var band = MapBlocks.BoxAt(parent, name + "_SplashBand",
                                   basePosition + Vector3.up * (splashBandHeight * 0.5f),
                                   bandSize, palette.tile);
        MapBlocks.NoCollision(band);

        int windows = Mathf.Max(2, Mathf.RoundToInt(runLength / 7f));
        for (int i = 0; i < windows; i++)
        {
            float offset = -runLength * 0.5f + runLength * (i + 0.5f) / windows;

            Vector3 position = runsAlongX
                ? new Vector3(basePosition.x + offset, 4.7f, basePosition.z)
                : new Vector3(basePosition.x, 4.7f, basePosition.z + offset);

            Vector3 size = runsAlongX
                ? new Vector3(runLength / windows * 0.62f, 1.5f, footprint.y + 0.16f)
                : new Vector3(footprint.x + 0.16f, 1.5f, runLength / windows * 0.62f);

            var window = MapBlocks.BoxAt(parent, name + "_Window" + i, position, size, palette.window);
            MapBlocks.NoCollision(MapBlocks.NoShadows(window));
        }
    }

    /// <summary>
    /// Roofs the two side wings and leaves the pool hall open to the sky.
    ///
    /// A fully closed hall reads better as a real complex, but it blocks the
    /// directional light and leaves the map dim and flat no matter how many
    /// point lights are added. Roofing only the wings keeps the pool sunlit,
    /// makes the enclosed routes feel enclosed, and reinforces how each side
    /// plays: dark and tight on the left, open and bright in the middle.
    /// </summary>
    static void BuildWingRoofs()
    {
        var group = MapBlocks.Group("Roof");
        const float roofHeight = 5.8f;

        float leftWidth = LeftWingX + HalfWidth;
        MapBlocks.BoxAt(group.transform, "Roof_LeftWing",
                        new Vector3(-HalfWidth + leftWidth * 0.5f, roofHeight, 0f),
                        new Vector3(leftWidth, 0.4f, HalfDepth * 2f), palette.concrete);

        float rightWidth = HalfWidth - RightWingX;
        MapBlocks.BoxAt(group.transform, "Roof_RightWing",
                        new Vector3(HalfWidth - rightWidth * 0.5f, roofHeight, 0f),
                        new Vector3(rightWidth, 0.4f, HalfDepth * 2f), palette.concrete);

        // Open beams over the pool hall: the frame of a glazed atrium roof, left
        // unglazed so the sun still reaches the water.
        for (int i = 0; i < 7; i++)
        {
            float z = -HalfDepth + 2.5f + i * ((HalfDepth * 2f - 5f) / 6f);
            var beam = MapBlocks.BoxAt(group.transform, "Beam" + i,
                                       new Vector3(0f, roofHeight, z),
                                       new Vector3(RightWingX - LeftWingX + 1f, 0.3f, 0.4f),
                                       palette.metal);
            MapBlocks.NoCollision(MapBlocks.NoShadows(beam));
        }
    }

    // ---------- the pool ----------

    /// <summary>
    /// The sunken pool: a shallow tiled basin with a ramp on every side.
    ///
    /// Ramps rather than steps, on all four sides rather than two, because bots
    /// cannot jump: any basin they can walk into must be one they can walk out
    /// of from wherever they happen to be standing.
    /// </summary>
    static void BuildPool()
    {
        var group = MapBlocks.Group("Pool");

        float width = PoolMaxX - PoolMinX;
        float depth = PoolMaxZ - PoolMinZ;

        MapBlocks.BoxAt(group.transform, "Basin", new Vector3(0f, -PoolDepth - 0.5f, 0f),
                        new Vector3(width, 1f, depth), palette.tile);

        BuildPoolRamps(group.transform, width, depth);
        BuildLaneMarkings(group.transform, width, depth);
        BuildDivingTower(group.transform);

        var water = MapBlocks.BoxAt(group.transform, "Water",
                                    new Vector3(0f, -PoolDepth + 0.42f, 0f),
                                    new Vector3(width - 0.2f, 0.06f, depth - 0.2f),
                                    GeneratedMaterials.Load("Mat_Water"));
        MapBlocks.NoCollision(water);
        MapBlocks.NoShadows(water);
    }

    static void BuildPoolRamps(Transform parent, float width, float depth)
    {
        // Wide ramps: a narrow one is a funnel, and a funnel is where bots jam.
        float sideRampWidth = depth * 0.72f;
        float endRampWidth = width * 0.72f;

        MapBlocks.Ramp(parent, "Ramp_West",
                       new Vector3(PoolMinX + RampRun, -PoolDepth, 0f),
                       new Vector3(PoolMinX, 0f, 0f), sideRampWidth, palette.tile);

        MapBlocks.Ramp(parent, "Ramp_East",
                       new Vector3(PoolMaxX - RampRun, -PoolDepth, 0f),
                       new Vector3(PoolMaxX, 0f, 0f), sideRampWidth, palette.tile);

        MapBlocks.Ramp(parent, "Ramp_North",
                       new Vector3(0f, -PoolDepth, PoolMaxZ - RampRun),
                       new Vector3(0f, 0f, PoolMaxZ), endRampWidth, palette.tile);

        MapBlocks.Ramp(parent, "Ramp_South",
                       new Vector3(0f, -PoolDepth, PoolMinZ + RampRun),
                       new Vector3(0f, 0f, PoolMinZ), endRampWidth, palette.tile);
    }

    /// <summary>
    /// Dark lane stripes on the basin floor. Decoration, but they are what makes
    /// the basin read as a swimming pool rather than a tiled pit.
    /// </summary>
    static void BuildLaneMarkings(Transform parent, float width, float depth)
    {
        Material line = GeneratedMaterials.Load("Mat_LaneMarking");
        const int lanes = 4;

        for (int i = 1; i < lanes; i++)
        {
            float z = -depth * 0.5f + depth * i / lanes;

            var stripe = MapBlocks.BoxAt(parent, "Lane" + i,
                                         new Vector3(0f, -PoolDepth + 0.02f, z),
                                         new Vector3(width - 1f, 0.02f, 0.25f), line);
            MapBlocks.NoCollision(MapBlocks.NoShadows(stripe));
        }
    }

    /// <summary>
    /// The diving platform: the tallest landmark on the map, visible from every
    /// route, and solid cover at the pool's edge. Not climbable — a perch over
    /// the open centre would dominate the whole arena.
    /// </summary>
    static void BuildDivingTower(Transform parent)
    {
        var group = MapBlocks.Group("DivingTower", parent);

        // On the north deck, clear of the divider walls at X = +-10, with the
        // board reaching south out over the water.
        const float z = PoolMaxZ + 1.6f;

        for (int i = 0; i < 2; i++)
        {
            float x = -1.3f + i * 2.6f;
            MapBlocks.Box(group.transform, "Leg" + i, new Vector3(x, 0f, z),
                          new Vector3(0.3f, 3.4f, 0.3f), palette.metal);
        }

        MapBlocks.BoxAt(group.transform, "Platform", new Vector3(0f, 3.5f, z),
                        new Vector3(2.6f, 0.2f, 2.2f), palette.plastic);

        MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Board",
            new Vector3(0f, 3.55f, z - 2.4f), new Vector3(0.9f, 0.12f, 2.6f), palette.plastic));

        MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Rail",
            new Vector3(0f, 4.2f, z + 1.0f), new Vector3(2.6f, 1.2f, 0.12f), palette.metal));
    }

    /// <summary>
    /// The walls separating the three routes, each pierced by three gaps so every
    /// route keeps at least two entrances and players can always rotate.
    /// </summary>
    static void BuildRouteDividers()
    {
        var group = MapBlocks.Group("RouteDividers");

        float[] gaps = { 13f, 0f, -13f };
        const float gapWidth = 5f;

        BuildPiercedWall(group.transform, "DividerWest", LeftWingX, gaps, gapWidth);
        BuildPiercedWall(group.transform, "DividerEast", RightWingX, gaps, gapWidth);
    }

    static void BuildPiercedWall(Transform parent, string name, float x, float[] gapCentres, float gapWidth)
    {
        var edges = new List<float> { -HalfDepth };

        var sorted = new List<float>(gapCentres);
        sorted.Sort();

        for (int i = 0; i < sorted.Count; i++)
        {
            edges.Add(sorted[i] - gapWidth * 0.5f);
            edges.Add(sorted[i] + gapWidth * 0.5f);
        }
        edges.Add(HalfDepth);

        for (int i = 0; i < edges.Count; i += 2)
        {
            float from = edges[i];
            float to = edges[i + 1];
            float length = to - from;
            if (length <= 0.1f) continue;

            MapBlocks.BoxAt(parent, name, new Vector3(x, WallHeight * 0.5f, (from + to) * 0.5f),
                            new Vector3(0.5f, WallHeight, length), palette.wallTile);
        }
    }

    // ---------- left wing: changing rooms, showers, plant room ----------

    static void BuildLeftWing()
    {
        var group = MapBlocks.Group("LeftWing");

        BuildChangingRooms(group.transform);
        BuildShowers(group.transform);
        BuildPlantRoom(group.transform);
    }

    static void BuildChangingRooms(Transform parent)
    {
        var group = MapBlocks.Group("ChangingRooms", parent);

        // Staggered rather than parallel, so the wing is a set of corners
        // instead of shooting lanes.
        PoolProps.LockerBank(group.transform, new Vector3(-22f, 0f, 15f), 8f, 0f, palette);
        PoolProps.LockerBank(group.transform, new Vector3(-15.5f, 0f, 11f), 6f, 90f, palette);
        PoolProps.LockerBank(group.transform, new Vector3(-25f, 0f, 8.5f), 5f, 90f, palette);

        PoolProps.Bench(group.transform, new Vector3(-19f, 0f, 12f), 0f, palette);
        PoolProps.Bench(group.transform, new Vector3(-21.5f, 0f, 6f), 90f, palette);

        PoolProps.ChangingCabin(group.transform, new Vector3(-26.5f, 0f, 18f), 0f, palette);
        PoolProps.ChangingCabin(group.transform, new Vector3(-24f, 0f, 18f), 0f, palette);
        PoolProps.ChangingCabin(group.transform, new Vector3(-21.5f, 0f, 18f), 0f, palette);

        PoolProps.VendingMachine(group.transform, new Vector3(-11.5f, 0f, 17.5f), -90f, palette);
        PoolProps.Planter(group.transform, new Vector3(-12.5f, 0f, 9f), palette);

        // Interior wall between changing area and showers, with a doorway.
        MapBlocks.BoxAt(group.transform, "InnerWall", new Vector3(-20f, 1.6f, 4f),
                        new Vector3(12f, 3.2f, 0.4f), palette.wallTile);
        MapBlocks.BoxAt(group.transform, "InnerWallStub", new Vector3(-27.2f, 1.6f, 4f),
                        new Vector3(1.6f, 3.2f, 0.4f), palette.wallTile);
    }

    static void BuildShowers(Transform parent)
    {
        var group = MapBlocks.Group("Showers", parent);

        // A row of dividers: tight, high cover, ideal for close-range fights.
        for (int i = 0; i < 5; i++)
        {
            float z = -4.5f + i * 2.2f;
            PoolProps.ShowerStall(group.transform, new Vector3(-25.5f, 0f, z), 90f, palette);
        }

        MapBlocks.BoxAt(group.transform, "ShowerBackWall", new Vector3(-27.2f, 1.5f, -0.5f),
                        new Vector3(0.4f, 3f, 12f), palette.wallTile);

        PoolProps.LowWall(group.transform, new Vector3(-19f, 0f, -1f), 7f, 90f, palette);
        PoolProps.LowWall(group.transform, new Vector3(-14f, 0f, 4.5f), 5f, 0f, palette);

        PoolProps.Puddle(group.transform, new Vector3(-23f, 0f, 1f), 2.4f, palette);
        PoolProps.Puddle(group.transform, new Vector3(-20.5f, 0f, -3.5f), 1.8f, palette);
        PoolProps.Puddle(group.transform, new Vector3(-13f, 0f, 0.5f), 2f, palette);
    }

    static void BuildPlantRoom(Transform parent)
    {
        var group = MapBlocks.Group("PlantRoom", parent);

        PoolProps.PumpUnit(group.transform, new Vector3(-24f, 0f, -10f), 0f, palette);
        PoolProps.PumpUnit(group.transform, new Vector3(-24f, 0f, -13.5f), 0f, palette);
        PoolProps.PumpUnit(group.transform, new Vector3(-19f, 0f, -16.5f), 90f, palette);

        PoolProps.Crate(group.transform, new Vector3(-14f, 0f, -12f), 15f, palette);
        PoolProps.Crate(group.transform, new Vector3(-15.5f, 0f, -13.5f), -20f, palette);
        PoolProps.Crate(group.transform, new Vector3(-26f, 0f, -6.5f), 0f, palette);

        // Overhead pipework: shorthand for a service area, high enough to clear
        // everyone's head.
        PoolProps.PipeRun(group.transform, new Vector3(-27.5f, 3.6f, -18f), new Vector3(-27.5f, 3.6f, -5f), 0.16f, palette);
        PoolProps.PipeRun(group.transform, new Vector3(-27.5f, 4f, -18f), new Vector3(-27.5f, 4f, -5f), 0.11f, palette);
        PoolProps.PipeRun(group.transform, new Vector3(-27.5f, 3.6f, -12f), new Vector3(-11f, 3.6f, -12f), 0.16f, palette);
        PoolProps.PipeRun(group.transform, new Vector3(-22f, 3.2f, -18.5f), new Vector3(-22f, 3.2f, -8f), 0.12f, palette);

        MapBlocks.BoxAt(group.transform, "PlantRoomWall", new Vector3(-18f, 1.6f, -7.5f),
                        new Vector3(0.4f, 3.2f, 9f), palette.wall);

        PoolProps.LowWall(group.transform, new Vector3(-12.5f, 0f, -16f), 6f, 90f, palette);
    }

    // ---------- centre: the pool deck ----------

    static void BuildCentre()
    {
        var group = MapBlocks.Group("Centre");

        // Lifeguard chair, off the pool's axis so it breaks the long
        // north-south sightline instead of sitting in the middle of it.
        PoolProps.LifeguardTower(group.transform, new Vector3(6f, 0f, 8.5f), 200f, palette);

        // North deck. Kept clear of the diving tower, which stands at Z = 7.6
        // spanning X -1.5 to 1.5.
        PoolProps.LowWall(group.transform, new Vector3(-5f, 0f, 10.5f), 6f, 0f, palette);
        PoolProps.Crate(group.transform, new Vector3(-8f, 0f, 14f), 25f, palette);
        PoolProps.Planter(group.transform, new Vector3(3.5f, 0f, 11f), palette);
        PoolProps.Planter(group.transform, new Vector3(-3f, 0f, 17.5f), palette);
        PoolProps.Bench(group.transform, new Vector3(5f, 0f, 15f), 0f, palette);

        // South deck, arranged differently so the two halves of the centre do
        // not play the same way.
        PoolProps.LowWall(group.transform, new Vector3(3f, 0f, -9.5f), 7f, 0f, palette);
        PoolProps.VendingMachine(group.transform, new Vector3(-5f, 0f, -10.5f), 0f, palette);
        PoolProps.Crate(group.transform, new Vector3(6.5f, 0f, -14f), -15f, palette);
        PoolProps.Bench(group.transform, new Vector3(-2f, 0f, -16.5f), 90f, palette);
        PoolProps.Planter(group.transform, new Vector3(-7f, 0f, -17f), palette);

        PoolProps.Puddle(group.transform, new Vector3(-5f, 0f, 7.6f), 3f, palette);
        PoolProps.Puddle(group.transform, new Vector3(-4f, 0f, -7.7f), 2.6f, palette);
        PoolProps.Puddle(group.transform, new Vector3(6f, 0f, 8.5f), 2.2f, palette);
    }

    // ---------- right wing: stands, canopy, lounge ----------

    static void BuildRightWing()
    {
        var group = MapBlocks.Group("RightWing");

        BuildStands(group.transform);
        BuildCanopy(group.transform);
        BuildLounge(group.transform);
    }

    /// <summary>
    /// Tiered seating rising to about 2 m. Kept low on purpose: high enough to
    /// be worth taking for the angle over the right wing, too low and too boxed
    /// in by the divider wall to see into the left wing or across the map.
    /// </summary>
    static void BuildStands(Transform parent)
    {
        var group = MapBlocks.Group("Stands", parent);

        const int tiers = 5;
        const float tierRise = 0.4f;
        const float tierRun = 1.5f;

        for (int i = 0; i < tiers; i++)
        {
            float height = tierRise * (i + 1);
            float z = 12f + i * tierRun;

            MapBlocks.BoxAt(group.transform, "Tier" + i,
                            new Vector3(19f, height * 0.5f, z),
                            new Vector3(15f, height, tierRun), palette.concrete);
        }

        // Ramp up the side so bots can reach the stands too.
        MapBlocks.Ramp(group.transform, "StandsRamp",
                       new Vector3(26.5f, 0.05f, 8f), new Vector3(26.5f, tierRise * tiers, 13.5f),
                       3.5f, palette.concrete);

        // Rail along the front edge: cover for anyone up there, and it stops the
        // top tier being a clean firing platform.
        MapBlocks.BoxAt(group.transform, "StandsRail", new Vector3(19f, 2.45f, 19.4f),
                        new Vector3(15f, 0.9f, 0.3f), palette.metal);

        PoolProps.Crate(group.transform, new Vector3(12.5f, 0f, 10.5f), 0f, palette);
        PoolProps.LowWall(group.transform, new Vector3(14f, 0f, 7.5f), 6f, 0f, palette);
    }

    /// <summary>
    /// A roof on posts over the middle of the right wing. It is the reason the
    /// stands cannot watch the whole map, and the posts double as cover.
    /// </summary>
    static void BuildCanopy(Transform parent)
    {
        var group = MapBlocks.Group("Canopy", parent);

        float[] postX = { 13f, 25f };
        float[] postZ = { -5f, 5f };

        foreach (float x in postX)
        {
            foreach (float z in postZ)
            {
                MapBlocks.BoxAt(group.transform, "Post", new Vector3(x, 1.9f, z),
                                new Vector3(0.45f, 3.8f, 0.45f), palette.metal);
            }
        }

        var roof = MapBlocks.BoxAt(group.transform, "Roof", new Vector3(19f, 3.95f, 0f),
                                   new Vector3(13.5f, 0.25f, 11.5f), palette.fabric);
        MapBlocks.NoShadows(roof);

        PoolProps.Crate(group.transform, new Vector3(19f, 0f, 0f), 40f, palette);
        PoolProps.LowWall(group.transform, new Vector3(22f, 0f, -4f), 5f, 90f, palette);
        PoolProps.Bench(group.transform, new Vector3(15f, 0f, 3f), 90f, palette);
        PoolProps.VendingMachine(group.transform, new Vector3(26.5f, 0f, 3f), 90f, palette);
    }

    static void BuildLounge(Transform parent)
    {
        var group = MapBlocks.Group("Lounge", parent);

        PoolProps.SunLounger(group.transform, new Vector3(14f, 0f, -9f), 12f, palette);
        PoolProps.SunLounger(group.transform, new Vector3(16.5f, 0f, -9.5f), -8f, palette);
        PoolProps.SunLounger(group.transform, new Vector3(22f, 0f, -14f), 95f, palette);
        PoolProps.SunLounger(group.transform, new Vector3(24.5f, 0f, -11f), 78f, palette);

        PoolProps.Parasol(group.transform, new Vector3(15.2f, 0f, -11.5f), palette);
        PoolProps.Parasol(group.transform, new Vector3(23.5f, 0f, -13f), palette);

        PoolProps.Planter(group.transform, new Vector3(12.5f, 0f, -14f), palette);
        PoolProps.Planter(group.transform, new Vector3(19f, 0f, -17.5f), palette);
        PoolProps.LowWall(group.transform, new Vector3(19f, 0f, -8f), 7f, 0f, palette);
        PoolProps.Crate(group.transform, new Vector3(26f, 0f, -17f), -25f, palette);
        PoolProps.Bench(group.transform, new Vector3(13f, 0f, -17f), 0f, palette);
    }

    // ---------- spawns ----------

    /// <summary>
    /// Ten spawn points spread over all three routes.
    ///
    /// None looks straight down a lane at another: each sits behind a divider
    /// wall, a locker bank or a prop, every one has cover within a couple of
    /// metres and at least two ways out, and none is under the stands. All of
    /// them are well clear of the pool basin, so nobody starts in the water.
    /// SpawnManager then picks whichever is farthest from anyone alive.
    /// </summary>
    static List<Transform> BuildSpawnPoints()
    {
        Material marker = GeneratedMaterials.Load("Mat_SpawnMarker");

        Vector3[] positions =
        {
            new Vector3(-25f, 0.2f, 12f),     // changing rooms, behind lockers
            new Vector3(-13f, 0.2f, 16.5f),   // changing rooms, near the doorway
            new Vector3(-22f, 0.2f, -2f),     // showers, behind the dividers
            new Vector3(-25f, 0.2f, -17f),    // plant room corner
            new Vector3(-13f, 0.2f, -17.5f),  // plant room exit to the south deck
            new Vector3(-4f, 0.2f, 17.5f),    // north deck, off the pool axis
            new Vector3(4.5f, 0.2f, -17.5f),  // south deck, offset from the north spawn
            new Vector3(13f, 0.2f, 17f),      // beside the stands
            new Vector3(26.5f, 0.2f, -2f),    // under the canopy, east edge
            new Vector3(16f, 0.2f, -17f)      // lounge
        };

        var spawns = new List<Transform>();
        var root = MapBlocks.Group("SpawnPoints");

        for (int i = 0; i < positions.Length; i++)
        {
            var spawn = new GameObject("Spawn_" + i);
            spawn.transform.SetParent(root.transform);
            spawn.transform.position = positions[i];

            // Face the middle of the arena so a fresh spawn looks toward the action.
            Vector3 toCentre = new Vector3(-positions[i].x, 0f, -positions[i].z);
            spawn.transform.rotation = toCentre.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toCentre.normalized, Vector3.up)
                : Quaternion.identity;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Marker";
            disc.transform.SetParent(spawn.transform, false);
            disc.transform.localPosition = new Vector3(0f, -0.19f, 0f);
            disc.transform.localScale = new Vector3(1.8f, 0.02f, 1.8f);
            MapBlocks.NoCollision(MapBlocks.NoShadows(disc));
            if (marker != null) disc.GetComponent<Renderer>().sharedMaterial = marker;

            spawns.Add(spawn.transform);
        }

        return spawns;
    }

    // ---------- gameplay objects ----------

    static void BuildManagers(List<Transform> spawns)
    {
        // Deliberately not at the world origin: that is the middle of the pool,
        // and anything that ever falls back to this object's position would end
        // up in the water.
        var managers = new GameObject("Managers");
        managers.transform.position = new Vector3(0f, 0f, HalfDepth + 4f);

        var spawnManager = managers.AddComponent<SpawnManager>();
        spawnManager.spawnPoints = spawns;

        managers.AddComponent<MatchManager>();
        managers.AddComponent<BotSpawner>();
    }

    static void BuildPlayer(Transform spawn)
    {
        Material bodyMaterial = GeneratedMaterials.Load("Mat_Player");
        Material headMaterial = GeneratedMaterials.Load("Mat_Player");

        // Kept in English so the scene file reads the same in every language;
        // MatchHUD translates it for display.
        var player = new GameObject("Player");
        player.transform.position = spawn.position;
        player.transform.rotation = spawn.rotation;

        int characterLayer = GameLayers.Character;
        if (characterLayer >= 0) player.layer = characterLayer;

        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.slopeLimit = 50f;
        controller.stepOffset = 0.45f;

        var cameraGO = new GameObject("PlayerCamera");
        cameraGO.transform.SetParent(player.transform, false);
        cameraGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var camera = cameraGO.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 250f;
        cameraGO.AddComponent<AudioListener>();

        // The weapon is drawn by its own camera on its own layer. Without this a
        // gun held close to the lens pokes through walls when you stand next to one.
        int weaponLayer = GameLayers.Weapon;
        if (weaponLayer >= 0) camera.cullingMask &= ~(1 << weaponLayer);

        var fps = player.AddComponent<FirstPersonController>();
        fps.cameraTransform = cameraGO.transform;

        var health = player.AddComponent<Health>();
        health.maxHealth = 100;
        health.respawnDelay = 3f;

        // A body with real hitboxes, so bots can land headshots on the player too.
        // The renderers are switched off: drawing your own head and arms in front
        // of a first-person camera looks wrong.
        CharacterModel.Parts parts = CharacterModel.Build(player, health, bodyMaterial, headMaterial, true);

        var weapon = player.AddComponent<WeaponController>();
        weapon.playerCamera = camera;
        weapon.view = BuildWeaponView(cameraGO.transform, camera, controller);

        var animator = player.AddComponent<CharacterAnimator>();
        animator.leftLegPivot = parts.leftLegPivot;
        animator.rightLegPivot = parts.rightLegPivot;
        animator.leftArmPivot = parts.leftArmPivot;
        animator.rightArmPivot = parts.rightArmPivot;

        // Dying drops the camera to the floor and rolls it, so death is felt
        // rather than just switching to a menu.
        var deathFall = player.AddComponent<DeathFall>();
        deathFall.model = parts.root;
        deathFall.cameraTransform = cameraGO.transform;

        // The walk cycle has to stop on death: it drives the limb pivots every
        // frame, which would keep animating a corpse while physics tumbles it.
        health.disableOnDeath = new MonoBehaviour[] { fps, weapon, animator };

        player.AddComponent<CursorRelease>();

        var playerHUD = player.AddComponent<PlayerHUD>();
        playerHUD.health = health;
        playerHUD.weapon = weapon;

        var matchHUD = player.AddComponent<MatchHUD>();
        matchHUD.player = player;
    }

    /// <summary>
    /// Builds the first-person weapon: a second camera that draws only the weapon
    /// layer on top of the main view, and the pistol model hanging in front of it.
    /// </summary>
    static WeaponView BuildWeaponView(Transform cameraTransform, Camera mainCamera,
                                      CharacterController owner)
    {
        int weaponLayer = GameLayers.Weapon;

        var weaponCameraGO = new GameObject("WeaponCamera");
        weaponCameraGO.transform.SetParent(cameraTransform, false);

        var weaponCamera = weaponCameraGO.AddComponent<Camera>();
        // Depth-only clear draws the weapon over the world without erasing it.
        weaponCamera.clearFlags = CameraClearFlags.Depth;
        weaponCamera.cullingMask = weaponLayer >= 0 ? (1 << weaponLayer) : 0;
        weaponCamera.depth = mainCamera.depth + 1;
        weaponCamera.nearClipPlane = 0.01f;
        weaponCamera.farClipPlane = 5f;
        weaponCamera.fieldOfView = 50f;

        var holder = new GameObject("WeaponView");
        holder.transform.SetParent(cameraTransform, false);

        Material gunMaterial = GeneratedMaterials.Load("Mat_Gun");
        Material gunAccent = GeneratedMaterials.Load("Mat_GunAccent");
        Transform muzzle = PistolModel.Build(holder.transform, gunMaterial, gunAccent, 1f);

        if (weaponLayer >= 0) GameLayers.ApplyRecursively(holder, weaponLayer);

        var view = holder.AddComponent<WeaponView>();
        view.muzzle = muzzle;
        view.owner = owner;
        holder.transform.localPosition = view.restPosition;

        return view;
    }
}
