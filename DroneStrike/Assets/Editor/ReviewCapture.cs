using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders a fixed set of review shots of the mission props to
/// Builds/Review/*.png: truck, jammer station, fuel group, armour with its
/// outline and weak-rear strip, and the FPV view of the charge.
///
/// There is no one watching a Play session when an agent edits this project,
/// and props built at runtime by the challenge runner never appear in a saved
/// scene. This builds them in edit mode on the real Mission1 layout and
/// photographs them from fixed angles, so a visual change can be looked at
/// (and compared shot-for-shot with a later pass) without pressing Play.
/// Runs in batch mode too, as long as -nographics is not passed.
/// The scene is reopened afterwards without saving.
/// </summary>
public static class ReviewCapture
{
    const int Width = 1280;
    const int Height = 720;
    const string Folder = "Builds/Review";

    [MenuItem("Tools/Drone Strike/Capture Review Shots")]
    public static void Capture()
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.OpenScene("Assets/Scenes/Mission1.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();

        var targets = new List<Target>(Object.FindObjectsByType<Target>(FindObjectsSortMode.None));
        Target truck = targets.Find(t => t.kind == Target.Kind.LightVehicle && t.GetComponent<PatrolMover>() == null);
        Target tank = targets.Find(t => t.kind == Target.Kind.ArmouredVehicle);

        var camera = new GameObject("ReviewCamera").AddComponent<Camera>();
        camera.fieldOfView = 55f;
        camera.farClipPlane = 900f;
        camera.clearFlags = CameraClearFlags.Skybox;

        if (truck != null)
        {
            Shoot(camera, "truck_close", truck.transform, new Vector3(6f, 3.2f, 9f), 1.4f);
            Shoot(camera, "truck_rear", truck.transform, new Vector3(-6f, 4f, -9f), 1.4f);
            Prime(truck);
            Shoot(camera, "truck_outline_40m", truck.transform, new Vector3(22f, 22f, 26f), 1.2f);
        }

        if (tank != null)
        {
            tank.WeakRear = true;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "WeakRear";
            marker.transform.SetParent(tank.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.3f, -2.9f);
            marker.transform.localScale = new Vector3(2.2f, 0.28f, 0.15f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            Prime(tank);
            Shoot(camera, "tank_rear_outline", tank.transform, new Vector3(-8f, 9f, -16f), 1.2f);
            Shoot(camera, "tank_from_behind", tank.transform, new Vector3(0f, 6f, -14f), 1.2f);
        }

        // The two challenge layouts, built by the real runner code.
        var runner = new MissionChallengeRunner();
        runner.Configure(FindDefinition(ChallengeKind.Jammer), 0);
        Target jammer = FindKind(Target.Kind.SignalJammer);
        if (jammer != null)
        {
            PrimeAll();
            Shoot(camera, "jammer_close", jammer.transform, new Vector3(7f, 5f, 9f), 3f);
            Shoot(camera, "jammer_60m", jammer.transform, new Vector3(40f, 30f, 35f), 3f);
        }

        EditorSceneManager.OpenScene("Assets/Scenes/Mission1.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        camera = new GameObject("ReviewCamera").AddComponent<Camera>();
        camera.fieldOfView = 55f;
        camera.farClipPlane = 900f;
        runner = new MissionChallengeRunner();
        runner.Configure(FindDefinition(ChallengeKind.GroupStrike), 0);
        Target fuel = FindKind(Target.Kind.FuelDepot);
        if (fuel != null)
        {
            PrimeAll();
            Shoot(camera, "fuel_close", fuel.transform, new Vector3(4f, 3f, 6f), 0.6f);
            Shoot(camera, "fuel_group_35m", fuel.transform, new Vector3(0f, 26f, -24f), 0.5f);
        }

        // The pilot's view of the charge, from a freshly built drone.
        DroneRig drone = DroneFactory.Create(new Vector3(0f, 30f, -80f), Quaternion.identity, WarheadType.Standard);
        Camera fpv = drone.GetComponentInChildren<Camera>();
        if (fpv != null)
        {
            fpv.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            Render(fpv, "fpv_charge");
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("Drone Strike: review shots written to " + Path.GetFullPath(Folder));
    }

    static MissionDefinition FindDefinition(ChallengeKind kind)
    {
        foreach (MissionDefinition definition in MissionChallenges.Definitions)
            if (definition.kind == kind) return definition;
        return MissionChallenges.Definitions[0];
    }

    static Target FindKind(Target.Kind kind)
    {
        foreach (Target target in Object.FindObjectsByType<Target>(FindObjectsSortMode.None))
            if (target.kind == kind) return target;
        return null;
    }

    static void Prime(Target target)
    {
        TargetOutline outline = target.GetComponent<TargetOutline>();
        if (outline == null) outline = target.gameObject.AddComponent<TargetOutline>();
        outline.Refresh();
    }

    static void PrimeAll()
    {
        foreach (Target target in Object.FindObjectsByType<Target>(FindObjectsSortMode.None))
            if (target.IsPriority) Prime(target);
    }

    static void Shoot(Camera camera, string name, Transform subject, Vector3 offset, float lookHeight)
    {
        Vector3 focus = subject.position + Vector3.up * lookHeight;
        camera.transform.position = subject.position + subject.rotation * offset;
        camera.transform.LookAt(focus);
        Render(camera, name);
    }

    static void Render(Camera camera, string name)
    {
        var texture = new RenderTexture(Width, Height, 24);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(image);
    }
}
