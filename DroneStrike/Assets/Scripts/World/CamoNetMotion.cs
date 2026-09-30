using UnityEngine;

/// <summary>Small movement in free fabric spans; rope attachment vertices stay fixed.</summary>
[RequireComponent(typeof(MeshFilter))]
public class CamoNetMotion : MonoBehaviour
{
    Mesh mesh;
    Vector3[] rest;
    Vector3[] moved;
    int stride;
    float blastStrength;
    float blastTime;

    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        rest = mesh.vertices;
        moved = new Vector3[rest.Length];
        stride = Mathf.RoundToInt(Mathf.Sqrt(rest.Length / 2f));
    }

    void OnEnable() { GameEffects.OnExplosionAt += ReactToBlast; }
    void OnDisable() { GameEffects.OnExplosionAt -= ReactToBlast; }

    void ReactToBlast(Vector3 position, float radius)
    {
        float distance = Vector3.Distance(position, transform.position);
        if (distance > radius + 22f) return;
        blastStrength = Mathf.Max(blastStrength, 0.09f *
            Mathf.Clamp01(1f - distance / (radius + 22f)));
        blastTime = Time.time;
    }

    void Update()
    {
        if (mesh == null || stride < 2) return;
        float afterBlast = blastStrength * Mathf.Exp(-(Time.time - blastTime) * 2.6f);
        if (afterBlast < 0.001f) blastStrength = 0f;
        int oneSide = stride * stride;
        for (int i = 0; i < moved.Length; i++)
        {
            int onSide = i % oneSide;
            float u = (float)(onSide % stride) / (stride - 1);
            float v = (float)(onSide / stride) / (stride - 1);
            float freedom = Mathf.Sin(u * Mathf.PI) * Mathf.Abs(Mathf.Sin(v * 2f * Mathf.PI));
            moved[i] = rest[i];
            moved[i].y += freedom * (0.035f * Mathf.Sin(Time.time * 1.7f + u * 5f + v * 3f)
                + afterBlast * Mathf.Sin((Time.time - blastTime) * 15f));
        }
        mesh.vertices = moved;
        mesh.RecalculateBounds();
        // The resting MeshCollider stays in place. The free-span displacement
        // is capped below 0.13 m, so rebuilding its triangles every frame in
        // WebGL would cost more than the visual movement warrants.
    }
}
