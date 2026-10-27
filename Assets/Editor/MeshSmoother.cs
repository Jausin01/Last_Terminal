using System.Collections.Generic;
using UnityEngine;

// Smooths the cloth folds of a skinned part where it is weighted to some bones (e.g. the arms of her shirt):
// - shape: Taubin smoothing (no shrinking) on the vertices welded by position, blended by the weight on those bones
// - skinning: bone weights averaged with the neighbors there (+2 rings), so moving the arm no longer folds the
//   shoulder / elbow (folds that only showed while she aims, deals...)
// - smooth normals there.
// Same vertex count / order / UVs / bind poses: a drop-in replacement for the original mesh.
public static class MeshSmoother
{
    // A smoothed copy of the mesh where it is weighted to these bones.
    public static Mesh SmoothByBones(SkinnedMeshRenderer source, string[] boneNames, int iterations, int weightIterations = 10,
                                     float lambda = 0.5f, float mu = -0.53f, float weightGain = 1.4f)
    {
        Mesh mesh = Object.Instantiate(source.sharedMesh);
        Vector3[] v = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] tris = mesh.triangles;
        BoneWeight[] bw = mesh.boneWeights;
        Transform[] bones = source.bones;

        // the bones that count (by name: "mixamorig:LeftArm" ends with "LeftArm")
        var region = new HashSet<int>();
        for (int b = 0; b < bones.Length; b++)
            if (bones[b] != null)
                foreach (string n in boneNames)
                    if (bones[b].name == n || bones[b].name.EndsWith(":" + n)) region.Add(b);

        // weld split vertices (UV seams / hard edges) by position
        var groupOf = new int[v.Length];
        var groups = new List<List<int>>();
        var byPos = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e4f), Mathf.RoundToInt(v[i].y * 1e4f), Mathf.RoundToInt(v[i].z * 1e4f));
            if (!byPos.TryGetValue(key, out int g)) { g = groups.Count; byPos.Add(key, g); groups.Add(new List<int>()); }
            groupOf[i] = g;
            groups[g].Add(i);
        }
        int count = groups.Count;
        var p = new Vector3[count];
        var w = new float[count];
        for (int g = 0; g < count; g++)
        {
            int i = groups[g][0];
            p[g] = v[i];
            BoneWeight x = bw[i];
            float s = (region.Contains(x.boneIndex0) ? x.weight0 : 0f) + (region.Contains(x.boneIndex1) ? x.weight1 : 0f) +
                      (region.Contains(x.boneIndex2) ? x.weight2 : 0f) + (region.Contains(x.boneIndex3) ? x.weight3 : 0f);
            w[g] = Mathf.Clamp01(s * weightGain);
        }
        var neighbors = new HashSet<int>[count];
        for (int g = 0; g < count; g++) neighbors[g] = new HashSet<int>();
        for (int t = 0; t < tris.Length; t += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = groupOf[tris[t + k]], b = groupOf[tris[t + (k + 1) % 3]];
                if (a == b) continue;
                neighbors[a].Add(b);
                neighbors[b].Add(a);
            }

        SmoothWeights(bw, groupOf, groups, neighbors, w, bones.Length, weightIterations);
        mesh.boneWeights = bw;

        var next = new Vector3[count];
        for (int it = 0; it < iterations; it++)
            foreach (float f in new[] { lambda, mu })
            {
                for (int g = 0; g < count; g++)
                {
                    if (w[g] <= 0f || neighbors[g].Count == 0) { next[g] = p[g]; continue; }
                    Vector3 avg = Vector3.zero;
                    foreach (int n in neighbors[g]) avg += p[n];
                    avg /= neighbors[g].Count;
                    next[g] = p[g] + f * w[g] * (avg - p[g]);
                }
                (p, next) = (next, p);
            }

        // smooth normals where it was reshaped (area weighted, shared by the welded copies)
        var faceSum = new Vector3[count];
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = groupOf[tris[t]], b = groupOf[tris[t + 1]], c = groupOf[tris[t + 2]];
            Vector3 n = Vector3.Cross(p[b] - p[a], p[c] - p[a]);
            faceSum[a] += n; faceSum[b] += n; faceSum[c] += n;
        }
        for (int i = 0; i < v.Length; i++)
        {
            int g = groupOf[i];
            v[i] = p[g];
            if (w[g] > 0f && faceSum[g].sqrMagnitude > 1e-12f)
                normals[i] = Vector3.Slerp(normals[i], faceSum[g].normalized, Mathf.Clamp01(w[g] * 2f)).normalized;
        }
        mesh.vertices = v;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        mesh.name = source.sharedMesh.name + "_Smooth";
        return mesh;
    }

    // averages the bone weights of the reshaped area (and 2 rings around it) with the neighbors; 4 biggest kept
    private static void SmoothWeights(BoneWeight[] bw, int[] groupOf, List<List<int>> groups, HashSet<int>[] neighbors,
                                      float[] region, int boneCount, int iterations)
    {
        int count = groups.Count;
        var inArea = new bool[count];
        for (int g = 0; g < count; g++) inArea[g] = region[g] > 0f;
        for (int ring = 0; ring < 2; ring++)
        {
            var grown = (bool[])inArea.Clone();
            for (int g = 0; g < count; g++)
                if (inArea[g]) foreach (int n in neighbors[g]) grown[n] = true;
            inArea = grown;
        }

        var W = new float[count, boneCount];
        for (int g = 0; g < count; g++)
        {
            BoneWeight x = bw[groups[g][0]];
            W[g, x.boneIndex0] += x.weight0; W[g, x.boneIndex1] += x.weight1;
            W[g, x.boneIndex2] += x.weight2; W[g, x.boneIndex3] += x.weight3;
        }
        var next = new float[count, boneCount];
        for (int it = 0; it < iterations; it++)
        {
            for (int g = 0; g < count; g++)
            {
                if (!inArea[g] || neighbors[g].Count == 0)
                {
                    for (int b = 0; b < boneCount; b++) next[g, b] = W[g, b];
                    continue;
                }
                float k = 0.5f / neighbors[g].Count;
                for (int b = 0; b < boneCount; b++)
                {
                    float sum = 0f;
                    foreach (int n in neighbors[g]) sum += W[n, b];
                    next[g, b] = 0.5f * W[g, b] + k * sum;
                }
            }
            (W, next) = (next, W);
        }

        // back to 4 influences, normalized, on every welded copy
        var top = new int[4];
        var val = new float[4];
        for (int g = 0; g < count; g++)
        {
            if (!inArea[g]) continue;
            for (int s = 0; s < 4; s++) { top[s] = 0; val[s] = 0f; }
            for (int b = 0; b < boneCount; b++)
            {
                float x = W[g, b];
                if (x <= val[3]) continue;
                int s = 3;
                while (s > 0 && x > val[s - 1]) { val[s] = val[s - 1]; top[s] = top[s - 1]; s--; }
                val[s] = x; top[s] = b;
            }
            float total = val[0] + val[1] + val[2] + val[3];
            if (total <= 1e-6f) continue;
            var result = new BoneWeight
            {
                boneIndex0 = top[0], weight0 = val[0] / total,
                boneIndex1 = top[1], weight1 = val[1] / total,
                boneIndex2 = top[2], weight2 = val[2] / total,
                boneIndex3 = top[3], weight3 = val[3] / total,
            };
            foreach (int i in groups[g]) bw[i] = result;
        }
    }
}
