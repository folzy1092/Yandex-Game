using UnityEngine;

/// <summary>
/// The furniture of a swimming-pool complex, assembled from primitives.
///
/// Each prop is built at a known height class so it can be placed for a reason
/// rather than scattered: sun loungers and benches are low cover you shoot over,
/// vending machines and changing cabins are high cover that cuts a sightline
/// outright.
/// </summary>
public static class PoolProps
{
    public class Palette
    {
        public Material tile;      // pool basin tiling
        public Material wallTile;  // wall tiling for showers and interiors
        public Material wall;
        public Material concrete;
        public Material metal;
        public Material plastic;
        public Material fabric;
        public Material wood;
        public Material plant;
        public Material accent;
        public Material window;
    }

    // ---------- low cover ----------

    /// <summary>Sun lounger: a slatted bed with a raised backrest at one end.</summary>
    public static void SunLounger(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var group = MapBlocks.Group("SunLounger", parent);
        group.transform.position = position;
        group.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        MapBlocks.BoxAt(group.transform, "Bed", position + Vector3.up * 0.42f,
                        new Vector3(0.75f, 0.09f, 1.9f), palette.plastic, new Vector3(0f, yaw, 0f));

        // Backrest, tipped up the way a lounger's is.
        MapBlocks.BoxAt(group.transform, "Back",
                        position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.72f, -0.72f),
                        new Vector3(0.75f, 0.09f, 0.85f), palette.plastic,
                        new Vector3(-52f, yaw, 0f));

        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0) ? -0.3f : 0.3f;
            float z = (i < 2) ? 0.75f : -0.55f;

            MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Leg" + i,
                position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(x, 0.19f, z),
                new Vector3(0.07f, 0.38f, 0.07f), palette.metal, new Vector3(0f, yaw, 0f)));
        }
    }

    /// <summary>Bench: a seat plank on two solid legs.</summary>
    public static void Bench(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var group = MapBlocks.Group("Bench", parent);

        MapBlocks.BoxAt(group.transform, "Seat", position + Vector3.up * 0.48f,
                        new Vector3(2.2f, 0.12f, 0.55f), palette.wood, new Vector3(0f, yaw, 0f));

        MapBlocks.BoxAt(group.transform, "LegLeft",
                        position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-0.9f, 0.24f, 0f),
                        new Vector3(0.14f, 0.48f, 0.5f), palette.concrete, new Vector3(0f, yaw, 0f));

        MapBlocks.BoxAt(group.transform, "LegRight",
                        position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.9f, 0.24f, 0f),
                        new Vector3(0.14f, 0.48f, 0.5f), palette.concrete, new Vector3(0f, yaw, 0f));
    }

    /// <summary>Low concrete wall, the workhorse piece for shaping sightlines.</summary>
    public static void LowWall(Transform parent, Vector3 position, float length, float yaw, Palette palette)
    {
        MapBlocks.Box(parent, "LowWall", position,
                      new Vector3(length, MapBlocks.LowCover, 0.35f), palette.concrete, yaw);
    }

    // ---------- medium cover ----------

    /// <summary>Equipment crate.</summary>
    public static void Crate(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        MapBlocks.Box(parent, "Crate", position,
                      new Vector3(1.3f, MapBlocks.MediumCover, 1.1f), palette.metal, yaw);

        // Decorative only: this used to keep its collider, which quietly raised
        // the crate's effective blocking height above MediumCover.
        MapBlocks.NoCollision(MapBlocks.NoShadows(MapBlocks.BoxAt(parent, "CrateLid",
            position + Vector3.up * (MapBlocks.MediumCover + 0.04f),
            new Vector3(1.38f, 0.08f, 1.18f), palette.accent, new Vector3(0f, yaw, 0f))));
    }

    /// <summary>Pump housing with pipework running out of it.</summary>
    public static void PumpUnit(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var group = MapBlocks.Group("PumpUnit", parent);

        MapBlocks.Box(group.transform, "Housing", position,
                      new Vector3(1.6f, 1.5f, 1.2f), palette.metal, yaw);

        MapBlocks.NoShadows(MapBlocks.Cylinder(group.transform, "Motor",
            position + Vector3.up * 1.75f, new Vector3(0.5f, 0.35f, 0.5f), palette.accent,
            new Vector3(90f, yaw, 0f)));

        MapBlocks.NoShadows(MapBlocks.Cylinder(group.transform, "PipeOut",
            position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(1.0f, 1.1f, 0f),
            new Vector3(0.22f, 0.7f, 0.22f), palette.metal, new Vector3(0f, 0f, 90f)));
    }

    /// <summary>Planter with foliage. The leaves have no collider, so nobody snags on a bush.</summary>
    public static void Planter(Transform parent, Vector3 position, Palette palette)
    {
        var group = MapBlocks.Group("Planter", parent);

        MapBlocks.Box(group.transform, "Pot", position,
                      new Vector3(1.1f, 0.75f, 1.1f), palette.concrete);

        MapBlocks.NoCollision(MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Foliage",
            position + Vector3.up * 1.35f, new Vector3(1.25f, 1.2f, 1.25f), palette.plant)));

        MapBlocks.NoCollision(MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "FoliageTop",
            position + Vector3.up * 2.0f, new Vector3(0.8f, 0.7f, 0.8f), palette.plant,
            new Vector3(0f, 35f, 0f))));
    }

    // ---------- high cover ----------

    /// <summary>Changing cabin: a booth tall enough to break a sightline outright.</summary>
    public static void ChangingCabin(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var group = MapBlocks.Group("ChangingCabin", parent);

        MapBlocks.Box(group.transform, "Body", position,
                      new Vector3(1.3f, 2.4f, 1.3f), palette.accent, yaw);

        MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Roof",
            position + Vector3.up * 2.48f, new Vector3(1.45f, 0.12f, 1.45f), palette.metal,
            new Vector3(0f, yaw, 0f)));
    }

    /// <summary>Vending machine.</summary>
    public static void VendingMachine(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var group = MapBlocks.Group("VendingMachine", parent);

        MapBlocks.Box(group.transform, "Body", position,
                      new Vector3(1.1f, 2.0f, 0.75f), palette.metal, yaw);

        MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Window",
            position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-0.15f, 1.25f, 0.39f),
            new Vector3(0.7f, 1.1f, 0.04f), palette.accent, new Vector3(0f, yaw, 0f)));
    }

    /// <summary>A bank of lockers, the backbone of the changing rooms.</summary>
    public static void LockerBank(Transform parent, Vector3 position, float length, float yaw,
                                  Palette palette)
    {
        var group = MapBlocks.Group("LockerBank", parent);

        MapBlocks.Box(group.transform, "Body", position,
                      new Vector3(length, 2.1f, 0.55f), palette.accent, yaw);

        // Door seams, so a long locker bank does not read as one flat slab.
        int doors = Mathf.Max(1, Mathf.RoundToInt(length / 0.7f));
        for (int i = 0; i < doors; i++)
        {
            float offset = -length * 0.5f + length * (i + 0.5f) / doors;

            MapBlocks.NoCollision(MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Seam" + i,
                position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(offset, 1.05f, 0.3f),
                new Vector3(0.04f, 2.0f, 0.04f), palette.metal, new Vector3(0f, yaw, 0f))));
        }
    }

    /// <summary>Shower stall: a back wall with dividers and a head on a riser.</summary>
    public static void ShowerStall(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var group = MapBlocks.Group("ShowerStall", parent);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        MapBlocks.Box(group.transform, "Divider", position,
                      new Vector3(0.18f, 2.3f, 1.5f), palette.wallTile, yaw);

        MapBlocks.NoShadows(MapBlocks.Cylinder(group.transform, "Riser",
            position + rotation * new Vector3(0f, 1.9f, 0.6f),
            new Vector3(0.09f, 0.4f, 0.09f), palette.metal));

        MapBlocks.NoCollision(MapBlocks.NoShadows(MapBlocks.Cylinder(group.transform, "Head",
            position + rotation * new Vector3(0f, 2.25f, 0.75f),
            new Vector3(0.26f, 0.05f, 0.26f), palette.metal)));
    }

    /// <summary>Lifeguard tower: a raised seat on legs, climbable by nobody — pure cover and landmark.</summary>
    public static void LifeguardTower(Transform parent, Vector3 position, float yaw, Palette palette)
    {
        var group = MapBlocks.Group("LifeguardTower", parent);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0) ? -0.55f : 0.55f;
            float z = (i < 2) ? -0.55f : 0.55f;

            MapBlocks.BoxAt(group.transform, "Leg" + i,
                position + rotation * new Vector3(x, 1.1f, z),
                new Vector3(0.16f, 2.2f, 0.16f), palette.wood, new Vector3(0f, yaw, 0f));
        }

        MapBlocks.BoxAt(group.transform, "Platform", position + Vector3.up * 2.3f,
                        new Vector3(1.5f, 0.15f, 1.5f), palette.wood, new Vector3(0f, yaw, 0f));

        MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Backrest",
            position + rotation * new Vector3(0f, 2.9f, -0.7f),
            new Vector3(1.5f, 1.1f, 0.12f), palette.wood, new Vector3(0f, yaw, 0f)));

        MapBlocks.NoCollision(MapBlocks.NoShadows(MapBlocks.BoxAt(group.transform, "Canopy",
            position + Vector3.up * 3.6f, new Vector3(1.8f, 0.1f, 1.8f), palette.fabric,
            new Vector3(0f, yaw, 0f))));
    }

    // ---------- decoration ----------

    /// <summary>Parasol. Decorative only — the canopy would otherwise catch bullets over cover.</summary>
    public static void Parasol(Transform parent, Vector3 position, Palette palette)
    {
        var group = MapBlocks.Group("Parasol", parent);

        MapBlocks.NoShadows(MapBlocks.Cylinder(group.transform, "Pole",
            position + Vector3.up * 1.2f, new Vector3(0.08f, 1.2f, 0.08f), palette.metal));

        MapBlocks.NoCollision(MapBlocks.NoShadows(MapBlocks.Cylinder(group.transform, "Canopy",
            position + Vector3.up * 2.35f, new Vector3(2.6f, 0.07f, 2.6f), palette.fabric)));

        MapBlocks.NoCollision(MapBlocks.NoShadows(MapBlocks.Cylinder(group.transform, "CanopyTop",
            position + Vector3.up * 2.5f, new Vector3(1.5f, 0.07f, 1.5f), palette.accent)));
    }

    /// <summary>A run of overhead pipework, the shorthand for a service area.</summary>
    public static void PipeRun(Transform parent, Vector3 from, Vector3 to, float radius, Palette palette)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f) return;

        var pipe = MapBlocks.Cylinder(parent, "Pipe", (from + to) * 0.5f,
                                      new Vector3(radius * 2f, length * 0.5f, radius * 2f), palette.metal);

        // Unity cylinders run along Y, so aim that axis down the pipe.
        pipe.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        MapBlocks.NoShadows(pipe);
        MapBlocks.NoCollision(pipe);
    }

    /// <summary>A flat wet patch on the floor. Cosmetic, no collider.</summary>
    public static void Puddle(Transform parent, Vector3 position, float size, Palette palette)
    {
        var puddle = MapBlocks.Cylinder(parent, "Puddle", position + Vector3.up * 0.015f,
                                        new Vector3(size, 0.01f, size * 0.75f), palette.tile);
        MapBlocks.NoCollision(MapBlocks.NoShadows(puddle));
    }
}
