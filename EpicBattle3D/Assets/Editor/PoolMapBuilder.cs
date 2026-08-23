using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Pool arena from primitives. Generating the level from code means
/// the whole map can be changed, reviewed and re-created without manual editor work.
///
/// The arena is 56 x 40 m and organised around three parallel routes, so players
/// always have somewhere else to be and no single spot owns the match:
///
///     Z+20 ┌──────────────┬───────────────┬──────────────┐
///          │ CHANGING     │  north deck   │  STANDS      │
///          │ ROOMS        │               │  (raised)    │
///     Z+6  ├─ ─ ─ ─ ─ ─ ─ ┼───────────────┼─ ─ ─ ─ ─ ─ ─ ┤
///          │ SHOWERS      │   THE POOL    │  CANOPY      │
///          │ (tight)      │   (sunken)    │  (open)      │
///     Z-6  ├─ ─ ─ ─ ─ ─ ─ ┼───────────────┼─ ─ ─ ─ ─ ─ ─ ┤
///          │ PLANT ROOM   │  south deck   │  LOUNGE      │
///     Z-20 └──────────────┴───────────────┴──────────────┘
///          X-28         X-10            X+10          X+28
///
/// Left is close-quarters: lockers, shower dividers and pump housings make a
/// maze of short corners. Centre is the landmark, with the pool itself as a
/// risky low route that costs you height and exit speed. Right is open, with a
/// raised stand that deliberately cannot see into the left wing or across the
/// whole map — the canopy and the dividing walls cut it off.
///
/// The walls between routes are pierced by three gaps each, so every route has
/// at least two ways in and out and nobody can be sealed into a fight.
/// </summary>
public static class PoolMapBuilder
{
    // Arena bounds
    const float HalfWidth = 28f;    // along X
    const float HalfDepth = 20f;    // along Z
    const float WallHeight = 6f;
    const float SlabThickness = 3f;

    // The pool basin
    const float PoolMinX = -9f;
    const float PoolMaxX = 9f;
    const float PoolMinZ = -7f;
    const float PoolMaxZ = 7f;

    /// <summary>
    /// Deep enough to feel like a real pool and to cost you your sightlines while
    /// you are in it, but still under the player's 1.2 m jump so getting out is
    /// never a trap — bots use the stepped ends, which they need since they
    /// cannot jump at all.
    /// </summary>
    const float PoolDepth = 1.15f;

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
        BuildRoof();
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
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.96f, 0.87f);
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.62f;   // soft shadows, no pitch-black corners
        lightGO.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        // Bright bounce from every direction so players stay readable in the
        // enclosed left wing without needing extra lights there.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.84f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.53f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.28f);
        RenderSettings.fog = false;

        BuildInteriorLights();
    }

    /// <summary>
    /// Ceiling lights for the roofed wings. The sun cannot reach under the roof,
    /// and requirement is explicitly "no excessively dark corners" — players have
    /// to stay readable in the close-quarters left wing, where most of the
    /// point-blank fighting happens.
    /// </summary>
    static void BuildInteriorLights()
    {
        var group = MapBlocks.Group("InteriorLights");

        Vector3[] positions =
        {
            new Vector3(-22f, 4.6f, 13f),
            new Vector3(-22f, 4.6f, 0f),
            new Vector3(-22f, 4.6f, -13f),
            new Vector3(-14f, 4.6f, 7f),
            new Vector3(-14f, 4.6f, -8f),
            new Vector3(20f, 4.6f, 12f),
            new Vector3(20f, 4.6f, -2f),
            new Vector3(20f, 4.6f, -14f)
        };

        foreach (Vector3 position in positions)
        {
            var lightGO = new GameObject("CeilingLight");
            lightGO.transform.SetParent(group.transform, false);
            lightGO.transform.position = position;

            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.97f, 0.90f);
            light.range = 17f;
            light.intensity = 1.25f;
            // Shadows off: eight shadow-casting point lights would cost far more
            // than they are worth in a WebGL build.
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
    /// splash band around the bottom with white tiling above it, and a run of
    /// clerestory windows near the roof.
    ///
    /// Only the lower band carries the collider. The upper band and the windows
    /// are decoration sitting on top of it, which keeps the collision geometry
    /// to four simple boxes.
    /// </summary>
    static void BuildPerimeter()
    {
        var group = MapBlocks.Group("Perimeter");

        float length = HalfWidth * 2f + 2f;
        float depth = HalfDepth * 2f;

        BuildTiledWall(group.transform, "Wall_North", new Vector3(0f, 0f, HalfDepth + 0.5f),
                       new Vector2(length, 1f), 0f);
        BuildTiledWall(group.transform, "Wall_South", new Vector3(0f, 0f, -HalfDepth - 0.5f),
                       new Vector2(length, 1f), 0f);
        BuildTiledWall(group.transform, "Wall_East", new Vector3(HalfWidth + 0.5f, 0f, 0f),
                       new Vector2(1f, depth), 90f);
        BuildTiledWall(group.transform, "Wall_West", new Vector3(-HalfWidth - 0.5f, 0f, 0f),
                       new Vector2(1f, depth), 90f);
    }

    /// <param name="footprint">Wall footprint as (x size, z size).</param>
    /// <param name="yaw">0 for a wall running along X, 90 for one running along Z.</param>
    static void BuildTiledWall(Transform parent, string name, Vector3 basePosition,
                               Vector2 footprint, float yaw)
    {
        const float splashBandHeight = 2.2f;

        // The full-height structural wall, tiled dark up to the splash line.
        MapBlocks.BoxAt(parent, name, basePosition + Vector3.up * (WallHeight * 0.5f),
                        new Vector3(footprint.x, WallHeight, footprint.y), palette.wallTile);

        bool runsAlongX = Mathf.Approximately(yaw, 0f);
        float runLength = runsAlongX ? footprint.x : footprint.y;

        // Splash band: a thin skin over the lower part of the wall.
        Vector3 bandSize = runsAlongX
            ? new Vector3(runLength, splashBandHeight, footprint.y + 0.12f)
            : new Vector3(footprint.x + 0.12f, splashBandHeight, runLength);

        var band = MapBlocks.BoxAt(parent, name + "_SplashBand",
                                   basePosition + Vector3.up * (splashBandHeight * 0.5f),
                                   bandSize, palette.tile);
        MapBlocks.NoCollision(band);

        // Clerestory windows just under the roof, the daylight source a real
        // pool hall has. Emissive-looking pale panels, not actual openings.
        int windows = Mathf.Max(2, Mathf.RoundToInt(runLength / 7f));
        for (int i = 0; i < windows; i++)
        {
            float offset = -runLength * 0.5f + runLength * (i + 0.5f) / windows;

            Vector3 position = runsAlongX
                ? new Vector3(basePosition.x + offset, 4.9f, basePosition.z)
                : new Vector3(basePosition.x, 4.9f, basePosition.z + offset);

            Vector3 size = runsAlongX
                ? new Vector3(runLength / windows * 0.62f, 1.4f, footprint.y + 0.16f)
                : new Vector3(footprint.x + 0.16f, 1.4f, runLength / windows * 0.62f);

            var window = MapBlocks.BoxAt(parent, name + "_Window" + i, position, size, palette.window);
            MapBlocks.NoCollision(MapBlocks.NoShadows(window));
        }
    }

    const float RoofHeight = 6.2f;

    /// <summary>
    /// Roofs the whole arena, turning it into an indoor pool hall like the
    /// reference map.
    ///
    /// The roof is built as strips with gaps between them rather than one solid
    /// slab: the gaps are skylights, and they are the only way sunlight reaches
    /// the floor once the hall is closed in. A single unbroken roof would kill
    /// the directional light and leave the entire map in flat ambient gloom.
    /// Ceiling lights fill in the rest.
    /// </summary>
    static void BuildRoof()
    {
        var group = MapBlocks.Group("Roof");

        const int strips = 7;
        const float skylightWidth = 2.6f;

        float span = HalfDepth * 2f;
        float stripDepth = (span - skylightWidth * (strips - 1)) / strips;

        for (int i = 0; i < strips; i++)
        {
            float z = -HalfDepth + stripDepth * 0.5f + i * (stripDepth + skylightWidth);

            MapBlocks.BoxAt(group.transform, "RoofStrip" + i,
                            new Vector3(0f, RoofHeight, z),
                            new Vector3(HalfWidth * 2f + 2f, 0.4f, stripDepth), palette.concrete);
        }

        // Cross beams spanning the skylights, so the gaps read as a glazed roof
        // frame rather than as holes in the ceiling.
        for (int i = 0; i < strips - 1; i++)
        {
            float z = -HalfDepth + stripDepth + skylightWidth * 0.5f + i * (stripDepth + skylightWidth);

            for (int j = 0; j < 5; j++)
            {
                float x = -HalfWidth + 6f + j * (HalfWidth * 2f - 12f) / 4f;
                var beam = MapBlocks.BoxAt(group.transform, "Beam",
                                           new Vector3(x, RoofHeight, z),
                                           new Vector3(0.3f, 0.25f, skylightWidth), palette.metal);
                MapBlocks.NoCollision(MapBlocks.NoShadows(beam));
            }
        }
    }

    /// <summary>
    /// The sunken pool: tiled basin, steps at both ends, a rim you can vault, and
    /// a water surface that is decoration only.
    /// </summary>
    static void BuildPool()
    {
        var group = MapBlocks.Group("Pool");

        float width = PoolMaxX - PoolMinX;
        float depth = PoolMaxZ - PoolMinZ;

        MapBlocks.BoxAt(group.transform, "Basin", new Vector3(0f, -PoolDepth - 0.5f, 0f),
                        new Vector3(width, 1f, depth), palette.tile);

        // Steps at each end. Bots cannot jump, so both ways out are stepped
        // rather than a wall they would mill about beneath.
        MapBlocks.Steps(group.transform, "StepsWest",
                        new Vector3(PoolMinX + 0.4f, -PoolDepth, 0f), Vector3.right,
                        3, PoolDepth / 3f, 0.55f, 4.5f, palette.tile);

        MapBlocks.Steps(group.transform, "StepsEast",
                        new Vector3(PoolMaxX - 0.4f, -PoolDepth, 0f), Vector3.left,
                        3, PoolDepth / 3f, 0.55f, 4.5f, palette.tile);

        BuildPoolRim(group.transform);
        BuildLaneMarkings(group.transform, width, depth);
        BuildDivingTower(group.transform);

        var water = MapBlocks.BoxAt(group.transform, "Water",
                                    new Vector3(0f, -PoolDepth + 0.55f, 0f),
                                    new Vector3(width - 0.2f, 0.06f, depth - 0.2f),
                                    GeneratedMaterials.Load("Mat_Water"));
        MapBlocks.NoCollision(water);
        MapBlocks.NoShadows(water);
    }

    /// <summary>
    /// Dark lane stripes on the basin floor. Pure decoration, but they are what
    /// makes the basin read as a swimming pool rather than a tiled pit.
    /// </summary>
    static void BuildLaneMarkings(Transform parent, float width, float depth)
    {
        Material line = GeneratedMaterials.Load("Mat_LaneMarking");
        const int lanes = 5;

        for (int i = 1; i < lanes; i++)
        {
            float z = -depth * 0.5f + depth * i / lanes;

            var stripe = MapBlocks.BoxAt(parent, "Lane" + i,
                                         new Vector3(0f, -PoolDepth + 0.02f, z),
                                         new Vector3(width - 0.6f, 0.02f, 0.28f), line);
            MapBlocks.NoCollision(MapBlocks.NoShadows(stripe));
        }
    }

    /// <summary>
    /// The diving platform at the deep end: the map's tallest landmark, visible
    /// from every route, and solid cover at the pool's edge. Deliberately not
    /// climbable — a perch over the open centre would dominate the whole arena.
    /// </summary>
    static void BuildDivingTower(Transform parent)
    {
        var group = MapBlocks.Group("DivingTower", parent);
        float x = PoolMinX - 1.4f;

        for (int i = 0; i < 2; i++)
        {
            float z = -1.2f + i * 2.4f;
            MapBlocks.Box(group.transform, "Leg" + i, new Vector3(x, 0f, z),
                          new Vector3(0.32f, 3.2f, 0.32f), palette.metal);
        }

        MapBlocks.BoxAt(group.transform, "Platform", new Vector3(x + 0.7f, 3.3f, 0f),
                        new Vector3(2.6f, 0.2f, 2.2f), palette.plastic);

        MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "RailBack",
            new Vector3(x - 0.5f, 3.9f, 0f), new Vector3(0.12f, 1.2f, 2.2f), palette.metal));

        // The board itself, jutting out over the water.
        MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Board",
            new Vector3(x + 2.6f, 3.35f, 0f), new Vector3(2.4f, 0.12f, 0.85f), palette.plastic));

        MapBlocks.Steps(group.transform, "TowerSteps",
                        new Vector3(x - 1.6f, 0f, 0f), Vector3.right,
                        4, 0.42f, 0.42f, 1.6f, palette.metal);
    }

    /// <summary>
    /// A raised lip around the basin, broken where the steps are. Low enough to
    /// step over, high enough to crouch behind at the pool's edge.
    /// </summary>
    static void BuildPoolRim(Transform parent)
    {
        const float rimHeight = 0.34f;
        const float rimThickness = 0.4f;
        const float gap = 5f;   // opening in front of each set of steps

        float width = PoolMaxX - PoolMinX;

        MapBlocks.Box(parent, "Rim_North", new Vector3(0f, 0f, PoolMaxZ + rimThickness * 0.5f),
                      new Vector3(width + rimThickness * 2f, rimHeight, rimThickness), palette.concrete);
        MapBlocks.Box(parent, "Rim_South", new Vector3(0f, 0f, PoolMinZ - rimThickness * 0.5f),
                      new Vector3(width + rimThickness * 2f, rimHeight, rimThickness), palette.concrete);

        float sideLength = (PoolMaxZ - PoolMinZ - gap) * 0.5f;
        float sideOffset = gap * 0.5f + sideLength * 0.5f;

        foreach (float x in new[] { PoolMinX - rimThickness * 0.5f, PoolMaxX + rimThickness * 0.5f })
        {
            MapBlocks.Box(parent, "Rim_Side", new Vector3(x, 0f, sideOffset),
                          new Vector3(rimThickness, rimHeight, sideLength), palette.concrete);
            MapBlocks.Box(parent, "Rim_Side", new Vector3(x, 0f, -sideOffset),
                          new Vector3(rimThickness, rimHeight, sideLength), palette.concrete);
        }
    }

    /// <summary>
    /// The walls separating the three routes. Each is built as segments with three
    /// gaps, so every route keeps at least two entrances and players can always
    /// rotate rather than being funnelled into one doorway.
    /// </summary>
    static void BuildRouteDividers()
    {
        var group = MapBlocks.Group("RouteDividers");

        // Gap centres along Z, shared by both dividers.
        float[] gaps = { 13f, 0f, -13f };
        const float gapWidth = 4.5f;

        BuildPiercedWall(group.transform, "DividerWest", LeftWingX, gaps, gapWidth);
        BuildPiercedWall(group.transform, "DividerEast", RightWingX, gaps, gapWidth);
    }

    /// <summary>Builds a wall along X = <paramref name="x"/> with openings at the given Z centres.</summary>
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
                            new Vector3(0.5f, WallHeight, length), palette.wall);
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

        // Locker banks form the corridors. Staggered rather than parallel, so the
        // wing is a set of corners instead of shooting lanes.
        PoolProps.LockerBank(group.transform, new Vector3(-22f, 0f, 14f), 8f, 0f, palette);
        PoolProps.LockerBank(group.transform, new Vector3(-15.5f, 0f, 10f), 6f, 90f, palette);
        PoolProps.LockerBank(group.transform, new Vector3(-24f, 0f, 7.5f), 5f, 90f, palette);

        PoolProps.Bench(group.transform, new Vector3(-19f, 0f, 11.5f), 0f, palette);
        PoolProps.Bench(group.transform, new Vector3(-21f, 0f, 5f), 90f, palette);

        PoolProps.ChangingCabin(group.transform, new Vector3(-26f, 0f, 17.5f), 0f, palette);
        PoolProps.ChangingCabin(group.transform, new Vector3(-23.5f, 0f, 17.5f), 0f, palette);
        PoolProps.ChangingCabin(group.transform, new Vector3(-21f, 0f, 17.5f), 0f, palette);

        PoolProps.VendingMachine(group.transform, new Vector3(-11.5f, 0f, 17f), -90f, palette);
        PoolProps.Planter(group.transform, new Vector3(-12.5f, 0f, 8f), palette);

        // A short interior wall splitting the changing area from the shower block,
        // with a doorway rather than a full seal.
        MapBlocks.BoxAt(group.transform, "InnerWall", new Vector3(-19f, 1.6f, 3.5f),
                        new Vector3(14f, 3.2f, 0.4f), palette.wallTile);
        MapBlocks.BoxAt(group.transform, "InnerWallStub", new Vector3(-27f, 1.6f, 3.5f),
                        new Vector3(2f, 3.2f, 0.4f), palette.wallTile);
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
        PoolProps.LowWall(group.transform, new Vector3(-14f, 0f, 4f), 5f, 0f, palette);

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

        // Overhead pipework: the visual shorthand for a service area, and it is
        // high enough not to interfere with anyone's movement.
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

        // The lifeguard chair, off the pool's axis so it breaks the long
        // north-south sightline instead of sitting in the middle of it.
        // Kept clear of the widened basin (now +-9 x +-7).
        PoolProps.LifeguardTower(group.transform, new Vector3(-5.5f, 0f, 9.5f), 160f, palette);

        // North deck cover.
        PoolProps.LowWall(group.transform, new Vector3(2.5f, 0f, 10f), 6f, 0f, palette);
        PoolProps.Crate(group.transform, new Vector3(6.5f, 0f, 14f), 25f, palette);
        PoolProps.Planter(group.transform, new Vector3(-2f, 0f, 15f), palette);
        PoolProps.Planter(group.transform, new Vector3(2f, 0f, 17.5f), palette);
        PoolProps.Bench(group.transform, new Vector3(-6.5f, 0f, 16f), 0f, palette);

        // South deck cover, deliberately arranged differently so the two halves
        // of the centre do not play the same way.
        PoolProps.LowWall(group.transform, new Vector3(-3f, 0f, -10f), 7f, 0f, palette);
        PoolProps.VendingMachine(group.transform, new Vector3(5f, 0f, -11f), 180f, palette);
        PoolProps.Crate(group.transform, new Vector3(-6.5f, 0f, -15f), -15f, palette);
        PoolProps.Bench(group.transform, new Vector3(2f, 0f, -16.5f), 90f, palette);
        PoolProps.Planter(group.transform, new Vector3(7f, 0f, -17f), palette);

        // Wet patches on the deck, kept outside the basin footprint.
        PoolProps.Puddle(group.transform, new Vector3(0f, 0f, 8.6f), 3f, palette);
        PoolProps.Puddle(group.transform, new Vector3(-4f, 0f, -8.7f), 2.6f, palette);
        PoolProps.Puddle(group.transform, new Vector3(6f, 0f, 9.2f), 2.2f, palette);
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
    /// Tiered seating rising to about 2.2 m. Kept low on purpose: high enough to
    /// be worth taking for the angle over the right wing, too low and too boxed in
    /// by the divider wall to see into the left wing or across the whole map.
    /// </summary>
    static void BuildStands(Transform parent)
    {
        var group = MapBlocks.Group("Stands", parent);

        const int tiers = 5;
        const float tierRise = 0.44f;
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
                       new Vector3(26f, 0.05f, 8f), new Vector3(26f, tierRise * tiers, 13.5f),
                       3.5f, palette.concrete);

        // A rail along the front edge: cover for anyone up there, and it stops
        // the top tier being a clean firing platform.
        MapBlocks.BoxAt(group.transform, "StandsRail", new Vector3(19f, 2.55f, 19.4f),
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
    /// None of them looks straight down a lane at another: each sits behind a
    /// divider wall, a locker bank or a prop, and every one has cover within a
    /// couple of metres and at least two ways out. SpawnManager then picks
    /// whichever is farthest from anyone alive, so respawn kills stay rare.
    /// </summary>
    static List<Transform> BuildSpawnPoints()
    {
        Material marker = GeneratedMaterials.Load("Mat_SpawnMarker");

        Vector3[] positions =
        {
            new Vector3(-25f, 0.2f, 11f),     // changing rooms, behind lockers
            new Vector3(-13f, 0.2f, 15.5f),   // changing rooms, near the doorway
            new Vector3(-22f, 0.2f, -2f),     // showers, behind the dividers
            new Vector3(-25f, 0.2f, -17f),    // plant room corner
            new Vector3(-13f, 0.2f, -17.5f),  // plant room exit to the south deck
            new Vector3(-3f, 0.2f, 17.5f),    // north deck, off the pool axis
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
        var managers = new GameObject("Managers");

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
        deathFall.leftLegPivot = parts.leftLegPivot;
        deathFall.rightLegPivot = parts.rightLegPivot;
        deathFall.leftArmPivot = parts.leftArmPivot;
        deathFall.rightArmPivot = parts.rightArmPivot;
        deathFall.cameraTransform = cameraGO.transform;

        // The walk cycle must stop on death, or it would fight DeathFall for
        // control of the same limb pivots.
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
