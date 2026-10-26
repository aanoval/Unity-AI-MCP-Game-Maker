#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Alday.UnityAiGameMaker.Editor
{
    [InitializeOnLoad]
    public static class UnityAiGameMakerServer
    {
        static HttpListener listener;
        static Thread serverThread;
        static bool isRunning;
        static UnityAiGameMakerConfig config;

        // Unique per domain, so the self-check can tell our listener apart from one left over from an earlier domain.
        static readonly string InstanceId = Guid.NewGuid().ToString("N");
        const int PortFallbackAttempts = 10;
        const int ProbeTimeoutMs = 2000;
        const int OwnershipProbes = 3;

        static UnityAiGameMakerServer()
        {
            // Asset import workers are separate Unity processes that also run [InitializeOnLoad]. A server started
            // there grabs the port and answers RPC calls without being the Editor the user works in, and its
            // LoadOrCreate() races the Editor's writes to the config file (and can rotate the token).
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;

            config = UnityAiGameMakerConfig.LoadOrCreate();
            EditorApplication.update += UnityAiMainThread.Pump;
            // Release the port before a domain reload (script compile, entering Play Mode); otherwise the old
            // domain's listener keeps answering with stale state and calls fail with Unauthorized.
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;

            if (config.autoStart)
                Start();
        }

        [MenuItem("Tools/Unity AI Game Maker/Start Local Server")]
        public static void StartFromMenu()
        {
            config = UnityAiGameMakerConfig.LoadOrCreate();
            Start();
        }

        [MenuItem("Tools/Unity AI Game Maker/Stop Local Server")]
        public static void StopFromMenu()
        {
            Stop();
        }

        [MenuItem("Tools/Unity AI Game Maker/Print Token")]
        public static void PrintToken()
        {
            config = UnityAiGameMakerConfig.LoadOrCreate();
            Debug.Log($"Unity AI Game Maker token: {config.token}");
            Debug.Log($"Unity AI Game Maker config: {UnityAiGameMakerConfig.ConfigPath}");
        }

        [MenuItem("Tools/Unity AI Game Maker/Open Config")]
        public static void OpenConfig()
        {
            config = UnityAiGameMakerConfig.LoadOrCreate();
            EditorUtility.RevealInFinder(UnityAiGameMakerConfig.ConfigPath);
        }

        public static void Start()
        {
            config = UnityAiGameMakerConfig.LoadOrCreate();
            if (isRunning)
            {
                Debug.Log("Unity AI Game Maker server is already running.");
                return;
            }

            if (config.bindHost != "127.0.0.1" && config.bindHost != "localhost")
            {
                Debug.LogError("Unity AI Game Maker refuses to bind to non-loopback host by default.");
                return;
            }

            StartOn(config.port, config.port);
        }

        static void StartOn(int port, int configuredPort)
        {
            for (; port < configuredPort + PortFallbackAttempts && port <= 65535; port++)
            {
                if (!TryBind(port))
                    continue;

                // The self-check has to wait until the domain has finished loading: during [InitializeOnLoad] the
                // listener's worker threads cannot enter the new domain yet, so a synchronous probe always times out.
                var started = listener;
                var startedPort = port;
                EditorApplication.delayCall += () => VerifyOwnership(started, startedPort, configuredPort);
                return;
            }

            Debug.LogError($"Unity AI Game Maker could not start on ports {configuredPort}-{configuredPort + PortFallbackAttempts - 1}. Set a free \"port\" in {UnityAiGameMakerConfig.ConfigPath} and use Tools > Unity AI Game Maker > Start Local Server.");
        }

        static bool TryBind(int port)
        {
            var candidate = new HttpListener();
            candidate.Prefixes.Add($"http://{config.bindHost}:{port}/");
            try
            {
                candidate.Start();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unity AI Game Maker could not bind port {port}: {ex.Message}");
                candidate.Close();
                return false;
            }

            listener = candidate;
            isRunning = true;
            serverThread = new Thread(() => ServerLoop(candidate))
            {
                IsBackground = true,
                Name = "unity-ai-game-maker"
            };
            serverThread.Start();
            return true;
        }

        static void VerifyOwnership(HttpListener started, int port, int configuredPort)
        {
            if (listener != started)
                return;

            // HttpListener.Start() can return without error while a listener left over from a previous domain
            // still owns the socket and answers with its stale token. Only keep the port if our own instance answers
            // every probe (several, in case the OS spreads connections across sockets sharing the port).
            var owned = true;
            for (var probe = 0; probe < OwnershipProbes && owned; probe++)
                owned = ProbeOwnsPort(port);

            if (!owned)
            {
                Debug.LogWarning($"Unity AI Game Maker: port {port} is answered by another listener (a stale server from a previous domain reload, or another Editor). Trying the next port.");
                Stop();
                StartOn(port + 1, configuredPort);
                return;
            }

            if (port != configuredPort)
            {
                // The CLI and MCP adapter read the port from the config file, so they follow the move.
                config.port = port;
                config.Save();
                Debug.LogWarning($"Unity AI Game Maker moved from port {configuredPort} to {port} and saved it to {UnityAiGameMakerConfig.ConfigPath}.");
            }

            Debug.Log($"Unity AI Game Maker listening on http://{config.bindHost}:{port}");
        }

        static bool ProbeOwnsPort(int port)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create($"http://{config.bindHost}:{port}/health");
                request.Proxy = null;
                request.Timeout = ProbeTimeoutMs;
                request.ReadWriteTimeout = ProbeTimeoutMs;
                using var response = (HttpWebResponse)request.GetResponse();
                using var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8);
                var health = JObject.Parse(reader.ReadToEnd());
                return health.Value<string>("instanceId") == InstanceId;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unity AI Game Maker self-check on port {port} failed: {ex.Message}");
                return false;
            }
        }

        public static void Stop()
        {
            isRunning = false;

            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unity AI Game Maker stop warning: {ex.Message}");
            }
            finally
            {
                listener = null;
            }
        }

        static void ServerLoop(HttpListener owned)
        {
            while (isRunning && listener == owned)
            {
                try
                {
                    var context = owned.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => Handle(context));
                }
                catch
                {
                    if (isRunning)
                        Thread.Sleep(50);
                }
            }
        }

        static void Handle(HttpListenerContext context)
        {
            try
            {
                var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "";
                if (string.IsNullOrEmpty(path))
                    path = "/";

                if (context.Request.HttpMethod == "GET" && path == "/health")
                {
                    WriteJson(context, 200, new
                    {
                        ok = true,
                        name = "Unity AI Game Maker",
                        version = "0.4.1",
                        instanceId = InstanceId,
                        bindHost = config.bindHost,
                        port = config.port,
                        authRequired = config.authRequired
                    });
                    return;
                }

                if (!IsAuthorized(context))
                {
                    WriteJson(context, 401, new { ok = false, error = "Unauthorized" });
                    return;
                }

                if (context.Request.HttpMethod == "GET" && path == "/tools")
                {
                    WriteJson(context, 200, new
                    {
                        ok = true,
                        count = UnityAiTools.ToolNames.Length,
                        tools = UnityAiTools.ToolNames
                    });
                    return;
                }

                if (context.Request.HttpMethod == "POST" && path == "/rpc")
                {
                    var body = ReadBody(context.Request);
                    var request = JObject.Parse(body);
                    var tool = request.Value<string>("tool");
                    var args = request["args"] as JObject ?? new JObject();

                    var result = UnityAiMainThread.Run(() => UnityAiTools.Invoke(tool, args, config));
                    WriteJson(context, 200, new { ok = true, result });
                    return;
                }

                WriteJson(context, 404, new { ok = false, error = "Not found" });
            }
            catch (Exception ex)
            {
                WriteJson(context, 500, new { ok = false, error = ex.Message });
            }
        }

        static bool IsAuthorized(HttpListenerContext context)
        {
            if (!config.authRequired)
                return true;

            if (string.IsNullOrWhiteSpace(config.token))
                return false;

            var header = context.Request.Headers["Authorization"];
            return header == $"Bearer {config.token}";
        }

        static string ReadBody(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        static void WriteJson(HttpListenerContext context, int statusCode, object payload)
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }
    }
}
#endif
