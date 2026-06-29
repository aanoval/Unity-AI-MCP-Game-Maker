#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alday.UnityAiGameMaker.Editor
{
    [InitializeOnLoad]
    public static class UnityAiScreenshotPlayModeBatch
    {
        const string StateFileName = "playmode-batch-state.json";
        const double StepTimeoutSeconds = 90d;

        sealed class SceneCaptureJob
        {
            public string scenePath = "";
            public string outputPath = "";
            public int width = 1080;
            public int height = 1920;
            public int waitFrames = 30;
            public string cameraPath = "";
        }

        sealed class BatchState
        {
            public bool active;
            public string outputPath = "";
            public int sceneIndex;
            public string step = "starting";
            public int settleRemaining;
            public List<SceneCaptureJob> scenes = new List<SceneCaptureJob>();
            public List<object> captures = new List<object>();
            public double lastProgressTime;
        }

        const string StepAwaitPlay = "await_play";
        const string StepAwaitSettle = "await_settle";
        const string StepCaptured = "captured";

        static BatchState state;
        static bool handlersRegistered;
        static double watchdogLastCheck;

        static string StateFilePath => Path.Combine(
            UnityAiGameMakerConfig.ProjectRoot,
            "Temp",
            "UnityAiGameMaker",
            StateFileName);

        static UnityAiScreenshotPlayModeBatch()
        {
            EditorApplication.delayCall += TryResumeFromDisk;
        }

        [MenuItem("Tools/Unity AI Game Maker/Run Play Mode Screenshot Batch")]
        public static void RunFromMenu()
        {
            var inputPath = EditorUtility.OpenFilePanel(
                "Unity AI Play Mode Screenshot Batch",
                UnityAiGameMakerConfig.ProjectRoot,
                "json");
            if (string.IsNullOrWhiteSpace(inputPath))
                return;

            Begin(inputPath, inputPath + ".out.json");
        }

        public static void RunFromEnvironment()
        {
            var inputPath = Environment.GetEnvironmentVariable("UNITY_AI_GAME_MAKER_BATCH_FILE")
                ?? Environment.GetEnvironmentVariable("UNITY_AI_CONNECTOR_BATCH_FILE");
            var outPath = Environment.GetEnvironmentVariable("UNITY_AI_GAME_MAKER_BATCH_OUT")
                ?? Environment.GetEnvironmentVariable("UNITY_AI_CONNECTOR_BATCH_OUT");

            if (string.IsNullOrWhiteSpace(inputPath))
                throw new InvalidOperationException("UNITY_AI_GAME_MAKER_BATCH_FILE is required.");

            if (string.IsNullOrWhiteSpace(outPath))
                outPath = inputPath + ".out.json";

            Begin(inputPath, outPath);
        }

        public static void BeginCaptureScenes(JObject args, string outFile)
        {
            var outputDir = args.Value<string>("outputDir") ?? args.Value<string>("output")
                ?? Path.GetFullPath(Path.Combine(UnityAiGameMakerConfig.ProjectRoot, "..", "screenshots"));
            var width = args.Value<int?>("width") ?? 1080;
            var height = args.Value<int?>("height") ?? 1920;
            var waitFrames = args.Value<int?>("waitFrames") ?? 30;
            var cameraPath = args.Value<string>("cameraPath") ?? "";
            var scenePaths = args["scenes"]?.ToObject<string[]>();
            if (scenePaths == null || scenePaths.Length == 0)
            {
                var filter = args.Value<string>("filter") ?? "menu";
                scenePaths = ResolveScenePathsFromFilter(filter);
            }

            var payload = new JObject
            {
                ["commands"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "screenshots.captureScenes",
                        ["args"] = new JObject
                        {
                            ["scenes"] = new JArray(scenePaths),
                            ["outputDir"] = outputDir,
                            ["width"] = width,
                            ["height"] = height,
                            ["waitFrames"] = waitFrames,
                            ["cameraPath"] = string.IsNullOrWhiteSpace(cameraPath) ? null : cameraPath
                        }
                    }
                }
            };

            var tempRoot = Path.Combine(UnityAiGameMakerConfig.ProjectRoot, "Temp", "UnityAiGameMaker");
            Directory.CreateDirectory(tempRoot);
            var inputPath = Path.Combine(tempRoot, "playmode-menu-capture.json");
            File.WriteAllText(inputPath, payload.ToString(Formatting.Indented));
            Begin(inputPath, outFile);
        }

        [MenuItem("Tools/Unity AI Game Maker/Capture Menu Screenshots (Play Mode)")]
        public static void CaptureMenuScreenshotsPlayModeFromMenu()
        {
            var outputDir = Path.GetFullPath(Path.Combine(UnityAiGameMakerConfig.ProjectRoot, "..", "screenshots"));
            var outFile = Path.Combine(outputDir, "playmode-capture.out.json");
            BeginCaptureScenes(new JObject
            {
                ["filter"] = "menusandgameplay",
                ["outputDir"] = outputDir,
                ["width"] = 1080,
                ["height"] = 1920,
                ["waitFrames"] = 30
            }, outFile);
        }

        static void Begin(string inputPath, string outFile)
        {
            ClearStateFile();
            state = new BatchState
            {
                active = true,
                outputPath = outFile,
                sceneIndex = 0,
                step = "starting",
                scenes = LoadSceneJobs(inputPath),
                captures = new List<object>(),
                lastProgressTime = EditorApplication.timeSinceStartup
            };

            if (state.scenes.Count == 0)
                throw new InvalidOperationException("No play mode screenshot scenes found in batch JSON.");

            RegisterHandlers();
            SaveState();
            Debug.Log($"Unity AI play mode screenshot batch started with {state.scenes.Count} scene(s).");
            StartCurrentScene();
        }

        static void TryResumeFromDisk()
        {
            if (!Application.isBatchMode)
            {
                ClearStateFile();
                return;
            }

            if (state != null && state.active)
                return;

            if (!File.Exists(StateFilePath))
                return;

            try
            {
                state = JsonConvert.DeserializeObject<BatchState>(File.ReadAllText(StateFilePath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Unity AI play mode screenshot batch could not resume state: " + ex.Message);
                ClearStateFile();
                return;
            }

            if (state == null || !state.active || state.scenes == null || state.scenes.Count == 0)
            {
                ClearStateFile();
                return;
            }

            RegisterHandlers();
            Debug.Log($"Unity AI play mode screenshot batch resumed at scene {state.sceneIndex + 1}/{state.scenes.Count}, step={state.step}.");

            if (state.step == StepAwaitSettle && EditorApplication.isPlaying)
            {
                EditorApplication.update -= OnSettleUpdate;
                EditorApplication.update += OnSettleUpdate;
                return;
            }

            if (state.step == StepCaptured && !EditorApplication.isPlaying)
            {
                AdvanceToNextScene();
                return;
            }

            if (state.step == StepAwaitPlay && EditorApplication.isPlaying)
            {
                BeginSettle();
                return;
            }

            if (state.step == StepAwaitPlay && !EditorApplication.isPlaying)
            {
                StartCurrentScene();
            }
        }

        static void RegisterHandlers()
        {
            if (handlersRegistered)
                return;

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnWatchdogUpdate;
            handlersRegistered = true;
        }

        static void UnregisterHandlers()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnWatchdogUpdate;
            EditorApplication.update -= OnSettleUpdate;
            handlersRegistered = false;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (state == null || !state.active)
                return;

            TouchProgress();

            if (change == PlayModeStateChange.EnteredPlayMode && state.step == StepAwaitPlay)
                BeginSettle();

            if (change == PlayModeStateChange.EnteredEditMode && state.step == StepCaptured)
                AdvanceToNextScene();
        }

        static void AdvanceToNextScene()
        {
            if (state == null || !state.active)
                return;

            state.sceneIndex++;
            SaveState();

            if (state.sceneIndex >= state.scenes.Count)
            {
                Complete();
                return;
            }

            EditorApplication.delayCall += StartCurrentScene;
        }

        static void StartCurrentScene()
        {
            if (state == null || !state.active)
                return;

            if (state.sceneIndex >= state.scenes.Count)
            {
                Complete();
                return;
            }

            if (EditorApplication.isPlaying)
            {
                state.step = StepCaptured;
                SaveState();
                EditorApplication.isPlaying = false;
                return;
            }

            var job = state.scenes[state.sceneIndex];
            Debug.Log($"Unity AI play mode screenshot: opening scene {state.sceneIndex + 1}/{state.scenes.Count} -> {job.scenePath}");

            var scene = EditorSceneManager.OpenScene(job.scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new InvalidOperationException("Scene could not be opened: " + job.scenePath);

            PrepareGameplaySessionIfNeeded(job.scenePath);
            UnityAiScreenshotTools.PrepareGameViewForCapture(job.width, job.height);

            state.step = StepAwaitPlay;
            SaveState();
            EditorApplication.isPlaying = true;
        }

        static void PrepareGameplaySessionIfNeeded(string scenePath)
        {
            if (scenePath.IndexOf("Gameplay", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            PlayerPrefs.SetInt("MenuFlow.PendingLevel", 1);
            PlayerPrefs.Save();
        }

        static void BeginSettle()
        {
            if (state == null || !state.active)
                return;

            var job = state.scenes[state.sceneIndex];
            state.step = StepAwaitSettle;
            state.settleRemaining = Math.Max(1, job.waitFrames);
            SaveState();

            EditorApplication.update -= OnSettleUpdate;
            EditorApplication.update += OnSettleUpdate;
            Debug.Log($"Unity AI play mode screenshot: settling {state.settleRemaining} frame(s) for {job.scenePath}");
        }

        static void OnSettleUpdate()
        {
            if (state == null || !state.active || state.step != StepAwaitSettle)
            {
                EditorApplication.update -= OnSettleUpdate;
                return;
            }

            TouchProgress();

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= OnSettleUpdate;
                Fail(new InvalidOperationException("Play mode ended before screenshot settle completed."));
                return;
            }

            state.settleRemaining--;
            if (state.settleRemaining > 0)
            {
                SaveState();
                return;
            }

            EditorApplication.update -= OnSettleUpdate;
            CaptureCurrentScene();
        }

        static void CaptureCurrentScene()
        {
            var job = state.scenes[state.sceneIndex];
            Debug.Log($"Unity AI play mode screenshot: capturing {job.outputPath}");

            var captureArgs = new JObject
            {
                ["outputPath"] = job.outputPath,
                ["width"] = job.width,
                ["height"] = job.height,
                ["source"] = "playMode"
            };
            if (!string.IsNullOrWhiteSpace(job.cameraPath))
                captureArgs["cameraPath"] = job.cameraPath;

            var capture = UnityAiScreenshotTools.Capture(captureArgs);
            state.captures.Add(new
            {
                scenePath = job.scenePath,
                sceneName = Path.GetFileNameWithoutExtension(job.scenePath),
                outputPath = job.outputPath,
                capture
            });

            state.step = StepCaptured;
            SaveState();
            EditorApplication.isPlaying = false;
            Debug.Log($"Unity AI play mode screenshot: exiting play mode for {job.scenePath}");
        }

        static void OnWatchdogUpdate()
        {
            if (state == null || !state.active)
                return;

            var now = EditorApplication.timeSinceStartup;
            if (now - watchdogLastCheck < 1d)
                return;

            watchdogLastCheck = now;

            if (now - state.lastProgressTime <= StepTimeoutSeconds)
                return;

            Fail(new TimeoutException(
                $"Play mode screenshot batch timed out after {StepTimeoutSeconds:0} seconds without progress (step={state.step}, sceneIndex={state.sceneIndex})."));
        }

        static void TouchProgress()
        {
            if (state == null)
                return;

            state.lastProgressTime = EditorApplication.timeSinceStartup;
            SaveState();
        }

        static void Complete()
        {
            var response = new
            {
                ok = true,
                mode = "playMode",
                count = state?.captures?.Count ?? 0,
                captures = state?.captures ?? new List<object>()
            };

            var outFile = state?.outputPath ?? string.Empty;
            WriteJson(outFile, response);
            Debug.Log($"Unity AI play mode screenshot batch completed: {outFile}");

            Cleanup();
            EditorApplication.Exit(0);
        }

        static void Fail(Exception ex)
        {
            var outFile = state?.outputPath ?? Path.Combine(Path.GetTempPath(), "unity-ai-playmode-batch-error.json");
            WriteJson(outFile, new { ok = false, error = ex.ToString() });
            Debug.LogError("Unity AI play mode screenshot batch failed: " + ex.Message);

            Cleanup();
            EditorApplication.Exit(1);
        }

        static void Cleanup()
        {
            UnregisterHandlers();
            state = null;
            ClearStateFile();
        }

        static void SaveState()
        {
            if (state == null || !state.active)
                return;

            var directory = Path.GetDirectoryName(StateFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(StateFilePath, JsonConvert.SerializeObject(state, Formatting.Indented));
        }

        static void ClearStateFile()
        {
            if (File.Exists(StateFilePath))
                File.Delete(StateFilePath);
        }

        static List<SceneCaptureJob> LoadSceneJobs(string inputPath)
        {
            var root = JObject.Parse(File.ReadAllText(inputPath));
            var jobs = new List<SceneCaptureJob>();

            if (root["scenes"] is JArray scenesArray)
            {
                foreach (var token in scenesArray)
                    jobs.Add(ParseSceneJob(token as JObject));

                return jobs;
            }

            var commands = root["commands"] as JArray;
            if (commands == null)
                throw new InvalidOperationException("Batch JSON must contain scenes[] or commands[].");

            foreach (var command in commands)
            {
                if (command?["tool"]?.Value<string>() != "screenshots.captureScenes")
                    continue;

                var args = command["args"] as JObject ?? new JObject();
                var outputDir = args.Value<string>("outputDir") ?? args.Value<string>("output")
                    ?? Path.GetFullPath(Path.Combine(UnityAiGameMakerConfig.ProjectRoot, "..", "screenshots"));
                var width = args.Value<int?>("width") ?? 1080;
                var height = args.Value<int?>("height") ?? 1920;
                var waitFrames = args.Value<int?>("waitFrames") ?? 30;
                var cameraPath = args.Value<string>("cameraPath") ?? "";
                var scenePaths = args["scenes"]?.ToObject<string[]>();
                if (scenePaths == null || scenePaths.Length == 0)
                    scenePaths = ResolveScenePathsFromFilter(args.Value<string>("filter") ?? "menu");

                foreach (var scenePath in scenePaths)
                {
                    var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                    jobs.Add(new SceneCaptureJob
                    {
                        scenePath = scenePath,
                        outputPath = Path.Combine(outputDir, sceneName + ".png"),
                        width = width,
                        height = height,
                        waitFrames = waitFrames,
                        cameraPath = cameraPath
                    });
                }
            }

            return jobs;
        }

        static SceneCaptureJob ParseSceneJob(JObject args)
        {
            if (args == null)
                throw new InvalidOperationException("Scene job must be an object.");

            var scenePath = args.Value<string>("scenePath") ?? args.Value<string>("path");
            if (string.IsNullOrWhiteSpace(scenePath))
                throw new InvalidOperationException("Each scene job requires scenePath.");

            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var outputDir = args.Value<string>("outputDir") ?? args.Value<string>("output")
                ?? Path.GetFullPath(Path.Combine(UnityAiGameMakerConfig.ProjectRoot, "..", "screenshots"));

            return new SceneCaptureJob
            {
                scenePath = scenePath,
                outputPath = args.Value<string>("outputPath") ?? Path.Combine(outputDir, sceneName + ".png"),
                width = args.Value<int?>("width") ?? 1080,
                height = args.Value<int?>("height") ?? 1920,
                waitFrames = args.Value<int?>("waitFrames") ?? 30,
                cameraPath = args.Value<string>("cameraPath") ?? ""
            };
        }

        static string[] ResolveScenePathsFromFilter(string filter)
        {
            var buildScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            return filter.ToLowerInvariant() switch
            {
                "menu" => buildScenes.Where(IsMenuScene).ToArray(),
                "gameplay" => buildScenes.Where(path =>
                    path.IndexOf("Gameplay", StringComparison.OrdinalIgnoreCase) >= 0).ToArray(),
                "menusandgameplay" => buildScenes.Where(path =>
                    IsMenuScene(path) || path.IndexOf("Gameplay", StringComparison.OrdinalIgnoreCase) >= 0).ToArray(),
                _ => buildScenes
            };
        }

        static bool IsMenuScene(string path)
        {
            var sceneName = Path.GetFileNameWithoutExtension(path);
            return sceneName.StartsWith("Menu_", StringComparison.OrdinalIgnoreCase)
                || sceneName.Contains("Main_Menu", StringComparison.OrdinalIgnoreCase)
                || sceneName.Contains("MainMenu", StringComparison.OrdinalIgnoreCase);
        }

        static void WriteJson(string path, object payloadObject)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonConvert.SerializeObject(payloadObject, Formatting.Indented));
            AssetDatabase.Refresh();
        }
    }
}
#endif
