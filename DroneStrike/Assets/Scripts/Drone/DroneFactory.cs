using UnityEngine;

/// <summary>
/// Builds a drone from primitives at runtime — frame, arms, motors, props,
/// payload and the FPV camera — so the whole aircraft exists in code and no
/// prefab has to be assembled by hand.
///
/// Seen from above, a standard quad layout:
///
///     ╲       ╱
///      ●─────●        ● motors with props
///      │ ▮▮▮ │        ▮ battery and payload on the centre plate
///      ●─────●
///     ╱       ╲
/// </summary>
public static class DroneFactory
{
    const float ArmLength = 0.34f;
    const float PropRadius = 0.16f;
    const int HiddenFromFpvLayer = 30;

    public static DroneRig Create(Vector3 position, Quaternion rotation, WarheadType warhead)
    {
        var drone = new GameObject("Drone");
        drone.transform.position = position;
        drone.transform.rotation = rotation;

        var body = drone.AddComponent<Rigidbody>();
        body.mass = 1.1f;

        // One box collider around the frame. Per-arm colliders would snag on
        // scenery and make crashes feel arbitrary.
        var collider = drone.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.62f, 0.16f, 0.62f);

        DroneModel model = DroneLoadout.Selected;
        int tier = DroneLoadout.SelectedIndex;

        Material frameMaterial = Resources.Load<Material>("Materials/Mat_DroneFrame");
        Material propMaterial = Resources.Load<Material>("Materials/Mat_Propeller");

        // A runtime instance rather than the shared asset: three airframes fly
        // in the same session and each wears its own colour, so tinting the
        // shared material would repaint the ones already built.
        Material accentMaterial = TintedAccent(model.accent);

        BuildFrame(drone.transform, frameMaterial, accentMaterial);
        BuildArmsAndRotors(drone.transform, frameMaterial, propMaterial);
        Transform view = BuildCamera(drone.transform, accentMaterial);
        BuildWarheadView(view, warhead, tier, model.accent);
        // The wide FPV lens otherwise catches the nearby arms and spinning
        // blades as large black wedges when the pilot looks down. Keep the
        // payload in view while excluding only the airframe's visual pieces.
        foreach (Renderer renderer in drone.GetComponentsInChildren<Renderer>())
            if (!renderer.transform.IsChildOf(view))
                renderer.gameObject.layer = HiddenFromFpvLayer;
        view.GetComponent<Camera>().cullingMask &= ~(1 << HiddenFromFpvLayer);

        var controller = drone.AddComponent<DroneController>();
        // Forward is measured from the camera, so the controller needs it before
        // its first FixedUpdate.
        controller.aimReference = view;

        // The airframe's own handling, applied before the charge's factors are
        // stacked on top — the two multiply, so a light drone with a light
        // charge really is the nimblest thing on the map.
        controller.thrust *= model.thrustFactor;
        controller.maxSpeed *= model.speedFactor;
        controller.climbThrust *= model.thrustFactor;
        controller.drag *= model.dragFactor;

        // Fit() rather than assigning the fields: the charge changes how the
        // drone handles, and that has to happen after the type is known.
        var warheadComponent = drone.AddComponent<Warhead>();
        warheadComponent.Fit(warhead, model.damageFactor);

        var battery = drone.AddComponent<DroneBattery>();
        battery.hoverEndurance *= model.enduranceFactor;

        drone.AddComponent<SignalLink>();
        drone.AddComponent<DroneImpact>();
        drone.AddComponent<RotorSpin>();
        drone.AddComponent<DroneAudio>();

        var gimbal = drone.AddComponent<DroneCameraGimbal>();
        gimbal.cameraTransform = view;

        var rig = drone.AddComponent<DroneRig>();

        // The camera has to exist before DroneRig.Awake reads it, which it does
        // because AddComponent runs Awake immediately and the camera is already
        // parented by now.
        if (view == null) Debug.LogError("DroneFactory: the drone was built without a camera.");

        return rig;
    }

    static void BuildFrame(Transform parent, Material frame, Material accent)
    {
        AddBox(parent, "Plate", new Vector3(0f, 0f, 0f),
               new Vector3(0.17f, 0.03f, 0.30f), frame);

        // Battery pack on top, the bright block that reads as "this end up".
        AddBox(parent, "Battery", new Vector3(0f, 0.055f, -0.02f),
               new Vector3(0.11f, 0.07f, 0.17f), accent);

        // Payload slung underneath.
        AddBox(parent, "Payload", new Vector3(0f, -0.07f, 0.05f),
               new Vector3(0.10f, 0.09f, 0.14f), frame);
    }

    /// <summary>
    /// Four arms in the classic X, each carrying a motor and a two-bladed rotor.
    ///
    /// The rotors are real blades rather than flat discs. A disc is what a prop
    /// blurs into once it is spinning, so it seems like a fair shortcut — but a
    /// drone sitting still, or seen the instant before it hits, wears four
    /// solid circles and reads as running on wheels. Two tapered, twisted
    /// blades cost almost nothing and fix that outright.
    /// </summary>
    static void BuildArmsAndRotors(Transform parent, Material frame, Material prop)
    {
        float[] angles = { 45f, 135f, 225f, 315f };

        Mesh blade = PrimitiveMesh.Blade(PropRadius, 0.042f, 0.022f, 0.006f, 22f);

        for (int i = 0; i < angles.Length; i++)
        {
            Quaternion spin = Quaternion.Euler(0f, angles[i], 0f);
            Vector3 direction = spin * Vector3.forward;
            Vector3 motorPosition = direction * ArmLength;

            var arm = AddBox(parent, "Arm" + i, motorPosition * 0.5f,
                             new Vector3(0.05f, 0.025f, ArmLength), frame);
            arm.transform.localRotation = spin;

            AddCylinder(parent, "Motor" + i, motorPosition + Vector3.up * 0.03f,
                        new Vector3(0.075f, 0.035f, 0.075f), frame);

            // RotorSpin finds these by name among the drone's direct children
            // and turns the whole assembly, so the blades hang off this rather
            // than off the airframe.
            var rotor = new GameObject("Prop" + i);
            rotor.transform.SetParent(parent, false);
            rotor.transform.localPosition = motorPosition + Vector3.up * 0.07f;

            AddCylinder(rotor.transform, "Hub", Vector3.zero,
                        new Vector3(0.03f, 0.008f, 0.03f), frame);

            for (int b = 0; b < 2; b++)
            {
                var go = new GameObject("Blade" + b);
                go.transform.SetParent(rotor.transform, false);
                go.transform.localRotation = Quaternion.Euler(0f, b * 180f, 0f);

                go.AddComponent<MeshFilter>().sharedMesh = blade;
                var renderer = go.AddComponent<MeshRenderer>();
                if (prop != null) renderer.sharedMaterial = prop;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }

    /// <summary>
    /// The FPV camera, tilted down a little the way a real one is mounted so the
    /// pilot can see where they are going while the drone leans forward.
    /// </summary>
    static Transform BuildCamera(Transform parent, Material accent)
    {
        var housing = AddBox(parent, "CameraHousing", new Vector3(0f, 0.02f, 0.16f),
                             new Vector3(0.05f, 0.05f, 0.05f), accent);
        housing.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

        // Parented for position only — DroneCameraGimbal overwrites the rotation
        // every frame so the body's pitch and roll never reach the view.
        var cameraGO = new GameObject("FPVCamera");
        cameraGO.transform.SetParent(parent, false);
        cameraGO.transform.localPosition = new Vector3(0f, 0.04f, 0.18f);

        var camera = cameraGO.AddComponent<Camera>();
        camera.fieldOfView = 92f;      // wide, like the lens on a real FPV rig
        camera.nearClipPlane = 0.04f;
        camera.farClipPlane = 850f; // Beyond the longest map's fog end, avoiding a visible cut-off.
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = false;
        camera.allowMSAA = true;

        cameraGO.AddComponent<AudioListener>();

        return cameraGO.transform;
    }

    /// <summary>
    /// The payload itself, in view: an RPG-type shaped charge, the thing real
    /// FPV strike drones actually carry, slung under the camera with its nose
    /// poking into the bottom of the frame.
    ///
    /// History, so the next pass does not repeat it: this was a smooth
    /// tube-flare-ogive of revolution (and before that a downloaded missile
    /// and a flat-nosed pod). Rendered, the smooth version read as a candle
    /// or a suppository — one soft rounded silhouette, as wide at the tail as
    /// at the head, seen end-on with its flat tail disc toward the lens.
    /// What makes an RPG grenade read as ordnance is the opposite: a head
    /// wider than everything behind it, a hard-edged stepped nose cone with
    /// a thin fuse probe, and a narrow tail tube. So it is built from
    /// separate hard-edged frustums (no smoothed normals across the joins),
    /// with a yellow HE band and black tape, and placed so only the nose and
    /// head are in frame — the tail runs back under the camera out of view.
    /// Verified with Tools > Drone Strike > Capture Review Shots (fpv_charge,
    /// warhead_side).
    ///
    /// Charge size scales it; the heavy charge carries a tandem precursor on
    /// a long probe (PG-7VR-style); the better airframes add tape wraps.
    /// </summary>
    static void BuildWarheadView(Transform cameraTransform, WarheadType warhead, int tier,
                                 Color accent)
    {
        Material body = Resources.Load<Material>("Materials/Mat_Warhead");
        Material dark = Resources.Load<Material>("Materials/Mat_DroneFrame");
        Material metal = Resources.Load<Material>("Materials/Mat_Chrome");
        Material marking = Resources.Load<Material>("Materials/Mat_Hazard");
        Material tape = Resources.Load<Material>("Materials/Mat_Rubber");
        if (dark == null) dark = body;
        if (metal == null) metal = dark;
        if (marking == null) marking = Resources.Load<Material>("Materials/Mat_WarheadBand");
        if (tape == null) tape = dark;

        float scale = warhead == WarheadType.Compact ? 0.8f
                    : warhead == WarheadType.Standard ? 0.9f : 1f;

        var root = new GameObject("WarheadView");
        root.transform.SetParent(cameraTransform, false);
        // Head centre 0.36 m ahead of and 0.24 m below the lens (~34 deg
        // below the view axis, half FOV 46): nose tip at ~18 deg, the tail
        // tube runs back past the lower edge of the frame.
        root.transform.localPosition = new Vector3(0f, -0.24f, 0.36f);
        // Nose ~10 deg up from the line of sight: pointed ahead at what the
        // pilot is flying into, tipped just enough that the cone and band
        // show. At 25 deg it stood almost upright in the frame.
        root.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
        root.transform.localScale = Vector3.one * scale;

        // Profile along local +Y (the nose), metres at scale 1. Head
        // diameter 92 mm, like a PG-7 grenade.
        const float headRadius = 0.046f;
        const float tailRadius = 0.022f;
        Segment(root, "TailTube", tailRadius, tailRadius, -0.26f, -0.07f, dark);
        Segment(root, "BoatTail", tailRadius, headRadius, -0.07f, -0.02f, body);
        Segment(root, "Head", headRadius, headRadius, -0.02f, 0.04f, body);
        Segment(root, "NoseLower", headRadius, 0.033f, 0.04f, 0.085f, body);
        Segment(root, "NoseUpper", 0.033f, 0.013f, 0.085f, 0.135f, body);
        Segment(root, "FuseCap", 0.013f, 0.009f, 0.135f, 0.148f, metal);

        float tip = 0.148f;
        if (warhead == WarheadType.Heavy)
        {
            // Tandem precursor: a long rod with a small charge on its end.
            Segment(root, "PrecursorRod", 0.006f, 0.006f, tip, tip + 0.09f, metal);
            Segment(root, "Precursor", 0.017f, 0.017f, tip + 0.09f, tip + 0.115f, body);
            Segment(root, "PrecursorNose", 0.017f, 0.004f, tip + 0.115f, tip + 0.14f, body);
        }
        else
        {
            Segment(root, "FuseProbe", 0.005f, 0.005f, tip, tip + 0.03f, metal);
            Segment(root, "FuseTip", 0.005f, 0f, tip + 0.03f, tip + 0.038f, metal);
        }

        // Yellow HE band round the head, black tape where it is lashed on.
        Segment(root, "MarkingBand", headRadius * 1.012f, headRadius * 1.012f, 0.012f, 0.022f, marking);
        Segment(root, "TapeHead", headRadius * 1.02f, headRadius * 1.02f, -0.015f, -0.004f, tape);
        Segment(root, "TapeTail", tailRadius * 1.15f, tailRadius * 1.15f, -0.12f, -0.105f, tape);
        if (tier >= 1)
            Segment(root, "TapeTail2", tailRadius * 1.15f, tailRadius * 1.15f, -0.2f, -0.185f, tape);

        // Zip-tie strap up to the airframe, and tail fins (both mostly
        // behind the frame edge; they keep the silhouette honest if the
        // field of view is widened).
        AddBox(root.transform, "MountStrap", new Vector3(0f, -0.16f, -0.03f),
               new Vector3(0.01f, 0.03f, 0.02f), tape);
        for (int i = 0; i < 4; i++)
        {
            Quaternion spin = Quaternion.Euler(0f, i * 90f + 45f, 0f);
            var fin = AddBox(root.transform, "Fin" + i,
                             new Vector3(0f, -0.23f, 0f) + spin * new Vector3(0f, 0f, tailRadius + 0.012f),
                             new Vector3(0.003f, 0.05f, 0.024f), dark);
            fin.transform.localRotation = spin;
        }
    }

    /// <summary>A hard-edged frustum between two heights along local +Y.</summary>
    static void Segment(GameObject root, string name, float bottomRadius, float topRadius,
                        float bottom, float top, Material material)
    {
        AddMesh(root.transform, name, new Vector3(0f, (bottom + top) * 0.5f, 0f),
                PrimitiveMesh.Frustum(bottomRadius, topRadius, top - bottom, 24), material);
    }

    // ---------- helpers ----------

    /// <summary>
    /// The accent colour for the airframe being built, as its own material
    /// instance so each drone keeps its own paint.
    /// </summary>
    static Material TintedAccent(Color accent)
    {
        Material source = Resources.Load<Material>("Materials/Mat_DroneAccent");
        if (source == null) return null;

        var instance = new Material(source);
        instance.color = accent;
        return instance;
    }

    static GameObject AddMesh(Transform parent, string name, Vector3 localPosition,
                              Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        if (material != null) renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return go;
    }

    static GameObject AddBox(Transform parent, string name, Vector3 localPosition,
                             Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;

        Strip(box, material);
        return box;
    }

    static GameObject AddCylinder(Transform parent, string name, Vector3 localPosition,
                                  Vector3 size, Material material)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localScale = size;

        Strip(cylinder, material);
        return cylinder;
    }

    /// <summary>
    /// Primitives arrive with their own colliders; the drone uses one box for the
    /// whole airframe, so the parts must not bring extras.
    /// </summary>
    static void Strip(GameObject go, Material material)
    {
        Object.Destroy(go.GetComponent<Collider>());

        var renderer = go.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
