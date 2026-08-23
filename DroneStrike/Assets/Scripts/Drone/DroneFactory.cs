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

        Material frameMaterial = Resources.Load<Material>("Materials/Mat_DroneFrame");
        Material accentMaterial = Resources.Load<Material>("Materials/Mat_DroneAccent");
        Material propMaterial = Resources.Load<Material>("Materials/Mat_Propeller");

        BuildFrame(drone.transform, frameMaterial, accentMaterial);
        BuildArmsAndRotors(drone.transform, frameMaterial, propMaterial);
        Transform view = BuildCamera(drone.transform, accentMaterial);

        var controller = drone.AddComponent<DroneController>();
        // Forward is measured from the camera, so the controller needs it before
        // its first FixedUpdate.
        controller.aimReference = view;

        var warheadComponent = drone.AddComponent<Warhead>();
        warheadComponent.type = warhead;
        drone.AddComponent<DroneBattery>();
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

    // ---------- helpers ----------

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
