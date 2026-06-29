#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Alday.UnityAiConnector.Editor
{
    public static class UnityAiGameCreationTools
    {
        public static object CreateScript(JObject args)
        {
            var className = SanitizeClassName(args.Value<string>("className") ?? "NewBehaviour");
            var path = args.Value<string>("path");
            if (string.IsNullOrWhiteSpace(path))
                path = "Assets/Scripts/" + className + ".cs";
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Script path must be inside Assets/.");

            UnityAiEditorControlTools.EnsureAssetParentFolder(path);
            var content = args.Value<string>("content");
            if (string.IsNullOrWhiteSpace(content))
                content = ScriptTemplate(className, args.Value<string>("template") ?? "MonoBehaviour");

            File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
            return new { path, className, template = args.Value<string>("template") ?? "MonoBehaviour" };
        }

        public static object SetComponentProperty(JObject args)
        {
            var target = UnityAiTools.ResolveTarget(args);
            var component = ResolveComponent(target, args);
            var member = args.Value<string>("property") ?? args.Value<string>("field") ?? args.Value<string>("member");
            if (string.IsNullOrWhiteSpace(member))
                throw new ArgumentException("property, field, or member is required.");

            Undo.RecordObject(component, "Set Component Property via Unity AI Connector");
            SetMember(component, member, args["value"]);
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return DescribeComponent(component);
        }

        public static object AddRigidbody(JObject args)
        {
            var target = UnityAiTools.ResolveTarget(args);
            var body = target.GetComponent<Rigidbody>();
            if (body == null)
                body = target.AddComponent<Rigidbody>();
            ApplyRigidbody(body, args);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return DescribeComponent(body);
        }

        public static object SetRigidbody(JObject args)
        {
            var target = UnityAiTools.ResolveTarget(args);
            var body = target.GetComponent<Rigidbody>();
            if (body == null)
                throw new InvalidOperationException("Target does not have Rigidbody.");

            Undo.RecordObject(body, "Set Rigidbody via Unity AI Connector");
            ApplyRigidbody(body, args);
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return DescribeComponent(body);
        }

        public static object AddCollider(JObject args)
        {
            var target = UnityAiTools.ResolveTarget(args);
            var collider = target.GetComponent<Collider>();
            if (collider == null)
                collider = (Collider)target.AddComponent(ColliderType(args));
            ApplyCollider(collider, args);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return DescribeComponent(collider);
        }

        public static object SetCollider(JObject args)
        {
            var target = UnityAiTools.ResolveTarget(args);
            var collider = target.GetComponent<Collider>();
            if (collider == null)
                throw new InvalidOperationException("Target does not have Collider.");

            Undo.RecordObject(collider, "Set Collider via Unity AI Connector");
            ApplyCollider(collider, args);
            EditorUtility.SetDirty(collider);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return DescribeComponent(collider);
        }

        public static object SetBuildSettings(JObject args)
        {
            var scenePaths = args["scenes"]?.ToObject<string[]>() ?? Array.Empty<string>();
            if (scenePaths.Length == 0)
                throw new ArgumentException("scenes is required.");

            EditorBuildSettings.scenes = scenePaths
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
            AssetDatabase.SaveAssets();
            return new
            {
                scenes = EditorBuildSettings.scenes.Select(scene => new { scene.path, scene.enabled }).ToArray()
            };
        }

        public static object StartPlaymode()
        {
            EditorApplication.isPlaying = true;
            return new { requested = true, isPlaying = EditorApplication.isPlaying, isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode };
        }

        public static object StopPlaymode()
        {
            EditorApplication.isPlaying = false;
            return new { requested = true, isPlaying = EditorApplication.isPlaying, isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode };
        }

        public static object ClearConsole()
        {
            var logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor");
            logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(null, null);
            return new { cleared = true };
        }

        public static object ReadConsole(JObject args)
        {
            var limit = args.Value<int?>("limit") ?? 50;
            var entries = new List<object>();
            var logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor");
            var logEntry = Type.GetType("UnityEditor.LogEntry,UnityEditor");
            if (logEntries == null || logEntry == null)
                return new { supported = false, entries };

            var getCount = logEntries.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var getEntry = logEntries.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getCount == null || getEntry == null)
                return new { supported = false, entries };

            var count = (int)getCount.Invoke(null, null);
            var start = Math.Max(0, count - limit);
            for (var i = start; i < count; i++)
            {
                var entry = Activator.CreateInstance(logEntry);
                getEntry.Invoke(null, new[] { (object)i, entry });
                entries.Add(new
                {
                    index = i,
                    condition = ReadField<string>(logEntry, entry, "condition"),
                    stackTrace = ReadField<string>(logEntry, entry, "stackTrace"),
                    file = ReadField<string>(logEntry, entry, "file"),
                    line = ReadField<int>(logEntry, entry, "line"),
                    mode = ReadField<int>(logEntry, entry, "mode")
                });
            }

            return new { supported = true, count, entries };
        }

        public static object CaptureScreenshot(JObject args)
        {
            var path = args.Value<string>("path") ?? "Temp/UnityAiConnector/screenshot.png";
            var width = args.Value<int?>("width") ?? 1280;
            var height = args.Value<int?>("height") ?? 720;
            var camera = ResolveCamera(args);
            var absolutePath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);

            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            if (path.StartsWith("Assets/", StringComparison.Ordinal))
                AssetDatabase.Refresh();
            return new { path, absolutePath, width, height, camera = UnityAiTools.GetPath(camera.gameObject) };
        }

        public static object CreateLight(JObject args)
        {
            var name = args.Value<string>("name") ?? "Light";
            var go = new GameObject(name, typeof(Light));
            UnityAiEditorControlTools.ApplyParent(go, args);
            UnityAiEditorControlTools.ApplyTransform(go, args);
            ApplyLight(go.GetComponent<Light>(), args);
            Undo.RegisterCreatedObjectUndo(go, "Create Light via Unity AI Connector");
            EditorSceneManager.MarkSceneDirty(go.scene);
            return DescribeGameObject(go);
        }

        public static object SetLight(JObject args)
        {
            var target = UnityAiTools.ResolveTarget(args);
            var light = target.GetComponent<Light>();
            if (light == null)
                throw new InvalidOperationException("Target does not have Light.");

            Undo.RecordObject(light, "Set Light via Unity AI Connector");
            ApplyLight(light, args);
            EditorUtility.SetDirty(light);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return DescribeComponent(light);
        }

        public static object AddAudioSource(JObject args)
        {
            var target = UnityAiTools.ResolveTarget(args);
            var source = target.GetComponent<AudioSource>();
            if (source == null)
                source = target.AddComponent<AudioSource>();
            ApplyAudioSource(source, args);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return DescribeComponent(source);
        }

        public static object SetAudioSource(JObject args)
        {
            var target = UnityAiTools.ResolveTarget(args);
            var source = target.GetComponent<AudioSource>();
            if (source == null)
                throw new InvalidOperationException("Target does not have AudioSource.");

            Undo.RecordObject(source, "Set AudioSource via Unity AI Connector");
            ApplyAudioSource(source, args);
            EditorUtility.SetDirty(source);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return DescribeComponent(source);
        }

        public static object CreateVirtualButton(JObject args)
        {
            var canvas = EnsureCanvas(args.Value<string>("parentPath"));
            var button = CreateUiButton(canvas.transform, args.Value<string>("name") ?? "Virtual Button", args.Value<string>("text") ?? "ACTION", args);
            return DescribeGameObject(button);
        }

        public static object CreateJoystick(JObject args)
        {
            var canvas = EnsureCanvas(args.Value<string>("parentPath"));
            var root = CreateJoystickObject(canvas.transform, args);
            return DescribeGameObject(root);
        }

        public static object CreateMobileControls(JObject args)
        {
            var canvas = EnsureCanvas(args.Value<string>("parentPath"));
            var joystick = CreateJoystickObject(canvas.transform, new JObject
            {
                ["name"] = args.Value<string>("joystickName") ?? "Move Joystick",
                ["anchoredPosition"] = new JArray(120, 110)
            });

            var jump = CreateUiButton(canvas.transform, args.Value<string>("jumpName") ?? "Jump Button", args.Value<string>("jumpText") ?? "JUMP", new JObject
            {
                ["anchoredPosition"] = new JArray(-120, 112),
                ["sizeDelta"] = new JArray(132, 62),
                ["anchorMin"] = new JArray(1, 0),
                ["anchorMax"] = new JArray(1, 0),
                ["pivot"] = new JArray(0.5f, 0.5f)
            });
            var action = CreateUiButton(canvas.transform, args.Value<string>("actionName") ?? "Action Button", args.Value<string>("actionText") ?? "ACTION", new JObject
            {
                ["anchoredPosition"] = new JArray(-268, 112),
                ["sizeDelta"] = new JArray(146, 62),
                ["anchorMin"] = new JArray(1, 0),
                ["anchorMax"] = new JArray(1, 0),
                ["pivot"] = new JArray(0.5f, 0.5f)
            });

            return new { canvas = UnityAiTools.GetPath(canvas), joystick = UnityAiTools.GetPath(joystick), jump = UnityAiTools.GetPath(jump), action = UnityAiTools.GetPath(action) };
        }

        public static object CreatePrefabChild(JObject args)
        {
            return EditPrefab(args, root =>
            {
                var parent = FindPrefabChild(root, args.Value<string>("parentPath")) ?? root;
                var name = args.Value<string>("name") ?? "New Child";
                var primitive = args.Value<string>("primitive");
                var child = string.IsNullOrWhiteSpace(primitive)
                    ? new GameObject(name)
                    : GameObject.CreatePrimitive((PrimitiveType)Enum.Parse(typeof(PrimitiveType), primitive, true));
                child.name = name;
                child.transform.SetParent(parent.transform, false);
                UnityAiEditorControlTools.ApplyTransform(child, args);
                return DescribeGameObject(child);
            });
        }

        public static object DeletePrefabChild(JObject args)
        {
            return EditPrefab(args, root =>
            {
                var child = FindPrefabChild(root, UnityAiEditorControlTools.RequiredString(args, "childPath"));
                if (child == null || child == root)
                    throw new InvalidOperationException("Prefab child not found or root cannot be deleted.");
                var path = UnityAiTools.GetPath(child);
                UnityEngine.Object.DestroyImmediate(child);
                return new { deleted = true, path };
            });
        }

        public static object AddPrefabComponent(JObject args)
        {
            return EditPrefab(args, root =>
            {
                var target = FindPrefabChild(root, args.Value<string>("childPath")) ?? root;
                var type = UnityAiEditorControlTools.FindType(UnityAiEditorControlTools.RequiredString(args, "type"));
                if (!typeof(Component).IsAssignableFrom(type))
                    throw new InvalidOperationException(type.FullName + " is not a Component.");
                var component = target.GetComponent(type);
                if (component == null)
                    component = target.AddComponent(type);
                return DescribeComponent(component);
            });
        }

        public static object SetPrefabComponentProperty(JObject args)
        {
            return EditPrefab(args, root =>
            {
                var target = FindPrefabChild(root, args.Value<string>("childPath")) ?? root;
                var component = ResolveComponent(target, args);
                var member = args.Value<string>("property") ?? args.Value<string>("field") ?? args.Value<string>("member");
                if (string.IsNullOrWhiteSpace(member))
                    throw new ArgumentException("property, field, or member is required.");
                SetMember(component, member, args["value"]);
                return DescribeComponent(component);
            });
        }

        static object EditPrefab(JObject args, Func<GameObject, object> edit)
        {
            var prefabPath = UnityAiEditorControlTools.RequiredString(args, "prefabPath");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var result = edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                return new { prefabPath, result };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ApplyRigidbody(Rigidbody body, JObject args)
        {
            foreach (var name in new[] { "mass", "drag", "angularDrag", "linearDamping", "angularDamping", "useGravity", "isKinematic" })
            {
                if (args[name] != null)
                    SetMember(body, name, args[name]);
            }
            if (args["constraints"] != null)
                body.constraints = (RigidbodyConstraints)Enum.Parse(typeof(RigidbodyConstraints), args.Value<string>("constraints"), true);
            if (args["collisionDetectionMode"] != null)
                body.collisionDetectionMode = (CollisionDetectionMode)Enum.Parse(typeof(CollisionDetectionMode), args.Value<string>("collisionDetectionMode"), true);
        }

        static void ApplyCollider(Collider collider, JObject args)
        {
            if (args["isTrigger"] != null)
                collider.isTrigger = args.Value<bool>("isTrigger");
            if (collider is BoxCollider box)
            {
                if (args["center"] != null)
                    box.center = ReadVector3(args["center"]);
                if (args["size"] != null)
                    box.size = ReadVector3(args["size"]);
            }
            if (collider is SphereCollider sphere)
            {
                if (args["center"] != null)
                    sphere.center = ReadVector3(args["center"]);
                if (args["radius"] != null)
                    sphere.radius = args.Value<float>("radius");
            }
            if (collider is CapsuleCollider capsule)
            {
                if (args["center"] != null)
                    capsule.center = ReadVector3(args["center"]);
                if (args["radius"] != null)
                    capsule.radius = args.Value<float>("radius");
                if (args["height"] != null)
                    capsule.height = args.Value<float>("height");
            }
        }

        static Type ColliderType(JObject args)
        {
            var type = (args.Value<string>("collider") ?? args.Value<string>("type") ?? "Box").ToLowerInvariant();
            return type switch
            {
                "sphere" or "spherecollider" => typeof(SphereCollider),
                "capsule" or "capsulecollider" => typeof(CapsuleCollider),
                "mesh" or "meshcollider" => typeof(MeshCollider),
                _ => typeof(BoxCollider)
            };
        }

        static void ApplyLight(Light light, JObject args)
        {
            if (args["lightType"] != null || args["type"] != null)
                light.type = (LightType)Enum.Parse(typeof(LightType), args.Value<string>("lightType") ?? args.Value<string>("type"), true);
            if (args["intensity"] != null)
                light.intensity = args.Value<float>("intensity");
            if (args["range"] != null)
                light.range = args.Value<float>("range");
            if (args["spotAngle"] != null)
                light.spotAngle = args.Value<float>("spotAngle");
            UnityAiEditorControlTools.ApplyColor(args["color"], value => light.color = value);
            UnityAiEditorControlTools.ApplyColor(args["ambientColor"], value => RenderSettings.ambientLight = value);
        }

        static void ApplyAudioSource(AudioSource source, JObject args)
        {
            if (args["clipPath"] != null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(args.Value<string>("clipPath"));
                if (clip == null)
                    throw new InvalidOperationException("AudioClip not found: " + args.Value<string>("clipPath"));
                source.clip = clip;
            }
            foreach (var name in new[] { "playOnAwake", "loop", "volume", "pitch", "spatialBlend", "minDistance", "maxDistance" })
            {
                if (args[name] != null)
                    SetMember(source, name, args[name]);
            }
        }

        static GameObject EnsureCanvas(string parentPath)
        {
            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                var existing = UnityAiTools.FindByPath(parentPath);
                if (existing == null)
                    throw new InvalidOperationException("Canvas not found: " + parentPath);
                return existing;
            }

            var canvas = UnityAiTools.AllSceneObjects().FirstOrDefault(go => go.GetComponent<Canvas>() != null);
            if (canvas != null)
                return canvas;

            canvas = new GameObject("Mobile Controls Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(800, 450);
            UnityAiEditorControlTools.EnsureEventSystem();
            Undo.RegisterCreatedObjectUndo(canvas, "Create Canvas via Unity AI Connector");
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            return canvas;
        }

        static GameObject CreateUiButton(Transform parent, string name, string textValue, JObject args)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = UnityAiEditorControlTools.ReadVector2(args["sizeDelta"], new Vector2(148, 58));
            rect.anchoredPosition = UnityAiEditorControlTools.ReadVector2(args["anchoredPosition"], Vector2.zero);
            rect.anchorMin = UnityAiEditorControlTools.ReadVector2(args["anchorMin"], new Vector2(0.5f, 0.5f));
            rect.anchorMax = UnityAiEditorControlTools.ReadVector2(args["anchorMax"], new Vector2(0.5f, 0.5f));
            rect.pivot = UnityAiEditorControlTools.ReadVector2(args["pivot"], new Vector2(0.5f, 0.5f));
            go.GetComponent<Image>().color = new Color(0.12f, 0.32f, 0.78f, 0.82f);
            UnityAiEditorControlTools.ApplyColor(args["backgroundColor"], value => go.GetComponent<Image>().color = value);

            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.text = textValue;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = args.Value<int?>("fontSize") ?? 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            UnityAiEditorControlTools.ApplyColor(args["textColor"], value => text.color = value);

            Undo.RegisterCreatedObjectUndo(go, "Create Virtual Button via Unity AI Connector");
            EditorSceneManager.MarkSceneDirty(go.scene);
            return go;
        }

        static GameObject CreateJoystickObject(Transform parent, JObject args)
        {
            var root = new GameObject(args.Value<string>("name") ?? "Virtual Joystick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = UnityAiEditorControlTools.ReadVector2(args["sizeDelta"], new Vector2(140, 140));
            rect.anchoredPosition = UnityAiEditorControlTools.ReadVector2(args["anchoredPosition"], new Vector2(120, 110));
            rect.anchorMin = UnityAiEditorControlTools.ReadVector2(args["anchorMin"], new Vector2(0, 0));
            rect.anchorMax = UnityAiEditorControlTools.ReadVector2(args["anchorMax"], new Vector2(0, 0));
            rect.pivot = UnityAiEditorControlTools.ReadVector2(args["pivot"], new Vector2(0.5f, 0.5f));
            root.GetComponent<Image>().color = new Color(1, 1, 1, 0.16f);

            var knob = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            knob.transform.SetParent(root.transform, false);
            var knobRect = knob.GetComponent<RectTransform>();
            knobRect.sizeDelta = new Vector2(58, 58);
            knobRect.anchorMin = knobRect.anchorMax = knobRect.pivot = new Vector2(0.5f, 0.5f);
            knob.GetComponent<Image>().color = new Color(1, 1, 1, 0.38f);

            Undo.RegisterCreatedObjectUndo(root, "Create Joystick via Unity AI Connector");
            EditorSceneManager.MarkSceneDirty(root.scene);
            return root;
        }

        static Camera ResolveCamera(JObject args)
        {
            var path = args.Value<string>("cameraPath") ?? args.Value<string>("path");
            if (!string.IsNullOrWhiteSpace(path))
            {
                var target = UnityAiTools.FindByPath(path);
                var camera = target == null ? null : target.GetComponent<Camera>();
                if (camera != null)
                    return camera;
            }

            if (Camera.main != null)
                return Camera.main;

            var found = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).FirstOrDefault();
            if (found == null)
                throw new InvalidOperationException("No Camera found.");
            return found;
        }

        static Component ResolveComponent(GameObject target, JObject args)
        {
            var typeName = UnityAiEditorControlTools.RequiredString(args, "type");
            var component = target.GetComponent(UnityAiEditorControlTools.FindType(typeName));
            if (component == null)
                throw new InvalidOperationException("Component not found: " + typeName);
            return component;
        }

        static void SetMember(object target, string memberName, JToken value)
        {
            var type = target.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, UnityAiEditorControlTools.ConvertToken(value, property.PropertyType));
                return;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, UnityAiEditorControlTools.ConvertToken(value, field.FieldType));
                return;
            }

            throw new InvalidOperationException("Writable public member not found: " + type.FullName + "." + memberName);
        }

        static T ReadField<T>(Type type, object target, string name)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return default;
            return (T)field.GetValue(target);
        }

        static GameObject FindPrefabChild(GameObject root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == root.name)
                return root;
            var trimmed = path.StartsWith(root.name + "/", StringComparison.Ordinal) ? path.Substring(root.name.Length + 1) : path;
            var current = root.transform;
            foreach (var part in trimmed.Split('/'))
            {
                current = current.Find(part);
                if (current == null)
                    return null;
            }
            return current.gameObject;
        }

        static string SanitizeClassName(string value)
        {
            value = Regex.Replace(value, "[^A-Za-z0-9_]", "");
            if (string.IsNullOrWhiteSpace(value))
                value = "NewBehaviour";
            if (char.IsDigit(value[0]))
                value = "_" + value;
            return value;
        }

        static string ScriptTemplate(string className, string template)
        {
            if (template.Equals("PlayerController", StringComparison.OrdinalIgnoreCase))
            {
                return $@"using UnityEngine;

public class {className} : MonoBehaviour
{{
    public float moveSpeed = 6f;
    public float jumpForce = 7f;

    Rigidbody body;

    void Awake()
    {{
        body = GetComponent<Rigidbody>();
    }}

    void Update()
    {{
        var horizontal = Input.GetAxisRaw(""Horizontal"");
        var vertical = Input.GetAxisRaw(""Vertical"");
        var movement = new Vector3(horizontal, 0f, vertical).normalized * moveSpeed;

        if (body != null)
            body.linearVelocity = new Vector3(movement.x, body.linearVelocity.y, movement.z);
        else
            transform.position += movement * Time.deltaTime;

        if (Input.GetButtonDown(""Jump"") && body != null)
            body.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }}
}}
";
            }

            if (template.Equals("GameManager", StringComparison.OrdinalIgnoreCase))
            {
                return $@"using UnityEngine;
using UnityEngine.SceneManagement;

public class {className} : MonoBehaviour
{{
    public int score;

    public void AddScore(int amount)
    {{
        score += amount;
    }}

    public void Restart()
    {{
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }}
}}
";
            }

            return $@"using UnityEngine;

public class {className} : MonoBehaviour
{{
    void Start()
    {{
    }}

    void Update()
    {{
    }}
}}
";
        }

        static Vector3 ReadVector3(JToken token)
        {
            var values = token.ToObject<float[]>();
            if (values == null || values.Length != 3)
                throw new ArgumentException("Vector3 values must be [x, y, z].");
            return new Vector3(values[0], values[1], values[2]);
        }

        static object DescribeGameObject(GameObject go)
        {
            return new
            {
                go.name,
                path = UnityAiTools.GetPath(go),
                id = UnityAiTools.GetObjectId(go),
                scene = go.scene.name,
                position = UnityAiTools.ToArray(go.transform.localPosition),
                rotation = UnityAiTools.ToArray(go.transform.localEulerAngles),
                scale = UnityAiTools.ToArray(go.transform.localScale)
            };
        }

        static object DescribeComponent(Component component)
        {
            return new
            {
                type = component.GetType().FullName,
                gameObjectPath = UnityAiTools.GetPath(component.gameObject),
                id = UnityAiTools.GetObjectId(component)
            };
        }
    }
}
#endif
