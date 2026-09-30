using UnityEngine;

/// <summary>
/// Mission props the challenge runner adds to the built Outpost scene at
/// runtime: the electronic-warfare station and the fuel cache. They exist
/// only for their own challenge, so they are built when it starts rather than
/// baked into every scene.
///
/// Every part is placed from the ground up — each height is the top of the
/// part beneath it — so nothing floats and nothing sinks, whatever the pivot
/// of the primitive.
/// </summary>
public static class FieldProps
{
    // ---------- jammer station ----------

    const float MastHeight = 5.6f;

    /// <summary>
    /// A short steel lattice mast with three panel antennas, an equipment
    /// cabinet with cables to the mast foot, and a blinking red beacon on top.
    /// Deliberately nothing like the timber pole with a dish used as scenery:
    /// from 20–80 m the panels, the cabinet and the red light identify it.
    /// </summary>
    public static Target JammerStation(Vector3 position, float yaw)
    {
        GameObject root = Root("JammerStation", position, yaw,
            new Vector3(3.4f, MastHeight + 0.6f, 3.4f), Target.Kind.SignalJammer);

        Material steel = Mat("Mat_SteelPaint");
        Material concrete = Mat("Mat_Concrete");
        Material dark = Mat("Mat_Gap");
        Material rubber = Mat("Mat_Rubber");
        Material hazard = Mat("Mat_Hazard");
        Material panel = Mat("Mat_Chrome");

        const float padTop = 0.18f;
        Part(root, "Pad", new Vector3(0f, padTop * 0.5f, 0f), new Vector3(3.2f, padTop, 3.2f), concrete);

        // Equipment cabinet with door seams, vents and a warning plate.
        Vector3 cabinet = new Vector3(-0.85f, padTop, 0.55f);
        const float cabinetHeight = 1.55f;
        Part(root, "Cabinet", cabinet + new Vector3(0f, cabinetHeight * 0.5f, 0f),
            new Vector3(1.1f, cabinetHeight, 0.75f), steel);
        Part(root, "CabinetRoof", cabinet + new Vector3(0f, cabinetHeight + 0.03f, 0f),
            new Vector3(1.22f, 0.06f, 0.86f), steel);
        Part(root, "DoorSeam", cabinet + new Vector3(0f, cabinetHeight * 0.5f, 0.378f),
            new Vector3(0.02f, cabinetHeight - 0.2f, 0.01f), dark);
        for (int i = 0; i < 4; i++)
            Part(root, "Vent", cabinet + new Vector3(0.28f, 0.35f + i * 0.09f, 0.378f),
                new Vector3(0.36f, 0.035f, 0.01f), dark);
        Part(root, "WarningPlate", cabinet + new Vector3(-0.27f, 1.05f, 0.379f),
            new Vector3(0.3f, 0.22f, 0.01f), hazard);
        Part(root, "Whip", cabinet + new Vector3(0.4f, cabinetHeight + 0.6f, -0.2f),
            new Vector3(0.025f, 0.6f, 0.025f), dark, PrimitiveType.Cylinder);

        // Generator beside it.
        Vector3 generator = new Vector3(0.9f, padTop, -0.95f);
        Part(root, "Generator", generator + new Vector3(0f, 0.36f, 0f), new Vector3(0.9f, 0.72f, 0.6f), dark);
        Part(root, "GeneratorPanel", generator + new Vector3(0f, 0.4f, 0.305f),
            new Vector3(0.5f, 0.3f, 0.01f), steel);

        // Four-leg lattice mast: legs, and cross-bracing every metre.
        const float legOffset = 0.2f;
        foreach (float x in new[] { -legOffset, legOffset })
            foreach (float z in new[] { -legOffset, legOffset })
                Part(root, "MastLeg", new Vector3(x + 0.6f, padTop + MastHeight * 0.5f, z - 0.4f),
                    new Vector3(0.06f, MastHeight, 0.06f), steel);
        for (float h = 0.6f; h < MastHeight; h += 0.9f)
        {
            Part(root, "Brace", new Vector3(0.6f, padTop + h, -0.4f - legOffset),
                new Vector3(legOffset * 2f, 0.04f, 0.04f), steel);
            Part(root, "Brace", new Vector3(0.6f, padTop + h, -0.4f + legOffset),
                new Vector3(legOffset * 2f, 0.04f, 0.04f), steel);
            Part(root, "Brace", new Vector3(0.6f - legOffset, padTop + h, -0.4f),
                new Vector3(0.04f, 0.04f, legOffset * 2f), steel);
            Part(root, "Brace", new Vector3(0.6f + legOffset, padTop + h, -0.4f),
                new Vector3(0.04f, 0.04f, legOffset * 2f), steel);
        }

        // Three panel antennas fanned around the top — the silhouette that says
        // "electronic warfare" rather than "street lamp".
        Vector3 mastTop = new Vector3(0.6f, padTop + MastHeight, -0.4f);
        for (int i = 0; i < 3; i++)
        {
            Quaternion around = Quaternion.Euler(0f, i * 120f, 0f);
            Vector3 arm = around * new Vector3(0f, 0f, 0.42f);
            GameObject bracket = Part(root, "PanelArm", mastTop + new Vector3(0f, -0.7f, 0f) + arm * 0.5f,
                new Vector3(0.05f, 0.05f, 0.42f), steel);
            bracket.transform.localRotation = around;
            GameObject antenna = Part(root, "PanelAntenna", mastTop + new Vector3(0f, -0.7f, 0f) + arm,
                new Vector3(0.34f, 1.15f, 0.09f), panel);
            antenna.transform.localRotation = around;
        }

        // Beacon on a short cap.
        Part(root, "MastCap", mastTop + new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.1f, 0.5f), steel);
        GameObject beacon = Part(root, "Beacon", mastTop + new Vector3(0f, 0.25f, 0f),
            new Vector3(0.28f, 0.28f, 0.28f), null, PrimitiveType.Sphere);
        var lightGO = new GameObject("BeaconLight");
        lightGO.transform.SetParent(beacon.transform, false);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.15f, 0.08f);
        light.range = 14f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        var blink = root.AddComponent<JammerBeacon>();
        blink.beacon = beacon.GetComponent<Renderer>();
        blink.beaconLight = light;

        // Cables from the cabinet across the pad to the mast foot.
        Cable(root, cabinet + new Vector3(0.45f, 0.05f, -0.2f), new Vector3(0.4f, padTop + 0.04f, -0.4f), rubber);
        Cable(root, generator + new Vector3(-0.35f, 0.05f, 0.2f), new Vector3(0.45f, padTop + 0.04f, -0.25f), rubber);
        Cable(root, cabinet + new Vector3(0.3f, 0.05f, -0.3f), generator + new Vector3(-0.4f, 0.05f, 0.1f), rubber);

        return root.GetComponent<Target>();
    }

    // ---------- fuel cache ----------

    /// <summary>Radius the cache's own detonation reaches, and its danger ring.</summary>
    public const float FuelBlastRadius = 9f;

    /// <summary>
    /// Drums and jerrycans on two pallets inside a low striped barrier, with a
    /// warning board. Small, clearly a store of fuel, and low enough that the
    /// trucks parked either side of it stay the tallest things in the group.
    /// </summary>
    public static Target FuelCache(Vector3 position, float yaw)
    {
        GameObject root = Root("FuelCache", position, yaw,
            new Vector3(3.6f, 1.5f, 2.8f), Target.Kind.FuelDepot);

        Material pallet = Mat("Mat_Pallet");
        Material dark = Mat("Mat_Gap");
        Material red = Mat("Mat_HazardRed");
        Material yellow = Mat("Mat_Hazard");
        Material canister = Mat("Mat_Canister");
        Material rim = Mat("Mat_Chrome");
        Material post = Mat("Mat_SteelPaint");

        const float palletHeight = 0.14f;
        foreach (float x in new[] { -0.7f, 0.7f })
        {
            Part(root, "Pallet", new Vector3(x, palletHeight * 0.5f, 0f), new Vector3(1.2f, palletHeight, 1.0f), pallet);
            Part(root, "PalletGap", new Vector3(x, palletHeight * 0.4f, 0.501f), new Vector3(0.9f, 0.05f, 0.01f), dark);
        }

        // Four drums on the left pallet, standing on its top face.
        const float drumHeight = 0.88f;
        const float drumDiameter = 0.56f;
        for (int i = 0; i < 4; i++)
        {
            float x = -0.7f + (i % 2 == 0 ? -0.29f : 0.29f);
            float z = i < 2 ? -0.24f : 0.24f;
            Vector3 foot = new Vector3(x, palletHeight, z);
            Part(root, "Drum", foot + new Vector3(0f, drumHeight * 0.5f, 0f),
                new Vector3(drumDiameter, drumHeight * 0.5f, drumDiameter), i == 1 ? canister : red,
                PrimitiveType.Cylinder);
            Part(root, "DrumRim", foot + new Vector3(0f, drumHeight + 0.01f, 0f),
                new Vector3(drumDiameter * 0.92f, 0.01f, drumDiameter * 0.92f), rim, PrimitiveType.Cylinder);
            Part(root, "DrumHoop", foot + new Vector3(0f, drumHeight * 0.33f, 0f),
                new Vector3(drumDiameter * 1.03f, 0.015f, drumDiameter * 1.03f), dark, PrimitiveType.Cylinder);
        }

        // Jerrycans, two rows of three, on the right pallet.
        const float canHeight = 0.47f;
        for (int row = 0; row < 2; row++)
            for (int column = 0; column < 3; column++)
            {
                Vector3 foot = new Vector3(0.7f - 0.38f + column * 0.38f, palletHeight, row == 0 ? -0.22f : 0.22f);
                Part(root, "Jerrycan", foot + new Vector3(0f, canHeight * 0.5f, 0f),
                    new Vector3(0.17f, canHeight, 0.34f), canister);
                Part(root, "CanHandle", foot + new Vector3(0f, canHeight + 0.03f, 0f),
                    new Vector3(0.05f, 0.06f, 0.2f), dark);
            }

        // Low barrier: posts at the corners and midpoints, rails in alternating
        // red and yellow sections, which is what makes it read as "danger".
        const float halfX = 1.75f;
        const float halfZ = 1.35f;
        const float railHeight = 0.62f;
        Vector3[] corners =
        {
            new Vector3(-halfX, 0f, -halfZ), new Vector3(halfX, 0f, -halfZ),
            new Vector3(halfX, 0f, halfZ), new Vector3(-halfX, 0f, halfZ)
        };
        for (int side = 0; side < 4; side++)
        {
            Vector3 a = corners[side];
            Vector3 b = corners[(side + 1) % 4];
            Part(root, "BarrierPost", a + new Vector3(0f, railHeight * 0.5f + 0.05f, 0f),
                new Vector3(0.07f, railHeight + 0.1f, 0.07f), post);
            float length = Vector3.Distance(a, b);
            int sections = Mathf.Max(2, Mathf.RoundToInt(length / 0.5f));
            for (int s = 0; s < sections; s++)
            {
                Vector3 centre = Vector3.Lerp(a, b, (s + 0.5f) / sections) + Vector3.up * railHeight;
                Vector3 along = (b - a).normalized;
                GameObject rail = Part(root, "BarrierRail", centre,
                    new Vector3(0.05f, 0.1f, length / sections), s % 2 == 0 ? red : yellow);
                rail.transform.localRotation = Quaternion.LookRotation(along, Vector3.up);
            }
        }

        // Warning board on a post at the front corner.
        Vector3 signFoot = new Vector3(halfX + 0.35f, 0f, halfZ + 0.2f);
        Part(root, "SignPost", signFoot + new Vector3(0f, 0.65f, 0f), new Vector3(0.06f, 1.3f, 0.06f), post);
        Part(root, "SignBoard", signFoot + new Vector3(0f, 1.2f, 0.04f), new Vector3(0.62f, 0.46f, 0.03f), yellow);
        Part(root, "SignFlame", signFoot + new Vector3(0f, 1.2f, 0.057f), new Vector3(0.16f, 0.24f, 0.01f), red);

        return root.GetComponent<Target>();
    }

    /// <summary>
    /// A dashed amber circle on the ground at the fuel blast radius, so the
    /// player can see which vehicles the explosion will take with it.
    /// </summary>
    public static GameObject DangerRing(Transform parent, float radius)
    {
        var ring = new GameObject("DangerRing");
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = Vector3.up * 0.06f;
        ring.AddComponent<MeshFilter>().sharedMesh = Annulus(radius - 0.22f, radius, 48, true);
        var renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GlowMaterial(new Color(1f, 0.45f, 0.1f, 0.8f));
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ring.AddComponent<RingPulse>().mode = RingPulse.Mode.Breathe;
        return ring;
    }

    /// <summary>An amber ring that expands from the jammer and fades — the jamming field made visible.</summary>
    public static GameObject JamWave(Transform parent, float radius)
    {
        var wave = new GameObject("JamWave");
        wave.transform.SetParent(parent, false);
        wave.transform.localPosition = Vector3.up * 0.08f;
        wave.AddComponent<MeshFilter>().sharedMesh = Annulus(0.96f, 1f, 64, false);
        var renderer = wave.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GlowMaterial(new Color(1f, 0.55f, 0.15f, 0.7f));
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var pulse = wave.AddComponent<RingPulse>();
        pulse.mode = RingPulse.Mode.Expand;
        pulse.maxRadius = radius;
        return wave;
    }

    // ---------- helpers ----------

    static GameObject Root(string name, Vector3 position, float yaw, Vector3 colliderSize, Target.Kind kind)
    {
        var root = new GameObject(name);
        root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        var collider = root.AddComponent<BoxCollider>();
        collider.size = colliderSize;
        collider.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
        var target = root.AddComponent<Target>();
        target.SetKind(kind);
        return root;
    }

    static Material Mat(string name)
    {
        Material material = Resources.Load<Material>("Materials/" + name);
        if (material == null) material = Resources.Load<Material>("Materials/Mat_RustMetal");
        return material;
    }

    static GameObject Part(GameObject parent, string name, Vector3 localPosition, Vector3 size,
                           Material material, PrimitiveType type = PrimitiveType.Cube)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            // Destroy is deferred to the end of the frame; disable now so the
            // part never takes a hit meant for the root's collider.
            collider.enabled = false;
            Discard(collider);
        }
        if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    /// <summary>Destroy in play mode, DestroyImmediate for the editor review capture.</summary>
    public static void Discard(Object target)
    {
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    static void Cable(GameObject parent, Vector3 from, Vector3 to, Material material)
    {
        Vector3 span = to - from;
        GameObject cable = Part(parent, "Cable", (from + to) * 0.5f,
            new Vector3(0.04f, 0.04f, span.magnitude), material);
        cable.transform.localRotation = Quaternion.LookRotation(span.normalized, Vector3.up);
    }

    /// <summary>
    /// Flat ring in the XZ plane. UVs all point at the centre of the glow
    /// texture, so an additive material renders it as a solid band.
    /// </summary>
    static Mesh Annulus(float inner, float outer, int segments, bool dashed)
    {
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < segments; i++)
        {
            if (dashed && i % 2 == 1) continue;
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            int start = vertices.Count;
            vertices.Add(new Vector3(Mathf.Cos(a0) * inner, 0f, Mathf.Sin(a0) * inner));
            vertices.Add(new Vector3(Mathf.Cos(a0) * outer, 0f, Mathf.Sin(a0) * outer));
            vertices.Add(new Vector3(Mathf.Cos(a1) * inner, 0f, Mathf.Sin(a1) * inner));
            vertices.Add(new Vector3(Mathf.Cos(a1) * outer, 0f, Mathf.Sin(a1) * outer));
            triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
        }
        var uvs = new Vector2[vertices.Count];
        for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(0.5f, 0.5f);
        var mesh = new Mesh { name = dashed ? "DashedRing" : "Ring" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material GlowMaterial(Color colour)
    {
        Material source = Resources.Load<Material>("Materials/Mat_Spark");
        if (source == null) return null;
        var material = new Material(source) { color = colour };
        return material;
    }
}
