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

        // Fit() rather than assigning the fields: the charge changes how the
        // drone handles, and that has to happen after the type is known.
        var warheadComponent = drone.AddComponent<Warhead>();
        warheadComponent.Fit(warhead, model.damageFactor);

        var battery = drone.AddComponent<DroneBattery>();
        battery.hoverEndurance *= model.enduranceFactor;

        drone.AddComponent<SignalLink>();
        drone.AddComponent<DroneImpact>();
        drone.AddComponent<RotorSpin>();

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
        camera.farClipPlane = 600f;

        cameraGO.AddComponent<AudioListener>();

        return cameraGO.transform;
    }

    /// <summary>
    /// The payload itself, in view.
    ///
    /// FPV strike footage always shows the warhead's own nose poking into the
    /// bottom of frame — it is mounted ahead of and below the camera, not out
    /// of sight — so it is built as a fixture of the camera rather than of the
    /// airframe: it has to stay framed the same way no matter how the drone is
    /// tilted, exactly like DroneCameraGimbal keeps the horizon steady.
    ///
    /// Shaped as a shaped-charge warhead: a sharp conical tip, a wide shoulder
    /// behind it, then a narrow tube. The earlier version used a capsule for
    /// the nose, which is round at both ends and wider than the tube behind it,
    /// and the silhouette that produces is not the one anybody wants on screen.
    /// A cone has a point, and a point reads as ordnance.
    ///
    /// It also grows with the airframe. Each unlock is meant to feel like
    /// better kit, and a number on a menu the player has already left does not
    /// do that — the charge in front of them all mission does.
    /// </summary>
    static void BuildWarheadView(Transform cameraTransform, WarheadType warhead, int tier,
                                 Color accent)
    {
        Material body = Resources.Load<Material>("Materials/Mat_Warhead");
        Material band = Resources.Load<Material>("Materials/Mat_WarheadBand");
        Material trim = TintedAccent(accent);

        float scale = warhead == WarheadType.Compact ? 0.78f : 1f;
        scale *= 1f + tier * 0.09f;

        var root = new GameObject("WarheadView");
        root.transform.SetParent(cameraTransform, false);
        // Slung low and forward, nose tipped down and away — mounted under the
        // drone's belly the way the real thing is, not held up in front of the lens.
        root.transform.localPosition = new Vector3(0f, -0.31f, 0.52f);
        root.transform.localRotation = Quaternion.Euler(72f, 0f, 0f);
        root.transform.localScale = Vector3.one * scale;

        // Tip, shoulder, tube. Heights are chained off each other so the three
        // always meet however the numbers are tuned.
        const float tipHeight = 0.20f;
        const float shoulderHeight = 0.10f;
        const float tubeHeight = 0.26f;

        float tubeTop = -0.06f + tubeHeight * 0.5f;
        float shoulderCentre = tubeTop + shoulderHeight * 0.5f;
        float tipCentre = tubeTop + shoulderHeight + tipHeight * 0.5f;

        AddMesh(root.transform, "Tip", new Vector3(0f, tipCentre, 0f),
                PrimitiveMesh.Frustum(0.062f, 0f, tipHeight), body);

        AddMesh(root.transform, "Shoulder", new Vector3(0f, shoulderCentre, 0f),
                PrimitiveMesh.Frustum(0.115f, 0.062f, shoulderHeight), body);

        AddCylinder(root.transform, "Tube", new Vector3(0f, -0.06f, 0f),
                    new Vector3(0.072f, tubeHeight * 0.5f, 0.072f), body);

        // Warning bands. A second one is the cheapest possible "this is the
        // better charge" cue, and it is read at a glance because it is the only
        // bright thing on an olive body.
        AddCylinder(root.transform, "Band", new Vector3(0f, tubeTop - 0.012f, 0f),
                    new Vector3(0.086f, 0.011f, 0.086f), band);

        if (tier >= 1)
            AddCylinder(root.transform, "BandLower", new Vector3(0f, tubeTop - 0.056f, 0f),
                        new Vector3(0.082f, 0.008f, 0.082f), trim);

        // The top airframe carries a tandem precursor on a standoff probe, which
        // is what a real one looks like and is unmistakable in silhouette.
        if (tier >= 2)
        {
            AddCylinder(root.transform, "Probe", new Vector3(0f, tipCentre + tipHeight * 0.5f + 0.05f, 0f),
                        new Vector3(0.012f, 0.05f, 0.012f), body);

            AddMesh(root.transform, "Precursor",
                    new Vector3(0f, tipCentre + tipHeight * 0.5f + 0.125f, 0f),
                    PrimitiveMesh.Frustum(0.03f, 0f, 0.07f), trim);
        }

        // Tail fins, fanned around the tube's rear. The better airframes carry
        // more of them, so the tail reads differently too.
        int fins = tier >= 2 ? 6 : 4;
        float finY = -0.06f - tubeHeight * 0.5f + 0.055f;

        for (int i = 0; i < fins; i++)
        {
            Quaternion spin = Quaternion.Euler(0f, i * (360f / fins), 0f);
            Vector3 offset = spin * new Vector3(0f, 0f, 0.052f);

            var fin = AddBox(root.transform, "Fin" + i, new Vector3(0f, finY, 0f) + offset,
                             new Vector3(0.008f, 0.085f, 0.075f), body);
            fin.transform.localRotation = spin;
        }
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
