using UnityEngine;

/// <summary>
/// The vehicles and structures the drone is sent after, assembled from
/// primitives.
///
/// Each is built with one collider covering the whole object rather than one
/// per part: the blast damages a target once regardless, and a single box makes
/// hit detection predictable when a drone comes in fast.
///
/// Silhouettes are kept distinct — a tank is long and low with a turret, a truck
/// is tall and boxy, a depot is a stack of crates — because from a hundred
/// metres up the shape is all the pilot has to go on.
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

    public static Target ArmouredVehicle(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var root = CreateRoot(parent, "ArmouredVehicle", position, yaw,
                              Target.Kind.ArmouredVehicle,
                              new Vector3(3.4f, 2.3f, 6.6f), new Vector3(0f, 1.15f, 0f));

        AddPart(root, "Hull", new Vector3(0f, 0.9f, 0f), new Vector3(3.2f, 1.0f, 6.4f), palette.vehicle);
        AddPart(root, "Turret", new Vector3(0f, 1.7f, -0.4f), new Vector3(2.1f, 0.7f, 2.6f), palette.vehicle);
        AddPart(root, "Barrel", new Vector3(0f, 1.8f, 2.2f), new Vector3(0.22f, 0.22f, 3.4f), palette.vehicleDark);

        // Tracks down each side.
        AddPart(root, "TrackLeft", new Vector3(-1.5f, 0.45f, 0f), new Vector3(0.55f, 0.9f, 6.2f), palette.vehicleDark);
        AddPart(root, "TrackRight", new Vector3(1.5f, 0.45f, 0f), new Vector3(0.55f, 0.9f, 6.2f), palette.vehicleDark);

        return root.GetComponent<Target>();
    }

    public static Target Truck(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var root = CreateRoot(parent, "Truck", position, yaw,
                              Target.Kind.LightVehicle,
                              new Vector3(2.6f, 3.2f, 7f), new Vector3(0f, 1.6f, 0f));

        AddPart(root, "Chassis", new Vector3(0f, 0.7f, 0f), new Vector3(2.4f, 0.5f, 6.8f), palette.vehicleDark);
        AddPart(root, "Cab", new Vector3(0f, 1.6f, 2.2f), new Vector3(2.3f, 1.4f, 2.0f), palette.vehicle);
        AddPart(root, "Cargo", new Vector3(0f, 1.9f, -1.2f), new Vector3(2.5f, 2.0f, 4.2f), palette.vehicle);

        for (int i = 0; i < 6; i++)
        {
            float x = (i % 2 == 0) ? -1.2f : 1.2f;
            float z = -2.2f + (i / 2) * 2.2f;

            var wheel = AddPart(root, "Wheel" + i, new Vector3(x, 0.5f, z),
                                new Vector3(0.4f, 1f, 1f), palette.vehicleDark, PrimitiveType.Cylinder);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        return root.GetComponent<Target>();
    }

    public static Target SupplyDepot(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var root = CreateRoot(parent, "SupplyDepot", position, yaw,
                              Target.Kind.SupplyDepot,
                              new Vector3(4.4f, 2.6f, 4.4f), new Vector3(0f, 1.3f, 0f));

        // A stack of crates under a tarp frame.
        for (int i = 0; i < 6; i++)
        {
            float x = (i % 2 == 0) ? -0.95f : 0.95f;
            float z = -1.1f + (i / 2) * 1.1f;
            float height = (i % 3 == 0) ? 1.7f : 0.9f;

            AddPart(root, "Crate" + i, new Vector3(x, height * 0.5f, z),
                    new Vector3(1.7f, height, 1.0f), palette.crate);
        }

        AddPart(root, "Cover", new Vector3(0f, 2.3f, 0f), new Vector3(4.2f, 0.12f, 4.2f), palette.vehicleDark);

        return root.GetComponent<Target>();
    }

    public static Target Antenna(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var root = CreateRoot(parent, "Antenna", position, yaw,
                              Target.Kind.Antenna,
                              new Vector3(2.4f, 9f, 2.4f), new Vector3(0f, 4.5f, 0f));

        AddPart(root, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(2.2f, 0.6f, 2.2f), palette.concrete);
        AddPart(root, "Mast", new Vector3(0f, 4.5f, 0f), new Vector3(0.35f, 8.4f, 0.35f), palette.metal);

        // Dish near the top, the part that makes it read as an antenna from above.
        var dish = AddPart(root, "Dish", new Vector3(0.7f, 7.4f, 0f),
                           new Vector3(1.8f, 0.16f, 1.8f), palette.metal, PrimitiveType.Cylinder);
        dish.transform.localRotation = Quaternion.Euler(0f, 0f, 65f);

        AddPart(root, "Guy", new Vector3(0f, 8.7f, 0f), new Vector3(1.4f, 0.1f, 0.1f), palette.metal);

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

        AddPart(building, "Walls", new Vector3(0f, size.y * 0.5f, 0f), size, palette.concrete);
        AddPart(building, "Roof", new Vector3(0f, size.y + 0.15f, 0f),
                new Vector3(size.x + 0.6f, 0.3f, size.z + 0.6f), palette.roof);

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

        // Two stacked cones of foliage. Cheap, and from the air a conifer is
        // mostly its outline anyway.
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
