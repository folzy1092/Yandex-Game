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
    const float ModelYawOffset = 0f;

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

    /// <summary>
    /// Six-wheeled cargo truck: chassis, cab, bonnet and a tarped bed.
    /// Tall and square, so it never gets confused with the armour from above.
    /// </summary>
    public static Target Truck(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        GameObject root = CreateRoot(parent, "Truck", position, yaw,
                                     Target.Kind.LightVehicle,
                                     new Vector3(2.7f, 3.4f, 7.2f), new Vector3(0f, 1.7f, 0f));

        const float wheelDiameter = 1.15f;
        const float axleHeight = wheelDiameter * 0.5f;      // 0.575
        const float chassisHeight = 0.32f;
        const float chassisTop = axleHeight + chassisHeight * 0.5f;

        AddPart(root, "Chassis", new Vector3(0f, axleHeight + 0.1f, 0f),
                new Vector3(2.1f, chassisHeight, 6.8f), palette.vehicleDark);

        // Bonnet, then the cab behind it, both resting on the chassis.
        AddPart(root, "Bonnet", new Vector3(0f, chassisTop + 0.45f, 2.75f),
                new Vector3(2.2f, 0.9f, 1.5f), palette.vehicle);

        AddPart(root, "Cab", new Vector3(0f, chassisTop + 0.85f, 1.35f),
                new Vector3(2.3f, 1.7f, 1.5f), palette.vehicle);

        AddPart(root, "Windscreen", new Vector3(0f, chassisTop + 1.35f, 2.12f),
                new Vector3(2.0f, 0.75f, 0.08f), palette.vehicleDark);

        // Cargo bed with a tarp over it, sitting on the chassis behind the cab.
        AddPart(root, "BedFloor", new Vector3(0f, chassisTop + 0.12f, -1.5f),
                new Vector3(2.4f, 0.2f, 4.0f), palette.vehicleDark);

        AddPart(root, "Tarp", new Vector3(0f, chassisTop + 1.05f, -1.5f),
                new Vector3(2.5f, 1.7f, 4.0f), palette.vehicle);

        AddPart(root, "TarpRear", new Vector3(0f, chassisTop + 1.05f, -3.52f),
                new Vector3(2.4f, 1.6f, 0.1f), palette.vehicleDark);

        // Three axles a side. Front axle under the bonnet, two under the bed.
        float[] axleZ = { 2.5f, -1.1f, -2.6f };
        foreach (float z in axleZ)
        {
            AddWheel(root, "Wheel", new Vector3(-1.12f, axleHeight, z),
                     wheelDiameter, 0.42f, palette.vehicleDark);
            AddWheel(root, "Wheel", new Vector3(1.12f, axleHeight, z),
                     wheelDiameter, 0.42f, palette.vehicleDark);
        }

        return root.GetComponent<Target>();
    }

    // ---------- supply depot ----------

    /// <summary>
    /// A stack of crates under a tarp on four posts. The tarp is derived from the
    /// stack height so it rests on the posts instead of floating over them.
    /// </summary>
    /// <summary>
    /// Uses the downloaded supply tent model if imported
    /// (Assets/Resources/Models/SupplyTent.glb), otherwise a crate stack under a
    /// tarp built from primitives. Same collider footprint either way, so the
    /// hitbox does not depend on which one loaded.
    /// </summary>
    const float TentModelScale = 1f;
    const float TentModelYawOffset = 0f;

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

        GameObject tentModel = ModelLibrary.Instantiate("SupplyTent", root.transform,
                                                        TentModelScale, TentModelYawOffset);
        if (tentModel != null)
        {
            NormalizeModelSize(root, tentModel, TentFootprint);
            FitColliderToModel(root, tentModel);
            return root.GetComponent<Target>();
        }

        BuildSupplyDepotPrimitives(root, palette, postHeight);
        return root.GetComponent<Target>();
    }

    static void BuildSupplyDepotPrimitives(GameObject root, Palette palette, float postHeight)
    {
        const float crateHeight = 1.2f;

        // Two rows of crates, the back row stacked two high.
        for (int column = 0; column < 3; column++)
        {
            float x = -2.4f + column * 2.4f;

            AddPart(root, "Crate", new Vector3(x, crateHeight * 0.5f, 1.6f),
                    new Vector3(2.1f, crateHeight, 2.8f), palette.crate);

            AddPart(root, "Crate", new Vector3(x, crateHeight * 0.5f, -1.6f),
                    new Vector3(2.1f, crateHeight, 2.8f), palette.crate);

            // Second layer on the back row only, so the stack has a profile.
            if (column == 1) continue;
            AddPart(root, "Crate", new Vector3(x, crateHeight * 1.5f, -1.6f),
                    new Vector3(2.0f, crateHeight, 2.6f), palette.crate);
        }

        // Four posts holding the tarp up, with the tarp resting on top of them.
        float half = TentFootprint * 0.5f - 0.4f;
        foreach (float x in new[] { -half, half })
        {
            foreach (float z in new[] { -half, half })
            {
                AddPart(root, "Post", new Vector3(x, postHeight * 0.5f, z),
                        new Vector3(0.22f, postHeight, 0.22f), palette.metal);
            }
        }

        AddPart(root, "Tarp", new Vector3(0f, postHeight + 0.06f, 0f),
                new Vector3(TentFootprint, 0.14f, TentFootprint), palette.vehicleDark);
    }

    // ---------- antenna ----------

    /// <summary>
    /// A guyed mast with a dish. Tall and thin, so it stands out from every
    /// other target on the map at any range.
    /// </summary>
    public static Target Antenna(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        const float mastHeight = 9f;

        GameObject root = CreateRoot(parent, "Antenna", position, yaw,
                                     Target.Kind.Antenna,
                                     new Vector3(2.6f, mastHeight, 2.6f),
                                     new Vector3(0f, mastHeight * 0.5f, 0f));

        AddPart(root, "Base", new Vector3(0f, 0.3f, 0f),
                new Vector3(2.2f, 0.6f, 2.2f), palette.concrete);

        AddPart(root, "Mast", new Vector3(0f, 0.6f + mastHeight * 0.5f, 0f),
                new Vector3(0.34f, mastHeight, 0.34f), palette.metal);

        // Lattice cross-bracing: a few angled bars break up the bare column.
        for (int i = 0; i < 4; i++)
        {
            float height = 1.6f + i * 2f;
            GameObject brace = AddPart(root, "Brace", new Vector3(0f, height, 0f),
                                       new Vector3(0.9f, 0.08f, 0.08f), palette.metal);
            brace.transform.localRotation = Quaternion.Euler(0f, i * 45f, 32f);
        }

        // Dish near the top, angled out — the part that identifies it from above.
        GameObject dish = AddPart(root, "Dish", new Vector3(0.85f, mastHeight - 1.4f, 0f),
                                  new Vector3(1.9f, 0.14f, 1.9f), palette.metal,
                                  PrimitiveType.Cylinder);
        dish.transform.localRotation = Quaternion.Euler(0f, 0f, 68f);

        AddPart(root, "Crown", new Vector3(0f, mastHeight + 0.5f, 0f),
                new Vector3(1.2f, 0.1f, 0.1f), palette.metal);

        return root.GetComponent<Target>();
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

        AddPart(tree, "CanopyLower", new Vector3(0f, 3.4f, 0f),
                new Vector3(2.6f, 2.2f, 2.6f), foliage);
        AddPart(tree, "CanopyUpper", new Vector3(0f, 4.9f, 0f),
                new Vector3(1.7f, 1.8f, 1.7f), foliage);

        // The trunk is the only part that collides, so a drone clips through
        // branches instead of detonating on them. AddPart strips every collider,
        // so the trunk's has to be put back deliberately.
        var trunkCollider = trunkPart.AddComponent<CapsuleCollider>();
        trunkCollider.height = 2f;
        trunkCollider.radius = 0.5f;

        NoShadows(trunkPart);
        return tree;
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
        target.kind = kind;

        return root;
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
