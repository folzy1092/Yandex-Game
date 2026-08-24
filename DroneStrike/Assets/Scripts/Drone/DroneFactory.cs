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
    /// The nose is one continuous curved profile — cylindrical tube, a shoulder
    /// that flares out wider than the tube, then an ogive taper to a point —
    /// built as a single lofted mesh with PrimitiveMesh.Revolve rather than a
    /// stack of separate frustums. Separate pieces meeting at mismatched radii
    /// is exactly what read as a stepped, pancake-like shape; one continuous
    /// profile is what an actual shaped-charge round looks like.
    ///
    /// A short mounting strap ties the tube to the camera housing above it, so
    /// it reads as slung underneath the airframe rather than floating loose in
    /// front of the lens.
    ///
    /// It also grows with the airframe: a second band on the second drone, a
    /// tandem precursor and more fins on the third. A number on a menu the
    /// player has already left does not sell an upgrade — the charge sitting in
    /// front of them all mission does.
    /// </summary>
    /// <summary>
    /// Assets/Resources/Models/Warhead.glb — "Missile" by Poly by Google
    /// (poly.pizza/m/dPVCvXP-S58, CC-BY, credited in CREDITS.txt).
    ///
    /// Its own rest orientation has never been seen in an editor, the same
    /// situation every downloaded model in this project started in. These
    /// three are the numbers to change if the next screenshot shows it lying
    /// on its side, nose backwards, or upside down — nothing else about the
    /// mount needs to move.
    /// </summary>
    const float WarheadModelPitch = 0f;
    const float WarheadModelYaw = 0f;
    const float WarheadModelRoll = 0f;

    /// <summary>Nose-to-tail length the model is rescaled to, in metres, before the loadout scale is applied.</summary>
    const float WarheadModelLength = 0.42f;

    static void BuildWarheadView(Transform cameraTransform, WarheadType warhead, int tier,
                                 Color accent)
    {
        Material body = Resources.Load<Material>("Materials/Mat_Warhead");
        Material band = Resources.Load<Material>("Materials/Mat_WarheadBand");
        Material trim = TintedAccent(accent);

        float scale = warhead == WarheadType.Compact ? 0.80f : 1f;
        scale *= 1f + tier * 0.08f;

        var root = new GameObject("WarheadView");
        root.transform.SetParent(cameraTransform, false);
        // Slung close under the housing, nose tipped forward and down — mounted
        // to the airframe the way the real thing is, not held out in empty air.
        root.transform.localPosition = new Vector3(0f, -0.22f, 0.40f);
        root.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);

        GameObject downloaded = ModelLibrary.Instantiate("Warhead", root.transform);
        if (downloaded != null)
        {
            // Pitch and yaw are unverified against the actual mesh — the same
            // situation Tank.glb and SupplyTent.glb started in, and the same
            // fix: these are the two numbers to turn once it is visible in a
            // build, rather than anything to do with the model itself.
            downloaded.transform.localRotation =
                Quaternion.Euler(WarheadModelPitch, WarheadModelYaw, WarheadModelRoll);
            FitWarheadModelLength(downloaded, WarheadModelLength);

            root.transform.localScale = Vector3.one * scale;
            return;
        }
        root.transform.localScale = Vector3.one * scale;

        const float tubeRadius = 0.072f;
        const float tubeBottom = -0.17f;
        const float tubeTop = 0.05f;

        var profile = new[]
        {
            new Vector2(tubeRadius, tubeBottom),          // tail end of the tube
            new Vector2(tubeRadius, tubeTop),              // tube meets the shoulder
            new Vector2(0.100f, tubeTop + 0.045f),          // the flare — wider than the tube,
                                                             // the silhouette that reads as "warhead"
            new Vector2(0.086f, tubeTop + 0.095f),
            new Vector2(0.058f, tubeTop + 0.150f),
            new Vector2(0.026f, tubeTop + 0.195f),
            new Vector2(0f, tubeTop + 0.225f)               // the point
        };

        AddMesh(root.transform, "Nose", Vector3.zero, PrimitiveMesh.Revolve(profile), body);

        // A strap linking the tube to the underside of the camera housing —
        // the detail that reads as "attached" rather than "hovering nearby".
        var strap = AddBox(root.transform, "MountStrap", new Vector3(0f, tubeTop - 0.03f, -0.055f),
                           new Vector3(0.03f, 0.10f, 0.03f), trim);
        strap.transform.localRotation = Quaternion.Euler(-24f, 0f, 0f);

        // The warning band. A second one on the better charges is the cheapest
        // possible "this is the stronger one" cue, and it reads at a glance
        // because it is the only bright ring on an otherwise olive body.
        AddCylinder(root.transform, "Band", new Vector3(0f, tubeTop - 0.01f, 0f),
                    new Vector3(tubeRadius * 1.18f, 0.011f, tubeRadius * 1.18f), band);

        if (tier >= 1)
            AddCylinder(root.transform, "BandLower", new Vector3(0f, tubeTop - 0.06f, 0f),
                        new Vector3(tubeRadius * 1.1f, 0.008f, tubeRadius * 1.1f), trim);

        // The top airframe carries a tandem precursor on a standoff probe, which
        // is what a real one looks like and is unmistakable in silhouette.
        if (tier >= 2)
        {
            float tipY = profile[profile.Length - 1].y;

            AddCylinder(root.transform, "Probe", new Vector3(0f, tipY + 0.05f, 0f),
                        new Vector3(0.012f, 0.05f, 0.012f), body);

            AddMesh(root.transform, "Precursor", new Vector3(0f, tipY + 0.125f, 0f),
                    PrimitiveMesh.Frustum(0.03f, 0f, 0.07f), trim);
        }

        // Tail fins, fanned around the tube's rear. The better airframes carry
        // more of them, so the tail reads differently too.
        int fins = tier >= 2 ? 6 : 4;
        float finY = tubeBottom + 0.06f;

        for (int i = 0; i < fins; i++)
        {
            Quaternion spin = Quaternion.Euler(0f, i * (360f / fins), 0f);
            Vector3 offset = spin * new Vector3(0f, 0f, tubeRadius * 0.72f);

            var fin = AddBox(root.transform, "Fin" + i, new Vector3(0f, finY, 0f) + offset,
                             new Vector3(0.008f, 0.085f, 0.075f), body);
            fin.transform.localRotation = spin;
        }
    }

    /// <summary>
    /// Rescales a downloaded model uniformly so its longest dimension comes
    /// out at <paramref name="desiredLength"/> metres, whatever units the
    /// source file was authored in — the same reasoning TargetProps.
    /// NormalizeModelSize uses for the tank and the tent, just runnable at
    /// play time rather than only from an Editor script.
    ///
    /// Measured at identity rotation and restored afterwards: Renderer.bounds
    /// is a world-space axis-aligned box, which only reports the model's true
    /// size — rather than an inflated one — when nothing above it is rotated
    /// at the moment of measuring.
    /// </summary>
    static void FitWarheadModelLength(GameObject model, float desiredLength)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Transform t = model.transform;
        Vector3 originalPosition = t.position;
        Quaternion originalRotation = t.rotation;
        t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        t.SetPositionAndRotation(originalPosition, originalRotation);

        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest < 0.0001f) return;

        t.localScale *= desiredLength / longest;
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
