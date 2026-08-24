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

        Material frameMaterial = Resources.Load<Material>("Materials/Mat_DroneFrame");
        Material propMaterial = Resources.Load<Material>("Materials/Mat_Propeller");

        // A runtime instance rather than the shared asset: three airframes fly
        // in the same session and each wears its own colour, so tinting the
        // shared material would repaint the ones already built.
        Material accentMaterial = TintedAccent(model.accent);

        BuildFrame(drone.transform, frameMaterial, accentMaterial);
        BuildArmsAndRotors(drone.transform, frameMaterial, propMaterial);
        Transform view = BuildCamera(drone.transform, accentMaterial);
        BuildWarheadView(view, warhead);

        var controller = drone.AddComponent<DroneController>();
        // Forward is measured from the camera, so the controller needs it before
        // its first FixedUpdate.
        controller.aimReference = view;

        // The airframe's own handling, applied before Warhead.Awake stacks the
        // charge's factors on top — the two multiply, so a light drone with a
        // light charge really is the nimblest thing on the map.
        controller.thrust *= model.thrustFactor;
        controller.maxSpeed *= model.speedFactor;
        controller.climbThrust *= model.thrustFactor;

        var warheadComponent = drone.AddComponent<Warhead>();
        warheadComponent.type = warhead;
        warheadComponent.damageMultiplier = model.damageFactor;

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

    static void BuildArmsAndRotors(Transform parent, Material frame, Material prop)
    {
        // Four arms at the corners, the classic X layout.
        float[] angles = { 45f, 135f, 225f, 315f };

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

            var propeller = AddCylinder(parent, "Prop" + i,
                                        motorPosition + Vector3.up * 0.07f,
                                        new Vector3(PropRadius * 2f, 0.004f, PropRadius * 2f), prop);
            propeller.name = "Prop" + i;
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
    /// The payload itself, in view. FPV kamikaze footage always shows the
    /// warhead's own nose poking into the bottom of frame — it is mounted
    /// ahead of and below the camera, not out of sight — so it is built here
    /// as a fixture of the camera rather than of the airframe: it has to stay
    /// framed the same way no matter how the drone is tilted, exactly like
    /// DroneCameraGimbal keeps the horizon steady.
    ///
    /// Modelled on a PG-7-style rocket rather than a plain cone: a bulbous
    /// ogive nose wider than the tube behind it, which is the actual shape a
    /// warhead like this has and reads as "ordnance" rather than "party hat".
    /// The compact charge is visibly smaller than the standard one, so the
    /// loadout is legible before a single HUD number is read.
    /// </summary>
    static void BuildWarheadView(Transform cameraTransform, WarheadType warhead)
    {
        Material body = Resources.Load<Material>("Materials/Mat_Warhead");
        Material band = Resources.Load<Material>("Materials/Mat_WarheadBand");

        float scale = warhead == WarheadType.Compact ? 0.75f : 1f;

        var root = new GameObject("WarheadView");
        root.transform.SetParent(cameraTransform, false);
        // Slung low and forward, nose tipped down and away — mounted under the
        // drone's belly the way the real thing is, not held up in front of the lens.
        root.transform.localPosition = new Vector3(0f, -0.30f, 0.5f);
        root.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        root.transform.localScale = Vector3.one * scale;

        AddCapsule(root.transform, "Nose", new Vector3(0f, 0.16f, 0f),
                  new Vector3(0.17f, 0.11f, 0.17f), body);

        AddCylinder(root.transform, "Band", new Vector3(0f, 0.02f, 0f),
                   new Vector3(0.135f, 0.012f, 0.135f), band);

        AddCylinder(root.transform, "Tube", new Vector3(0f, -0.08f, 0f),
                   new Vector3(0.075f, 0.22f, 0.075f), body);

        // Tail fins: four thin fins fanned out around the tube's rear, the
        // last detail that sells the silhouette at a glance.
        for (int i = 0; i < 4; i++)
        {
            Quaternion spin = Quaternion.Euler(0f, i * 90f, 0f);
            Vector3 offset = spin * new Vector3(0f, 0f, 0.045f);

            var fin = AddBox(root.transform, "Fin" + i, new Vector3(0f, -0.26f, 0f) + offset,
                             new Vector3(0.01f, 0.09f, 0.09f), body);
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

    static GameObject AddCapsule(Transform parent, string name, Vector3 localPosition,
                                 Vector3 size, Material material)
    {
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = name;
        capsule.transform.SetParent(parent, false);
        capsule.transform.localPosition = localPosition;
        capsule.transform.localScale = size;

        Strip(capsule, material);
        return capsule;
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
