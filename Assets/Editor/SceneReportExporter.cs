using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Last Terminal/15: a report of the open scene, to send to Claude so he understands a scene made by hand.
// Writes <project>/SceneReports/<scene>/ (outside Assets: nothing gets imported) and zips it:
//   report.md     - summary: lighting, the notes (DesignNote), every game script with its settings and links,
//                   cameras, lights, missing scripts, the legend of the numbered top view
//   hierarchy.txt - every object: world position / rotation, size, components (pieces inside imported models summed up)
//   top.png       - seen from above, cut at 2.2 m over the floor (no ceiling), the scripts / notes numbered
//   eyes_*.png    - from the player's eyes (or the middle of the scene), looking N / E / S / W
//   cam_*.png     - through each camera of the scene;  sceneview.png - what the Scene view shows now
// The scene isn't changed (everything temporary is removed, lighting put back).
internal static class SceneReportExporter
{
    // Limits: labels on the map picture, camera shots, plan cut height (m).
    private const int MaxLabels = 70;
    private const int MaxCameras = 10;
    private const float CutHeight = 2.2f;

    // Types listed without their fields.
    private static readonly HashSet<Type> PlainTypes = new HashSet<Type>
        { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SkinnedMeshRenderer), typeof(RectTransform) };

    // Menu 15: writes the report folder and zips it.
    [MenuItem("Last Terminal/15 - Export Scene Report (for Claude)")]
    public static void Export()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path)) { Debug.LogError("[Report] Save the scene first."); return; }
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string dir = Path.Combine(root, "SceneReports", scene.name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        var all = new List<GameObject>();
        foreach (GameObject r in scene.GetRootGameObjects()) Collect(r, all);

        var labeled = new List<GameObject>();
        foreach (GameObject go in all)
            if (labeled.Count < MaxLabels && (go.GetComponent<DesignNote>() != null || OurScripts(go).Any())) labeled.Add(go);

        File.WriteAllText(Path.Combine(dir, "hierarchy.txt"), Hierarchy(scene), Encoding.UTF8);
        List<string> images = Pictures(scene, dir, all, labeled);
        File.WriteAllText(Path.Combine(dir, "report.md"), Report(scene, all, labeled, images), Encoding.UTF8);

        string zip = dir + ".zip";
        bool zipped = Zip(dir, zip);
        EditorUtility.RevealInFinder(zipped ? zip : dir);
        Debug.Log($"[Report] {(zipped ? zip : dir)} ready: send it to Claude. ({all.Count} objects, {images.Count} pictures)");
    }

    // ================================================================== reading the scene
    private static void Collect(GameObject go, List<GameObject> into)
    {
        into.Add(go);
        foreach (Transform c in go.transform) Collect(c.gameObject, into);
    }

    // Our own components on an object (not Unity's).
    private static IEnumerable<MonoBehaviour> OurScripts(GameObject go) =>
        go.GetComponents<MonoBehaviour>().Where(m => m != null && m.GetType().Assembly.GetName().Name.StartsWith("Assembly-CSharp") && !(m is DesignNote));

    // Hierarchy path of a transform.
    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    // A vector as short text.
    private static string V(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

    // World bounds of an object's renderers.
    private static bool WorldBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    // inside an imported model (prefab instance) and nothing but meshes down there: summed up in its parent
    private static bool Plain(GameObject go)
    {
        foreach (Component c in go.GetComponentsInChildren<Component>(true))
            if (c != null && !PlainTypes.Contains(c.GetType())) return false;
        return true;
    }

    // Is the object a part of an imported model?
    private static bool InsideModel(GameObject go) =>
        PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetOutermostPrefabInstanceRoot(go) != go;

    // ================================================================== hierarchy.txt
    private static string Hierarchy(Scene scene)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {scene.name} ({scene.path}) - positions/rotations are WORLD, size = renderers' bounds (m)");
        sb.AppendLine("# [x] = inactive.  ~N = N mesh pieces of an imported model summed up");
        foreach (GameObject r in scene.GetRootGameObjects()) Line(sb, r, 0);
        return sb.ToString();
    }

    // One hierarchy line with its components.
    private static void Line(StringBuilder sb, GameObject go, int depth)
    {
        Transform t = go.transform;
        string size = WorldBounds(go, out Bounds b) ? $" size{V(b.size)}" : "";
        string src = PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go ? $" <{AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go))}>" : "";
        var comps = new List<string>();
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c == null) { comps.Add("MISSING SCRIPT"); continue; }
            if (c is Transform) continue;
            comps.Add(Describe(c, false));
        }
        sb.Append(new string(' ', depth * 2)).Append(go.activeSelf ? "" : "[x] ").Append(go.name)
          .Append($" pos{V(t.position)} rot{V(t.eulerAngles)}")
          .Append(t.lossyScale != Vector3.one ? $" scale{V(t.lossyScale)}" : "").Append(size).Append(src);
        if (comps.Count > 0) sb.Append(" | ").Append(string.Join("; ", comps));
        sb.AppendLine();

        int summed = 0;
        foreach (Transform c in t)
        {
            if (InsideModel(c.gameObject) && Plain(c.gameObject)) { summed++; continue; }
            Line(sb, c.gameObject, depth + 1);
        }
        if (summed > 0) sb.Append(new string(' ', (depth + 1) * 2)).AppendLine($"~{summed} mesh pieces");
    }

    // A component and its serialized values.
    private static string Describe(Component c, bool full)
    {
        switch (c)
        {
            case MeshFilter mf: return $"Mesh {(mf.sharedMesh != null ? mf.sharedMesh.name : "none")}";
            case Renderer r: return $"{c.GetType().Name} [{string.Join(", ", r.sharedMaterials.Select(m => m != null ? m.name : "none"))}]";
            case BoxCollider bc: return $"BoxCollider{(bc.isTrigger ? "(trigger)" : "")} center{V(bc.center)} size{V(bc.size)}";
            case Collider col: return $"{c.GetType().Name}{(col.isTrigger ? "(trigger)" : "")}";
            case Light l: return $"Light {l.type} color#{ColorUtility.ToHtmlStringRGB(l.color)} intensity {l.intensity:0.##} range {l.range:0.#} shadows {l.shadows}";
            case Camera cam: return $"Camera fov {cam.fieldOfView:0} {(cam.enabled ? "" : "(off)")}";
            case AudioSource a: return $"AudioSource {(a.clip != null ? a.clip.name : "no clip")}{(a.loop ? " loop" : "")}";
            case Animator an: return $"Animator {(an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "no controller")}";
            case MonoBehaviour mb when mb.GetType().Assembly.GetName().Name.StartsWith("Assembly-CSharp"):
                return full ? $"{mb.GetType().Name}\n{Fields(mb, "    ")}" : mb.GetType().Name + (mb.enabled ? "" : "(off)");
            default: return c.GetType().Name;
        }
    }

    // the serialized fields of one of our scripts: values, links to objects of the scene / assets
    private static string Fields(Object target, string indent)
    {
        var sb = new StringBuilder();
        var so = new SerializedObject(target);
        SerializedProperty it = so.GetIterator();
        if (!it.NextVisible(true)) return "";
        do
        {
            if (it.name == "m_Script") continue;
            sb.Append(indent).Append(it.name).Append(" = ").AppendLine(Value(it, 0));
        } while (it.NextVisible(false));
        return sb.ToString();
    }

    // A serialized value as text.
    private static string Value(SerializedProperty p, int depth)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue ? "true" : "false";
            case SerializedPropertyType.Float: return p.floatValue.ToString("0.###");
            case SerializedPropertyType.String: return "\"" + p.stringValue.Replace("\n", "\\n") + "\"";
            case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString();
            case SerializedPropertyType.Color: return "#" + ColorUtility.ToHtmlStringRGBA(p.colorValue);
            case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
            case SerializedPropertyType.Vector3: return V(p.vector3Value);
            case SerializedPropertyType.Vector4: return p.vector4Value.ToString();
            case SerializedPropertyType.ObjectReference: return Ref(p.objectReferenceValue);
            case SerializedPropertyType.ManagedReference: return p.managedReferenceValue != null ? $"<{p.managedReferenceValue.GetType().Name}>" : "null";
        }
        if (p.isArray && p.propertyType == SerializedPropertyType.Generic)
        {
            int n = p.arraySize;
            var items = new List<string>();
            for (int i = 0; i < Mathf.Min(n, 12); i++) items.Add(Value(p.GetArrayElementAtIndex(i), depth + 1));
            return $"[{n}] " + string.Join(", ", items) + (n > 12 ? ", ..." : "");
        }
        if (p.hasVisibleChildren && depth < 2)
        {
            var parts = new List<string>();
            SerializedProperty c = p.Copy(), end = p.GetEndProperty();
            if (c.NextVisible(true))
                do { if (SerializedProperty.EqualContents(c, end)) break; parts.Add($"{c.name}={Value(c, depth + 1)}"); } while (c.NextVisible(false));
            return "{" + string.Join(", ", parts) + "}";
        }
        return p.propertyType.ToString();
    }

    // An object reference as text.
    private static string Ref(Object o)
    {
        if (o == null) return "null";
        string asset = AssetDatabase.GetAssetPath(o);
        if (!string.IsNullOrEmpty(asset)) return $"{o.GetType().Name} asset {asset}{(AssetDatabase.IsSubAsset(o) ? " (" + o.name + ")" : "")}";
        if (o is Component c) return $"{o.GetType().Name} on {PathOf(c.transform)}";
        if (o is GameObject g) return $"GameObject {PathOf(g.transform)}";
        return $"{o.GetType().Name} {o.name}";
    }

    // ================================================================== report.md
    private static string Report(Scene scene, List<GameObject> all, List<GameObject> labeled, List<string> images)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Scene report: {scene.name}");
        sb.AppendLine($"`{scene.path}` - {all.Count} objects, exported {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## Lighting");
        sb.AppendLine($"- ambient: {RenderSettings.ambientMode} #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight)}, skybox: {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}");
        sb.AppendLine($"- fog: {(RenderSettings.fog ? $"{RenderSettings.fogMode} #{ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor)} {RenderSettings.fogStartDistance:0}-{RenderSettings.fogEndDistance:0} m" : "off")}");
        if (WorldBoundsAll(all, out Bounds sb2)) sb.AppendLine($"- everything fits in: center {V(sb2.center)} size {V(sb2.size)}");
        sb.AppendLine();

        sb.AppendLine("## Notes (DesignNote)");
        bool anyNote = false;
        foreach (GameObject go in all)
        {
            var n = go.GetComponent<DesignNote>();
            if (n == null) continue;
            anyNote = true;
            sb.AppendLine($"- **{PathOf(go.transform)}** at {V(go.transform.position)}: {n.note.Replace("\n", " / ")}");
        }
        if (!anyNote) sb.AppendLine("(none: add a DesignNote component to the objects to say what they are for)");
        sb.AppendLine();

        sb.AppendLine("## Numbers on top.png");
        for (int i = 0; i < labeled.Count; i++)
            sb.AppendLine($"{i + 1}. {PathOf(labeled[i].transform)} {V(labeled[i].transform.position)}");
        sb.AppendLine();

        sb.AppendLine("## Game scripts (settings and links)");
        foreach (GameObject go in all)
            foreach (MonoBehaviour mb in OurScripts(go))
            {
                sb.AppendLine($"### {mb.GetType().Name} on {PathOf(go.transform)}{(go.activeInHierarchy && mb.enabled ? "" : " (off)")}");
                sb.AppendLine($"position {V(go.transform.position)} rotation {V(go.transform.eulerAngles)}");
                sb.AppendLine("```");
                sb.Append(Fields(mb, ""));
                sb.AppendLine("```");
            }
        sb.AppendLine();

        sb.AppendLine("## Cameras / lights");
        foreach (GameObject go in all)
        {
            foreach (Camera c in go.GetComponents<Camera>()) sb.AppendLine($"- {Describe(c, false)} - {PathOf(go.transform)} {V(go.transform.position)} {V(go.transform.eulerAngles)}");
            foreach (Light l in go.GetComponents<Light>()) sb.AppendLine($"- {Describe(l, false)} - {PathOf(go.transform)} {V(go.transform.position)}");
        }
        sb.AppendLine();

        var missingScripts = all.Where(g => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g) > 0).ToList();
        if (missingScripts.Count > 0)
        {
            sb.AppendLine("## Missing scripts");
            foreach (GameObject g in missingScripts) sb.AppendLine($"- {PathOf(g.transform)}");
            sb.AppendLine();
        }
        sb.AppendLine("## Pictures");
        foreach (string i in images) sb.AppendLine($"- {i}");
        return sb.ToString();
    }

    // World bounds of every object.
    private static bool WorldBoundsAll(List<GameObject> all, out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (GameObject go in all)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null || !r.enabled || !go.activeInHierarchy || r is ParticleSystemRenderer) continue;
            if (r.bounds.size.magnitude > 300f) continue;   // ground planes, sky backdrops: not the place itself
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    // ================================================================== pictures
    private static List<string> Pictures(Scene scene, string dir, List<GameObject> all, List<GameObject> labeled)
    {
        var made = new List<string>();
        if (!WorldBoundsAll(all, out Bounds bounds)) return made;
        var temp = new List<GameObject>();
        // even light so dark rooms read (put back after)
        AmbientMode ambMode = RenderSettings.ambientMode;
        Color amb = RenderSettings.ambientLight;
        bool fog = RenderSettings.fog;
        try
        {
            var camGo = new GameObject("__ReportCamera") { hideFlags = HideFlags.HideAndDontSave };
            temp.Add(camGo);
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

            // through the scene's own cameras, as lit
            int k = 0;
            foreach (GameObject go in all)
                foreach (Camera c in go.GetComponents<Camera>())
                {
                    if (k >= MaxCameras || c == cam) continue;
                    cam.CopyFrom(c);
                    cam.enabled = false;
                    Shoot(cam, Path.Combine(dir, $"cam_{++k:00}_{Safe(go.name)}.png"), 1280, 720, made);
                }
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
            {
                cam.orthographic = false;
                cam.fieldOfView = sv.camera.fieldOfView;
                cam.nearClipPlane = 0.03f; cam.farClipPlane = 1000f;
                cam.transform.SetPositionAndRotation(sv.camera.transform.position, sv.camera.transform.rotation);
                Shoot(cam, Path.Combine(dir, "sceneview.png"), 1280, 720, made);
            }

            // the plan views: flat light, no fog
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.62f);
            RenderSettings.fog = false;
            var lightGo = new GameObject("__ReportLight") { hideFlags = HideFlags.HideAndDontSave };
            temp.Add(lightGo);
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.7f;
            sun.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(70f, 30f, 0f);

            // top view, cut above the floor, numbered
            float floor = bounds.min.y;
            for (int i = 0; i < labeled.Count; i++)
            {
                var l = new GameObject("__ReportLabel") { hideFlags = HideFlags.HideAndDontSave };
                temp.Add(l);
                Vector3 p = labeled[i].transform.position;
                l.transform.SetPositionAndRotation(new Vector3(p.x, floor + CutHeight - 0.05f, p.z), Quaternion.Euler(90f, 0f, 0f));
                var t = l.AddComponent<TextMeshPro>();
                t.text = (i + 1).ToString();
                t.enableAutoSizing = true;
                t.fontSizeMin = 0.1f;
                t.fontSizeMax = 500f;
                t.alignment = TextAlignmentOptions.Center;
                t.color = labeled[i].GetComponent<DesignNote>() != null ? new Color(1f, 0.85f, 0.1f) : new Color(0.2f, 1f, 1f);
                t.fontStyle = FontStyles.Bold;
                float tag = Mathf.Max(bounds.size.x, bounds.size.z) * 0.045f;   // number size: ~4.5% of the picture
                t.rectTransform.sizeDelta = new Vector2(tag * 1.6f, tag);
            }
            float half = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.08f;
            cam.orthographic = true;
            cam.orthographicSize = half;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = CutHeight + 5f;
            cam.transform.SetPositionAndRotation(new Vector3(bounds.center.x, floor + CutHeight, bounds.center.z), Quaternion.Euler(90f, 0f, 0f));
            Shoot(cam, Path.Combine(dir, "top.png"), 1600, 1600, made);
            foreach (GameObject g in temp.Where(g => g != null && g.name == "__ReportLabel").ToList()) { Object.DestroyImmediate(g); }
            temp.RemoveAll(g => g == null);

            // from his eyes (the first person player, else the middle of the place): N / E / S / W
            Vector3 eye = new Vector3(bounds.center.x, floor + 1.6f, bounds.center.z);
            foreach (GameObject go in all)
                foreach (MonoBehaviour mb in OurScripts(go))
                    if (mb.GetType().Name == "FirstPersonPlayer") eye = go.transform.position + Vector3.up * 1.6f;
            cam.orthographic = false;
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            foreach (var (n, yaw) in new[] { ("N", 0f), ("E", 90f), ("S", 180f), ("W", 270f) })
            {
                cam.transform.SetPositionAndRotation(eye, Quaternion.Euler(8f, yaw, 0f));
                Shoot(cam, Path.Combine(dir, $"eyes_{n}.png"), 1280, 720, made);
            }
        }
        finally
        {
            foreach (GameObject g in temp) if (g != null) Object.DestroyImmediate(g);
            RenderSettings.ambientMode = ambMode;
            RenderSettings.ambientLight = amb;
            RenderSettings.fog = fog;
        }
        return made;
    }

    // Renders a camera to a PNG file.
    private static void Shoot(Camera cam, string file, int w, int h, List<string> made)
    {
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        try
        {
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            made.Add(Path.GetFileName(file));
        }
        catch (Exception e) { Debug.LogWarning($"[Report] picture {Path.GetFileName(file)} failed: {e.Message}"); }
        finally { RenderTexture.ReleaseTemporary(rt); }
    }

    // A string safe for a file name.
    private static string Safe(string s) => string.Concat(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));

    // System.IO.Compression.ZipFile when the scripting profile has it (else the folder is sent as it is)
    private static bool Zip(string dir, string zip)
    {
        try
        {
            if (File.Exists(zip)) File.Delete(zip);
            Type zf = Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.ZipFile")
                      ?? Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.FileSystem");
            var create = zf?.GetMethod("CreateFromDirectory", new[] { typeof(string), typeof(string) });
            if (create == null) return false;
            create.Invoke(null, new object[] { dir, zip });
            return File.Exists(zip);
        }
        catch (Exception e) { Debug.LogWarning($"[Report] zip failed ({e.Message}): send the folder."); return false; }
    }
}
