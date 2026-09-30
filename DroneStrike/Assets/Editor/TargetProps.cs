using UnityEngine;

/// <summary>
/// The vehicles and structures the drone is sent after, assembled from primitives.
///
/// Two rules the whole file follows.
///
/// Unity's cylinder is 2 units tall and 1 across at scale 1, so a wheel of
/// diameter D and width W is scale (D, W/2, D) rotated 90° about Z. Getting
/// that wrong is what turns wheels into thin spikes, so every wheel goes
/// through <see cref="AddWheel"/> rather than being scaled by hand.
///
/// Parts have to touch. A roof floated above the crates it covers, or a turret
/// hovering over a hull, reads as broken immediately — so heights are derived
/// from the parts below them instead of typed in independently.
///
/// Silhouettes are kept distinct — a tank is long and low with a gun, a truck is
/// tall and boxy, a depot is a stack under a tarp — because from a hundred
/// metres up the outline is all the pilot has to go on.
/// </summary>
public static class TargetProps
{
    public class Palette
    {
        public Material vehicle;
        public Material vehicleDark;
        public Material crate;
        public Material concrete;
        public Material metal;
        public Material roof;

        // Vehicle surfaces that must not all be the same matte olive.
        public Material paint;
        public Material glass;
        public Material rubber;
        public Material gap;
        public Material chrome;
        public Material headlamp;
        public Material hazardRed;
    }

    // ---------- armoured vehicle ----------

    /// <summary>
    /// Tracked armour. Uses the downloaded tank model if it has been imported
    /// (Assets/Resources/Models/Tank.glb, via the com.unity.cloud.gltfast
    /// package), otherwise builds the same silhouette from primitives.
    ///
    /// The model's own scale and facing came from a public model site and have
    /// not been checked in the editor — <see cref="ModelScale"/> and
    /// <see cref="ModelYawOffset"/> are the two knobs to turn if it comes in
    /// too big, too small, or facing sideways.
    /// </summary>
    const float ModelScale = 1f;

    /// <summary>
    /// Tank.glb is authored with its hull along local X and the gun toward +X.
    /// Everything else assumes +Z is forward — above all Target.IsWeakHit and
    /// the weak-rear strip at local -Z — so at 0 the "rear" was really the
    /// left flank. Confirmed from the Capture Review Shots render.
    /// </summary>
    const float ModelYawOffset = -90f;

    /// <summary>Length a real main battle tank comes out at, in metres.</summary>
    const float TankFootprint = 7.2f;

    public static Target ArmouredVehicle(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        GameObject root = CreateRoot(parent, "ArmouredVehicle", position, yaw,
                                     Target.Kind.ArmouredVehicle,
                                     new Vector3(3.4f, 2.7f, 7.2f), new Vector3(0f, 1.35f, 0f));

        GameObject tankModel = ModelLibrary.Instantiate("Tank", root.transform, ModelScale, ModelYawOffset);
        if (tankModel != null)
        {
            NormalizeModelSize(root, tankModel, TankFootprint);
            RecentreModelOnGround(root, tankModel);
            FitColliderToModel(root, tankModel);
            return root.GetComponent<Target>();
        }

        BuildArmouredVehiclePrimitives(root, palette);
        return root.GetComponent<Target>();
    }

    static void BuildArmouredVehiclePrimitives(GameObject root, Palette palette)
    {
        const float trackHeight = 0.8f;
        const float trackTop = trackHeight;          // 0.8
        const float hullHeight = 0.75f;
        const float hullTop = trackTop + hullHeight; // 1.55
        const float turretHeight = 0.7f;

        // Tracks down each side, and the road wheels inside them.
        foreach (float side in new[] { -1.35f, 1.35f })
        {
            AddPart(root, "Track", new Vector3(side, trackHeight * 0.5f, 0f),
                    new Vector3(0.62f, trackHeight, 6.6f), palette.vehicleDark);

            for (int i = 0; i < 5; i++)
            {
                float z = -2.4f + i * 1.2f;
                AddWheel(root, "RoadWheel", new Vector3(side, 0.42f, z), 0.72f, 0.34f, palette.metal);
            }
        }

        // Hull sits directly on the tracks.
        AddPart(root, "Hull", new Vector3(0f, trackTop + hullHeight * 0.5f, -0.2f),
                new Vector3(2.9f, hullHeight, 6.0f), palette.vehicle);

        // Sloped glacis plate at the front — the detail that reads as "tank"
        // more than anything except the gun.
        GameObject glacis = AddPart(root, "Glacis", new Vector3(0f, trackTop + 0.3f, 3.0f),
                                    new Vector3(2.9f, 0.22f, 1.9f), palette.vehicle);
        glacis.transform.localRotation = Quaternion.Euler(38f, 0f, 0f);

        // Turret, slightly back of centre, sitting on the hull roof.
        AddPart(root, "Turret", new Vector3(0f, hullTop + turretHeight * 0.5f, -0.7f),
                new Vector3(2.1f, turretHeight, 2.9f), palette.vehicle);

        AddPart(root, "TurretRear", new Vector3(0f, hullTop + turretHeight * 0.5f, -2.1f),
                new Vector3(1.5f, turretHeight * 0.8f, 0.7f), palette.vehicleDark);

        // Mantlet and gun, level with the turret's centre height.
        float gunHeight = hullTop + turretHeight * 0.55f;
        AddPart(root, "Mantlet", new Vector3(0f, gunHeight, 0.75f),
                new Vector3(0.9f, 0.5f, 0.7f), palette.vehicleDark);

        GameObject barrel = AddPart(root, "Barrel", new Vector3(0f, gunHeight, 2.7f),
                                    new Vector3(0.2f, 1.9f, 0.2f), palette.vehicleDark,
                                    PrimitiveType.Cylinder);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        AddPart(root, "Hatch", new Vector3(-0.5f, hullTop + turretHeight + 0.06f, -0.9f),
                new Vector3(0.7f, 0.12f, 0.7f), palette.vehicleDark);
    }

    // ---------- truck ----------

    /// <summary>Length of the cargo truck, metres.</summary>
    const float TruckFootprint = 7.2f;

    /// <summary>
    /// Truck.glb (M939) is authored along local X with the cab toward +X,
    /// like Tank.glb — rotated so the cab faces +Z, which is what patrol
    /// headings and parking layouts assume.
    /// </summary>
    const float TruckYawOffset = -90f;

    /// <summary>
    /// Six-wheeled army cargo truck.
    ///
    /// The previous one read as "two boxes on wheels": a cab block, a tarp
    /// block, one matte olive over everything. This one is built the way the
    /// real thing is put together — a ladder frame on three axles, a separate
    /// cab with a bonnet, glass, grille and bumper, arches over every wheel,
    /// and a slatted cargo bed under a tarp on hoops — and each surface has its
    /// own material: satin paint on the cab, matte canvas, dark glossy glass,
    /// matte rubber tyres with metal hubs, near-black in the seams.
    ///
    /// Every height is derived from the part below it (wheel → frame → cab
    /// floor / bed floor), so nothing floats and nothing sinks into the frame.
    /// Local +Z is forward.
    /// </summary>
    public static Target Truck(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        GameObject root = CreateRoot(parent, "Truck", position, yaw,
                                     Target.Kind.LightVehicle,
                                     new Vector3(2.7f, 3.1f, 7.4f), new Vector3(0f, 1.55f, 0f));

        // The downloaded M939 (CC-BY, see CREDITS.txt) when present; the
        // procedural truck below is the fallback for a project without it.
        GameObject truckModel = ModelLibrary.Instantiate("Truck", root.transform, 1f, TruckYawOffset);
        if (truckModel != null)
        {
            NormalizeModelSize(root, truckModel, TruckFootprint);
            RecentreModelOnGround(root, truckModel);
            FitColliderToModel(root, truckModel);
            return root.GetComponent<Target>();
        }

        Material paint = palette.paint != null ? palette.paint : palette.vehicle;
        Material canvas = palette.vehicle;
        Material dark = palette.vehicleDark;
        Material gap = palette.gap != null ? palette.gap : dark;
        Material glass = palette.glass != null ? palette.glass : dark;
        Material rubber = palette.rubber != null ? palette.rubber : dark;
        Material chrome = palette.chrome != null ? palette.chrome : palette.metal;
        Material lamp = palette.headlamp != null ? palette.headlamp : chrome;
        Material tail = palette.hazardRed != null ? palette.hazardRed : dark;

        const float wheelDiameter = 1.1f;
        const float wheelWidth = 0.42f;
        const float axle = wheelDiameter * 0.5f;          // 0.55
        const float track = 1.08f;                          // wheel centre from the middle
        const float frameHeight = 0.24f;
        const float frameBottom = 0.66f;
        const float frameTop = frameBottom + frameHeight;   // 0.90
        const float front = 3.55f;                          // bumper face

        // Ladder frame: two rails and cross members, visible between the wheels.
        foreach (float x in new[] { -0.55f, 0.55f })
            AddPart(root, "FrameRail", new Vector3(x, frameBottom + frameHeight * 0.5f, -0.15f),
                    new Vector3(0.16f, frameHeight, 6.7f), gap);
        foreach (float z in new[] { 2.6f, 0.9f, -0.9f, -2.7f })
            AddPart(root, "CrossMember", new Vector3(0f, frameBottom + frameHeight * 0.5f, z),
                    new Vector3(1.1f, frameHeight * 0.7f, 0.14f), gap);

        // Wheels: rubber tyre, metal hub, dark hub cap.
        float[] axles = { 2.75f, -1.25f, -2.55f };
        foreach (float z in axles)
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 centre = new Vector3(side * track, axle, z);
                AddWheel(root, "Tyre", centre, wheelDiameter, wheelWidth, rubber);
                AddWheel(root, "Hub", centre + new Vector3(side * 0.13f, 0f, 0f), 0.5f, 0.2f, chrome);
                AddWheel(root, "HubCap", centre + new Vector3(side * 0.24f, 0f, 0f), 0.2f, 0.04f, gap);
            }

        // Front arches are part of the cab's wings; rear arches hang off the bed.
        foreach (float z in axles)
            foreach (float side in new[] { -1f, 1f })
                AddArch(root, new Vector3(side * track, axle, z), wheelDiameter,
                        z > 0f ? paint : dark);

        // Cab: floor on the frame, a separate body with glass on three sides,
        // door seams, and a roof with a little overhang.
        // Cab and bed ride on mounts above the arches (tyre top 1.10, arch
        // 1.20), the way a six-wheeled army truck does - otherwise the tyres
        // cut straight through the floor.
        const float bodyFloor = 1.22f;
        const float cabFloor = bodyFloor;
        const float cabHeight = 1.62f;
        const float cabBack = 1.05f;
        const float cabFront = 2.72f;
        float cabDepth = cabFront - cabBack;
        float cabMidZ = (cabFront + cabBack) * 0.5f;
        AddPart(root, "CabMount", new Vector3(0f, (frameTop + cabFloor) * 0.5f, cabMidZ),
                new Vector3(1.4f, cabFloor - frameTop, cabDepth - 0.2f), gap);
        AddPart(root, "Cab", new Vector3(0f, cabFloor + cabHeight * 0.5f, cabMidZ),
                new Vector3(2.3f, cabHeight, cabDepth), paint);
        AddPart(root, "CabRoof", new Vector3(0f, cabFloor + cabHeight + 0.05f, cabMidZ + 0.04f),
                new Vector3(2.38f, 0.1f, cabDepth + 0.12f), paint);

        float glassY = cabFloor + cabHeight - 0.45f;
        AddPart(root, "Windscreen", new Vector3(0f, glassY, cabFront + 0.005f),
                new Vector3(2.0f, 0.62f, 0.04f), glass);
        AddPart(root, "WindscreenPillar", new Vector3(0f, glassY, cabFront + 0.02f),
                new Vector3(0.07f, 0.62f, 0.03f), paint);
        foreach (float side in new[] { -1f, 1f })
        {
            AddPart(root, "SideWindow", new Vector3(side * 1.152f, glassY, cabMidZ + 0.25f),
                    new Vector3(0.04f, 0.55f, 0.85f), glass);
            AddPart(root, "DoorSeam", new Vector3(side * 1.153f, cabFloor + cabHeight * 0.45f, cabBack + 0.12f),
                    new Vector3(0.03f, cabHeight * 0.85f, 0.03f), gap);
            AddPart(root, "DoorHandle", new Vector3(side * 1.16f, cabFloor + 0.75f, cabMidZ - 0.1f),
                    new Vector3(0.04f, 0.04f, 0.18f), chrome);
            AddPart(root, "Step", new Vector3(side * 1.12f, frameBottom - 0.02f, cabMidZ),
                    new Vector3(0.3f, 0.05f, 0.5f), gap);
        }
        AddPart(root, "RearWindow", new Vector3(0f, glassY, cabBack - 0.005f),
                new Vector3(1.0f, 0.38f, 0.04f), glass);

        // Bonnet ahead of the cab, grille, headlamps and bumper on the nose.
        const float bonnetHeight = 0.82f;
        float bonnetDepth = front - 0.12f - cabFront;
        float bonnetZ = cabFront + bonnetDepth * 0.5f;
        AddPart(root, "Bonnet", new Vector3(0f, frameTop + bonnetHeight * 0.5f, bonnetZ),
                new Vector3(1.7f, bonnetHeight, bonnetDepth), paint);
        AddPart(root, "BonnetSeam", new Vector3(0f, frameTop + bonnetHeight + 0.005f, bonnetZ),
                new Vector3(0.03f, 0.01f, bonnetDepth - 0.1f), gap);

        float grilleZ = cabFront + bonnetDepth + 0.01f;
        float grilleY = frameTop + bonnetHeight * 0.48f;
        AddPart(root, "GrilleBack", new Vector3(0f, grilleY, grilleZ),
                new Vector3(1.3f, 0.62f, 0.03f), gap);
        for (int i = 0; i < 7; i++)
            AddPart(root, "GrilleBar", new Vector3(-0.54f + i * 0.18f, grilleY, grilleZ + 0.02f),
                    new Vector3(0.05f, 0.6f, 0.03f), chrome);
        foreach (float side in new[] { -1f, 1f })
        {
            GameObject lampPart = AddPart(root, "Headlamp", new Vector3(side * 0.78f, grilleY + 0.04f, grilleZ + 0.02f),
                                          new Vector3(0.24f, 0.04f, 0.24f), lamp, PrimitiveType.Cylinder);
            lampPart.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        AddPart(root, "Bumper", new Vector3(0f, frameBottom + 0.08f, front - 0.1f),
                new Vector3(2.42f, 0.26f, 0.2f), gap);
        foreach (float side in new[] { -1f, 1f })
            AddPart(root, "TowHook", new Vector3(side * 0.6f, frameBottom - 0.02f, front + 0.02f),
                    new Vector3(0.1f, 0.12f, 0.12f), chrome);

        // Mirrors on stalks, and the exhaust stack behind the cab.
        foreach (float side in new[] { -1f, 1f })
        {
            AddPart(root, "MirrorArm", new Vector3(side * 1.28f, glassY + 0.1f, cabFront - 0.1f),
                    new Vector3(0.28f, 0.04f, 0.04f), gap);
            AddPart(root, "Mirror", new Vector3(side * 1.43f, glassY + 0.05f, cabFront - 0.1f),
                    new Vector3(0.05f, 0.3f, 0.2f), gap);
        }
        AddPart(root, "Exhaust", new Vector3(-1.02f, cabFloor + cabHeight * 0.55f, cabBack - 0.12f),
                new Vector3(0.13f, cabHeight * 0.6f, 0.13f), chrome, PrimitiveType.Cylinder);
        AddPart(root, "FuelTank", new Vector3(1.0f, frameBottom + 0.02f, 0.25f),
                new Vector3(0.44f, 0.38f, 0.44f), chrome, PrimitiveType.Cylinder)
            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Cargo bed: floor on the frame, drop sides, tailgate, hoops and tarp.
        const float bedFront = 0.88f;
        const float bedBack = -3.55f;
        float bedLength = bedFront - bedBack;
        float bedZ = (bedFront + bedBack) * 0.5f;
        const float bedFloorHeight = 0.12f;
        float bedTop = bodyFloor + bedFloorHeight;          // 1.34
        foreach (float x in new[] { -0.55f, 0.55f })
            AddPart(root, "BedRunner", new Vector3(x, (frameTop + bodyFloor) * 0.5f, bedZ),
                    new Vector3(0.14f, bodyFloor - frameTop, bedLength - 0.2f), gap);
        const float sideHeight = 0.5f;
        AddPart(root, "BedFloor", new Vector3(0f, bodyFloor + bedFloorHeight * 0.5f, bedZ),
                new Vector3(2.44f, bedFloorHeight, bedLength), dark);
        foreach (float side in new[] { -1f, 1f })
        {
            AddPart(root, "DropSide", new Vector3(side * 1.19f, bedTop + sideHeight * 0.5f, bedZ),
                    new Vector3(0.06f, sideHeight, bedLength), paint);
            for (int i = 1; i < 4; i++)
                AddPart(root, "SideSeam", new Vector3(side * 1.222f, bedTop + sideHeight * 0.5f, bedBack + i * bedLength / 4f),
                        new Vector3(0.01f, sideHeight, 0.03f), gap);
            AddPart(root, "TailLight", new Vector3(side * 1.0f, frameBottom + 0.1f, bedBack - 0.02f),
                    new Vector3(0.18f, 0.1f, 0.04f), tail);
        }
        AddPart(root, "Tailgate", new Vector3(0f, bedTop + sideHeight * 0.5f, bedBack - 0.03f),
                new Vector3(2.44f, sideHeight, 0.06f), paint);
        AddPart(root, "Headboard", new Vector3(0f, bedTop + 0.6f, bedFront - 0.03f),
                new Vector3(2.44f, 1.2f, 0.06f), dark);

        // Canvas tarp over hoops: the hoop ribs stand just proud of the canvas,
        // so the roof reads as fabric on a frame rather than one more box.
        const float tarpHeight = 1.05f;
        float tarpBottom = bedTop + sideHeight;             // 1.84
        AddPart(root, "Tarp", new Vector3(0f, tarpBottom + tarpHeight * 0.5f, bedZ - 0.05f),
                new Vector3(2.38f, tarpHeight, bedLength - 0.2f), canvas);
        for (int i = 0; i < 4; i++)
        {
            float z = bedFront - 0.4f - i * (bedLength - 0.8f) / 3f;
            AddPart(root, "Hoop", new Vector3(0f, tarpBottom + tarpHeight + 0.015f, z),
                    new Vector3(2.4f, 0.04f, 0.08f), dark);
            foreach (float side in new[] { -1f, 1f })
                AddPart(root, "HoopLeg", new Vector3(side * 1.195f, tarpBottom + tarpHeight * 0.5f, z),
                        new Vector3(0.03f, tarpHeight, 0.08f), dark);
        }
        AddPart(root, "TarpRearFlap", new Vector3(0f, tarpBottom + tarpHeight * 0.5f, bedBack + 0.14f),
                new Vector3(2.3f, tarpHeight * 0.95f, 0.04f), dark);

        return root.GetComponent<Target>();
    }

    /// <summary>
    /// A wheel arch from three plates: a flat top over the tyre and two
    /// angled skirts, so the wheel reads as sitting in a well, not under a lid.
    /// </summary>
    static void AddArch(GameObject root, Vector3 wheelCentre, float wheelDiameter, Material material)
    {
        float radius = wheelDiameter * 0.5f + 0.1f;
        float side = Mathf.Sign(wheelCentre.x);
        Vector3 top = wheelCentre + new Vector3(side * 0.02f, radius, 0f);
        AddPart(root, "Arch", top, new Vector3(0.5f, 0.05f, wheelDiameter * 0.55f), material);
        foreach (float dir in new[] { -1f, 1f })
        {
            GameObject skirt = AddPart(root, "Arch",
                wheelCentre + new Vector3(side * 0.02f, radius * 0.72f, dir * radius * 0.72f),
                new Vector3(0.5f, 0.05f, wheelDiameter * 0.42f), material);
            skirt.transform.localRotation = Quaternion.Euler(dir * 45f, 0f, 0f);
        }
    }

    // ---------- supply depot ----------

    /// <summary>
    /// A field supply point under anti-drone netting. It is always generated
    /// from the same four-post frame, so an older three-legged SupplyTent model
    /// can no longer appear in some scenes.
    /// </summary>
    /// <summary>
    /// Width a field supply tent comes out at, in metres. A real one is big
    /// enough to drive a truck into, and at 5.4 m it read as a garden gazebo
    /// next to the armour parked beside it.
    /// </summary>
    const float TentFootprint = 8.5f;

    public static Target SupplyDepot(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        const float postHeight = 3.6f;

        GameObject root = CreateRoot(parent, "SupplyDepot", position, yaw,
                                     Target.Kind.SupplyDepot,
                                     new Vector3(TentFootprint, postHeight + 0.2f, TentFootprint),
                                     new Vector3(0f, (postHeight + 0.2f) * 0.5f, 0f));

        BuildSupplyDepotPrimitives(root, palette, postHeight);
        return root.GetComponent<Target>();
    }

    static void BuildSupplyDepotPrimitives(GameObject root, Palette palette, float postHeight)
    {
        // Crates on pallets in two rows, the back row stacked two high on the
        // outer columns so the stack has a profile. Same crate models as the
        // loose stacks outside; each sits on the measured top of the one below.
        const float crate = 1.15f;
        const float pallet = 1.3f;
        for (int column = 0; column < 3; column++)
        {
            float x = -2.4f + column * 2.4f;
            foreach (float row in new[] { 1.6f, -1.6f })
                for (int pair = 0; pair < 2; pair++)
                {
                    var foot = new Vector3(x, 0f, row + (pair == 0 ? -0.66f : 0.66f));
                    string model = (column + pair) % 2 == 0 ? "CrateA" : "CrateB";
                    GameObject base_ = PropModels.Place("Pallet", "Pallet", root.transform, foot,
                                                        pair * 90f, pallet, palette.crate, false);
                    foot.y = PropModels.TopOf(base_);
                    GameObject lower = PropModels.Place(model, "Crate", root.transform, foot,
                                                        column * 90f, crate, palette.crate, false);
                    if (row > 0f || column == 1) continue;
                    foot.y = PropModels.TopOf(lower);
                    PropModels.Place(model == "CrateA" ? "CrateB" : "CrateA", "Crate", root.transform,
                                     foot, pair * 90f + 8f, crate * 0.95f, palette.crate, false);
                }
        }

        // Four fixed posts. There is no conditional model path and no skipped
        // corner, so every depot has exactly the same complete support frame.
        float half = TentFootprint * 0.5f - 0.4f;
        foreach (float x in new[] { -half, half })
        {
            foreach (float z in new[] { -half, half })
            {
                AddPart(root, "Post", new Vector3(x, postHeight * 0.5f, z),
                        new Vector3(0.22f, postHeight, 0.22f), palette.metal);
                AddPart(root, "PostFoot", new Vector3(x, 0.08f, z),
                        new Vector3(0.65f, 0.16f, 0.65f), palette.concrete);
            }
        }

        // Perimeter beams make the four supports read as one load-bearing frame.
        float span = half * 2f;
        foreach (float z in new[] { -half, half })
            AddPart(root, "TopBeam", new Vector3(0f, postHeight, z),
                    new Vector3(span, 0.14f, 0.14f), palette.metal);
        foreach (float x in new[] { -half, half })
            AddPart(root, "TopBeam", new Vector3(x, postHeight, 0f),
                    new Vector3(0.14f, 0.14f, span), palette.metal);

        var net = new GameObject("AntiDroneNet");
        net.transform.SetParent(root.transform, false);
        net.transform.localPosition = new Vector3(0f, postHeight, 0f);
        var filter = net.AddComponent<MeshFilter>();
        filter.sharedMesh = PrimitiveMesh.Drape(span + 0.35f, span + 0.35f, 0.72f, 8, 7319);
        var renderer = net.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = DroneMaterials.Load("Mat_CamoNet");
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
    }

    // ---------- antenna ----------

    /// <summary>
    /// A communications tower on the skyline: a 14 m galvanised triangular
    /// lattice mast on a concrete footing, a white parabolic dish and panel
    /// antennas near the top, a whip on the crown, three guy wires and an
    /// equipment cabinet at the foot.
    ///
    /// Scenery, not a target. It replaced a rust-textured square pole with a
    /// flat disc on it that read as a wooden sign or a street lamp. Being
    /// tall, pale grey and see-through, it cannot be mistaken for the jammer
    /// station either (short, dark, cabinet, blinking red beacon).
    /// </summary>
    public static GameObject Antenna(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        const float height = 14f;
        const float footing = 0.4f;
        const float baseRadius = 0.85f;
        const float topRadius = 0.32f;

        Material steel = DroneMaterials.Load("Mat_Galvanized");
        Material white = DroneMaterials.Load("Mat_WhitePaint");
        Material dark = DroneMaterials.Load("Mat_Gap");
        if (steel == null) steel = palette.metal;
        if (white == null) white = palette.concrete;
        if (dark == null) dark = palette.vehicleDark;

        var root = new GameObject("Antenna");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Solid for the drone: flying into the mast still costs the drone.
        var collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(2.2f, height + footing, 2.2f);
        collider.center = new Vector3(0f, (height + footing) * 0.5f, 0f);

        AddPart(root, "Footing", new Vector3(0f, footing * 0.5f, 0f),
                new Vector3(2.4f, footing, 2.4f), palette.concrete);

        // Three legs leaning in toward the crown, with rings and zig-zag
        // bracing on each face.
        var legBase = new Vector3[3];
        var legTop = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            Quaternion around = Quaternion.Euler(0f, i * 120f, 0f);
            legBase[i] = around * new Vector3(0f, footing, baseRadius);
            legTop[i] = around * new Vector3(0f, footing + height, topRadius);
            Strut(root, "Leg", legBase[i], legTop[i], 0.08f, steel);
        }
        const int bays = 8;
        for (int bay = 0; bay <= bays; bay++)
        {
            float t = (float)bay / bays;
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = Vector3.Lerp(legBase[i], legTop[i], t);
                Vector3 b = Vector3.Lerp(legBase[(i + 1) % 3], legTop[(i + 1) % 3], t);
                Strut(root, "Ring", a, b, 0.035f, steel);
                if (bay == bays) continue;
                float next = (float)(bay + 1) / bays;
                Vector3 c = Vector3.Lerp(legBase[(i + 1) % 3], legTop[(i + 1) % 3], next);
                Strut(root, "Brace", a, c, 0.025f, steel);
            }
        }

        // Dish and panel antennas on short arms near the top.
        float mount = footing + height - 2.4f;
        GameObject dish = new GameObject("Dish");
        dish.transform.SetParent(root.transform, false);
        dish.transform.localPosition = new Vector3(0f, mount, topRadius + 0.75f);
        dish.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
        dish.AddComponent<MeshFilter>().sharedMesh = DishMesh(0.75f, 0.28f, 6, 20);
        dish.AddComponent<MeshRenderer>().sharedMaterial = white;
        Strut(root, "DishArm", new Vector3(0f, mount, topRadius * 0.9f), dish.transform.localPosition, 0.05f, steel);
        Strut(root, "DishFeed", dish.transform.localPosition + new Vector3(0f, 0f, 0.28f),
              dish.transform.localPosition + new Vector3(0f, 0f, 0.62f), 0.025f, dark);

        for (int i = 0; i < 3; i++)
        {
            Quaternion around = Quaternion.Euler(0f, 60f + i * 120f, 0f);
            Vector3 arm = around * new Vector3(0f, 0f, topRadius + 0.45f);
            Vector3 at = new Vector3(0f, footing + height - 0.9f, 0f);
            Strut(root, "PanelArm", at + around * new Vector3(0f, 0f, topRadius * 0.6f), at + arm, 0.04f, steel);
            GameObject panel = AddPart(root, "Panel", at + arm + around * new Vector3(0f, 0f, 0.05f),
                                       new Vector3(0.3f, 1.3f, 0.1f), white);
            panel.transform.localRotation = around;
        }

        Strut(root, "Whip", new Vector3(0f, footing + height, 0f),
              new Vector3(0f, footing + height + 2.2f, 0f), 0.03f, steel);

        // Guy wires from two-thirds height to anchors on the ground.
        for (int i = 0; i < 3; i++)
        {
            Quaternion around = Quaternion.Euler(0f, i * 120f + 60f, 0f);
            Vector3 anchor = around * new Vector3(0f, 0.05f, 4.1f);
            Vector3 top = Vector3.Lerp(legBase[i], legTop[i], 0.65f);
            Strut(root, "GuyWire", top, anchor, 0.012f, dark);
            AddPart(root, "GuyAnchor", anchor + Vector3.up * 0.1f, new Vector3(0.3f, 0.2f, 0.3f), palette.concrete);
        }

        // Equipment cabinet and cable tray at the foot.
        AddPart(root, "Cabinet", new Vector3(1.55f, 0.75f, -0.2f), new Vector3(0.7f, 1.5f, 0.9f), white);
        AddPart(root, "CabinetDoorSeam", new Vector3(1.905f, 0.75f, -0.2f), new Vector3(0.01f, 1.3f, 0.02f), dark);
        Strut(root, "CableTray", new Vector3(1.2f, 1.1f, -0.2f), new Vector3(0.25f, 2.2f, 0f), 0.06f, dark);

        return root;
    }

    /// <summary>A thin round bar between two local points.</summary>
    static GameObject Strut(GameObject root, string name, Vector3 from, Vector3 to, float thickness, Material material)
    {
        Vector3 span = to - from;
        GameObject bar = AddPart(root, name, (from + to) * 0.5f,
                                 new Vector3(thickness, span.magnitude * 0.5f, thickness), material,
                                 PrimitiveType.Cylinder);
        bar.transform.localRotation = Quaternion.FromToRotation(Vector3.up, span.normalized);
        return bar;
    }

    /// <summary>
    /// A parabolic bowl opening along +Y, both sides with their own vertices
    /// and analytic normals (shared vertices across a double-sided sheet
    /// average to zero normals and render black — see CLAUDE.md).
    /// </summary>
    static Mesh DishMesh(float radius, float depth, int rings, int segments)
    {
        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        float a = depth / (radius * radius);

        for (int side = 0; side < 2; side++)
        {
            int start = vertices.Count;
            for (int k = 0; k <= rings; k++)
            {
                float r = radius * k / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    vertices.Add(new Vector3(cos * r, a * r * r, sin * r));
                    Vector3 up = new Vector3(-2f * a * r * cos, 1f, -2f * a * r * sin).normalized;
                    normals.Add(side == 0 ? up : -up);
                }
            }
            int stride = segments + 1;
            for (int k = 0; k < rings; k++)
                for (int s = 0; s < segments; s++)
                {
                    int i0 = start + k * stride + s;
                    int i1 = start + (k + 1) * stride + s;
                    int i2 = start + (k + 1) * stride + s + 1;
                    int i3 = start + k * stride + s + 1;
                    if (side == 0) triangles.AddRange(new[] { i0, i2, i1, i0, i3, i2 });
                    else triangles.AddRange(new[] { i0, i1, i2, i0, i2, i3 });
                }
        }

        var mesh = new Mesh { name = "Dish" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- scenery, not targets ----------

    public static GameObject Warehouse(Transform parent, Vector3 position, Vector3 size,
                                       float yaw, Palette palette)
    {
        var building = new GameObject("Warehouse");
        building.transform.SetParent(parent, false);
        building.transform.position = position;
        building.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        var walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
        walls.name = "Walls";
        walls.transform.SetParent(building.transform, false);
        walls.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        walls.transform.localScale = size;
        walls.GetComponent<Renderer>().sharedMaterial = palette.concrete;

        // Roof sits flush on the walls.
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(building.transform, false);
        roof.transform.localPosition = new Vector3(0f, size.y + 0.15f, 0f);
        roof.transform.localScale = new Vector3(size.x + 0.6f, 0.3f, size.z + 0.6f);
        roof.GetComponent<Renderer>().sharedMaterial = palette.roof;
        Object.DestroyImmediate(roof.GetComponent<Collider>());

        return building;
    }

    public static GameObject Tree(Transform parent, Vector3 position, float scale,
                                  Material trunk, Material foliage)
    {
        var tree = new GameObject("Tree");
        tree.transform.SetParent(parent, false);
        tree.transform.position = position;
        tree.transform.localScale = Vector3.one * scale;
        tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject trunkPart = AddPart(tree, "Trunk", new Vector3(0f, 1.6f, 0f),
                                       new Vector3(0.32f, 3.2f, 0.32f), trunk, PrimitiveType.Cylinder);

        // Layered evergreen silhouettes, shared by every tree. No new RNG
        // calls here: map seeds retain their original placement sequence.
        var crown = AddTreeCrown(tree.transform, "Crown", TreeCrownMesh(false), foliage);
        var distant = AddTreeCrown(tree.transform, "DistantCrown", TreeCrownMesh(true), foliage);
        distant.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var lod = tree.AddComponent<LODGroup>();
        lod.SetLODs(new[] {
            new LOD(0.045f, new Renderer[] { trunkPart.GetComponent<Renderer>(), crown }),
            new LOD(0.004f, new Renderer[] { distant })
        });
        lod.RecalculateBounds();

        // The trunk is the only part that collides, so a drone clips through
        // branches instead of detonating on them. AddPart strips every collider,
        // so the trunk's has to be put back deliberately.
        var trunkCollider = trunkPart.AddComponent<CapsuleCollider>();
        trunkCollider.height = 2f;
        trunkCollider.radius = 0.5f;

        NoShadows(trunkPart);
        return tree;
    }

    static Mesh nearCrown, farCrown;

    static Mesh TreeCrownMesh(bool distant)
    {
        Mesh cached = distant ? farCrown : nearCrown;
        if (cached != null) return cached;
        const string folder = "Assets/Generated/Shared";
        System.IO.Directory.CreateDirectory(folder);
        string path = folder + (distant ? "/PineFar.asset" : "/PineNear.asset");
        cached = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached == null)
        {
            Mesh cone = PrimitiveMesh.Frustum(1f, 0f, 1f, distant ? 5 : 9);
            var parts = new CombineInstance[distant ? 1 : 3];
            for (int i = 0; i < parts.Length; i++)
            {
                float radius = distant ? 1.6f : 1.65f - i * 0.40f;
                float height = distant ? 4.7f : 2.8f - i * 0.35f;
                float centre = distant ? 3.65f : 2.7f + i * 1.0f;
                parts[i] = new CombineInstance { mesh = cone,
                    transform = Matrix4x4.TRS(new Vector3(0f, centre, 0f),
                        Quaternion.Euler(0f, i * 23f, 0f), new Vector3(radius, height, radius)) };
            }
            cached = new Mesh { name = distant ? "PineFar" : "PineNear" };
            cached.CombineMeshes(parts, true, true);
            Object.DestroyImmediate(cone);
            UnityEditor.AssetDatabase.CreateAsset(cached, path);
        }
        if (distant) farCrown = cached; else nearCrown = cached;
        return cached;
    }

    static MeshRenderer AddTreeCrown(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    // ---------- helpers ----------

    static GameObject CreateRoot(Transform parent, string name, Vector3 position, float yaw,
                                 Target.Kind kind, Vector3 colliderSize, Vector3 colliderCentre)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        var collider = root.AddComponent<BoxCollider>();
        collider.size = colliderSize;
        collider.center = colliderCentre;

        var target = root.AddComponent<Target>();
        target.SetKind(kind);

        AddHighlight(root.transform, Mathf.Max(colliderSize.x, colliderSize.z), target);

        return root;
    }

    /// <summary>
    /// A soft glowing ring under the target, so something tucked in shade or
    /// behind netting still catches the eye from altitude instead of blending
    /// into the ground. Gently breathing rather than static, which is what
    /// actually pulls the eye without reading as a UI marker painted onto the
    /// world. Stops once the target is destroyed — a wreck does not need
    /// finding, it is already found.
    /// </summary>
    static void AddHighlight(Transform parent, float footprint, Target target)
    {
        Material highlight = Resources.Load<Material>("Materials/Mat_Highlight");
        if (highlight == null) return;

        var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = "Highlight";
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = Vector3.one * (footprint * 1.15f);

        Object.DestroyImmediate(ring.GetComponent<Collider>());
        var renderer = ring.GetComponent<Renderer>();
        renderer.sharedMaterial = highlight;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var breathe = ring.AddComponent<TargetHighlight>();
        breathe.target = target;
    }

    /// <summary>
    /// Resizes a root's BoxCollider to actually match the model just placed
    /// under it, instead of the hand-guessed numbers <see cref="CreateRoot"/>
    /// was given for the primitive fallback.
    ///
    /// A downloaded model's real-world scale is unknown until someone looks at
    /// it, so a collider sized from a guess can end up floating beside the
    /// visible mesh instead of around it — the drone flies straight through the
    /// tank you can see because the hitbox is somewhere else entirely. Measuring
    /// the model's own renderer bounds after it is placed is what makes the
    /// hitbox correct regardless of what scale the source file turns out to be.
    ///
    /// Renderer.bounds is an axis-aligned box in world space, which only equals
    /// the collider's local-space box if the root has no rotation at the moment
    /// it is measured — so the root is squared up to identity for the
    /// measurement and restored immediately after.
    /// </summary>
    static void FitColliderToModel(GameObject root, GameObject modelInstance)
    {
        var collider = root.GetComponent<BoxCollider>();
        if (collider == null) return;

        Bounds bounds;
        if (!MeasureLocalBounds(root, modelInstance, out bounds)) return;

        collider.center = bounds.center;

        // Nothing thinner than this collides reliably. A tarp or a tent panel
        // measures a few centimetres through, and a drone doing thirty metres a
        // second covers that inside one physics step — continuous detection
        // saves the frontal hit but not a clip through a corner. Padding the
        // box out to something a moving object cannot miss is what makes the
        // tent destructible at all.
        const float minThickness = 1.2f;
        collider.size = new Vector3(
            Mathf.Max(bounds.size.x, minThickness),
            Mathf.Max(bounds.size.y, minThickness),
            Mathf.Max(bounds.size.z, minThickness));
    }

    /// <summary>
    /// Rescales a model so its longest horizontal dimension comes out at
    /// <paramref name="desiredSize"/> metres, whatever units the source file
    /// happened to be authored in.
    ///
    /// A model downloaded from a public site can arrive in metres, centimetres
    /// or inches, and there is no way to tell which without opening it. Placing
    /// one at scale 1 and hoping is how a tent ends up the size of a hangar and
    /// pokes through the fence next to it. Measuring what actually arrived and
    /// scaling to a known footprint makes placement predictable — every position
    /// in the scene builder is then a real distance rather than a guess.
    /// </summary>
    static void NormalizeModelSize(GameObject root, GameObject modelInstance, float desiredSize)
    {
        Bounds bounds;
        if (!MeasureLocalBounds(root, modelInstance, out bounds)) return;

        float largest = Mathf.Max(bounds.size.x, bounds.size.z);
        if (largest <= 0.0001f) return;

        float correction = desiredSize / largest;

        // Only correct a genuine unit mismatch. A model that already arrives at
        // roughly the right size should keep its own proportions rather than be
        // squeezed to an exact number.
        if (correction > 0.75f && correction < 1.33f) return;

        modelInstance.transform.localScale *= correction;
    }

    /// <summary>
    /// Shifts the model so its footprint is centred on the root's own origin
    /// horizontally, with its lowest point sitting exactly at the root — not
    /// wherever the source file's own pivot happened to leave it.
    ///
    /// A downloaded mesh is rarely authored with its geometry centred on
    /// (0,0,0); some exporters put the origin at a corner, a tie-down point, a
    /// tent peg. Everything else about a target — the highlight marker under
    /// it, the radius other props are kept clear of during scattering — is
    /// measured from the root's position, which is exactly where the object
    /// was placed on the map. Leaving the visible mesh sitting off at whatever
    /// offset the file happened to use is what let a tent's highlight glow sit
    /// in open ground next to it while the tent itself leaned into a berm or a
    /// tank's footprint a few metres away — the two were never actually at the
    /// same point to begin with.
    /// </summary>
    static void RecentreModelOnGround(GameObject root, GameObject modelInstance)
    {
        Bounds bounds;
        if (!MeasureLocalBounds(root, modelInstance, out bounds)) return;

        // Horizontal centre, but the vertical offset takes the model down to
        // its own lowest point rather than its centre — recentring vertically
        // too would sink half of it through the ground.
        Vector3 offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        modelInstance.transform.localPosition -= offset;
    }

    /// <summary>
    /// The union of a model's renderer bounds, expressed in the root's local
    /// space.
    ///
    /// Renderer.bounds is an axis-aligned box in world space, which only equals
    /// a local-space box when the root has no rotation at the moment it is
    /// measured — so the root is squared up to identity for the measurement and
    /// restored immediately after.
    /// </summary>
    static bool MeasureLocalBounds(GameObject root, GameObject modelInstance, out Bounds bounds)
    {
        bounds = new Bounds();

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        Vector3 originalPosition = root.transform.position;
        Quaternion originalRotation = root.transform.rotation;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        root.transform.SetPositionAndRotation(originalPosition, originalRotation);
        return true;
    }

    static GameObject AddPart(GameObject parent, string name, Vector3 localPosition, Vector3 size,
                              Material material, PrimitiveType type = PrimitiveType.Cube)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;

        // The root carries the collider for the whole object.
        StripCollider(part);

        if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    /// <summary>
    /// A wheel lying on its side.
    ///
    /// Unity's cylinder is 2 units tall along Y and 1 unit across, so a wheel of
    /// diameter D and width W is scale (D, W/2, D) — then rolled 90° about Z to
    /// lay it flat. Scaling it any other way is what produces spikes instead of
    /// wheels.
    /// </summary>
    static GameObject AddWheel(GameObject parent, string name, Vector3 localPosition,
                               float diameter, float width, Material material)
    {
        GameObject wheel = AddPart(parent, name, localPosition,
                                   new Vector3(diameter, width * 0.5f, diameter),
                                   material, PrimitiveType.Cylinder);

        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        return wheel;
    }

    static void StripCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
    }

    static void NoShadows(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
