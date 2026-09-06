using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Build-time optimisation and persistence. No per-frame scene traversal.</summary>
public static class SceneVisualAssets
{
    public static void CombineGrass(Transform root)
    {
        const float cellSize = 24f;
        var cells = new Dictionary<Vector2Int, List<MeshFilter>>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            Vector3 p = filter.transform.position;
            var cell = new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));
            if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new List<MeshFilter>();
            list.Add(filter);
        }
        foreach (var cell in cells)
        {
            var chunk = new GameObject("Grass_" + cell.Key.x + "_" + cell.Key.y);
            chunk.transform.SetParent(root, false);
            chunk.transform.position = new Vector3((cell.Key.x + 0.5f) * cellSize, 0f,
                                                   (cell.Key.y + 0.5f) * cellSize);
            var combine = new CombineInstance[cell.Value.Count];
            for (int i = 0; i < combine.Length; i++)
            {
                var filter = cell.Value[i];
                combine[i] = new CombineInstance { mesh = filter.sharedMesh,
                    transform = chunk.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix };
            }
            var mesh = new Mesh { name = chunk.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(combine, true, true);
            chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = cell.Value[0].GetComponent<Renderer>().sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lod = chunk.AddComponent<LODGroup>();
            // Separate cells still cull independently. There is no giant mesh
            // keeping the entire map's grass visible from every viewpoint.
            lod.SetLODs(new[] { new LOD(0.065f, new Renderer[] { renderer }) });
            lod.RecalculateBounds();
            foreach (var filter in cell.Value)
            {
                Object.DestroyImmediate(filter.sharedMesh);
                Object.DestroyImmediate(filter.gameObject);
            }
        }
    }

    public static void PersistMeshes(Scene scene, string sceneName)
    {
        string folder = "Assets/Generated/" + sceneName;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var saved = new Dictionary<Mesh, Mesh>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                filter.sharedMesh = Persist(filter.sharedMesh, folder, saved);
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                collider.sharedMesh = Persist(collider.sharedMesh, folder, saved);
        }
        foreach (var pair in saved) Object.DestroyImmediate(pair.Key);
        AssetDatabase.SaveAssets();
    }

    static Mesh Persist(Mesh mesh, string folder, Dictionary<Mesh, Mesh> saved)
    {
        if (mesh == null || AssetDatabase.Contains(mesh)) return mesh;
        if (saved.TryGetValue(mesh, out var result)) return result;
        string path = folder + "/Mesh_" + saved.Count.ToString("D4") + ".asset";
        result = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (result == null)
        {
            result = Object.Instantiate(mesh);
            result.name = mesh.name;
            AssetDatabase.CreateAsset(result, path);
        }
        else
        {
            EditorUtility.CopySerialized(mesh, result);
            EditorUtility.SetDirty(result);
        }
        saved.Add(mesh, result);
        return result;
    }
}
