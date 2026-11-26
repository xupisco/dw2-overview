using System.Net;
using System.Text;
using System.Text.Json;

namespace DW2Overview;

/// <summary>
/// A small HTTP server inside the game, on http://localhost:&lt;port&gt;/ only (no other machine can reach it).
/// It serves the page from the web folder and the JSON that Snapshot prepared on the game thread;
/// it never touches game objects itself.
///
/// Reading is safe from other sites by design: a page from another origin can send a request to localhost,
/// but without CORS headers the browser will not let it read the answer. Requests that carry another
/// site's Origin are refused anyway. Commands (later) will also need a per-session token.
/// </summary>
internal static class Server
{
    private static HttpListener listener;
    private static string root;
    private static int port;

    private static readonly Dictionary<string, string> contentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain; charset=utf-8",
    };

    public static void Start(int listenPort, string webRoot)
    {
        port = listenPort;
        root = Path.GetFullPath(webRoot);
        listener = new HttpListener();
        // Only "localhost": http.sys lets a normal user listen there without a URL reservation (127.0.0.1 would need admin).
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        new Thread(Loop) { IsBackground = true, Name = "DW2Overview server" }.Start();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
        Log.Info($"Server listening on http://localhost:{port}/ (web folder {root})");
    }

    private static void Stop()
    {
        try
        {
            listener?.Close();
        }
        catch
        {
        }
    }

    private static void Loop()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (Exception) when (!listener.IsListening)
            {
                return;
            }
            catch (Exception e)
            {
                Log.Warn("Accept: " + e.Message);
                continue;
            }
            ThreadPool.QueueUserWorkItem(_ => Handle(context));
        }
    }

    private static void Handle(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        try
        {
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["X-Content-Type-Options"] = "nosniff";

            string origin = request.Headers["Origin"];
            if (origin != null && origin != $"http://localhost:{port}")
            {
                Send(response, 403, "text/plain; charset=utf-8", "Forbidden origin.");
                return;
            }
            string path = request.Url?.AbsolutePath ?? "/";
            if (request.HttpMethod == "POST")
            {
                HandleCommand(request, response, path, origin);
                return;
            }
            if (request.HttpMethod != "GET")
            {
                Send(response, 405, "text/plain; charset=utf-8", "Method not allowed.");
                return;
            }

            switch (path)
            {
                case "/api/info":
                    Snapshot.Touch();
                    Send(response, 200, contentTypes[".json"], Snapshot.InfoJson);
                    return;
                case "/api/construction":
                    Snapshot.Touch();
                    Send(response, 200, contentTypes[".json"], Snapshot.ConstructionJson);
                    return;
                case "/api/colonies":
                    Snapshot.Touch();
                    Send(response, 200, contentTypes[".json"], Snapshot.ColoniesJson);
                    return;
                case "/api/research":
                    Snapshot.Touch();
                    Send(response, 200, contentTypes[".json"], Snapshot.ResearchJson);
                    return;
                case "/api/messages":
                    Snapshot.Touch();
                    Send(response, 200, contentTypes[".json"], Snapshot.MessagesJson);
                    return;
                case "/api/resources":
                    Snapshot.TouchResources();
                    Send(response, 200, contentTypes[".json"], Snapshot.ResourcesJson);
                    return;
                case "/api/ships":
                    Snapshot.TouchShips();
                    Send(response, 200, contentTypes[".json"], Snapshot.ShipsJson);
                    return;
                case "/api/commands":
                {
                    Snapshot.Touch();
                    var rows = Commands.OnGameThread(game => Commands.ListKeyMappings(game));
                    Send(response, rows == null ? 503 : 200, contentTypes[".json"],
                        JsonSerializer.Serialize(new { items = rows ?? new List<object>() }, apiJson));
                    return;
                }
                case "/api/search":
                    HandleSearch(request, response);
                    return;
                case "/api/index":
                    Snapshot.TouchIndex();
                    Send(response, 200, contentTypes[".json"], Snapshot.IndexJson);
                    return;
            }
            if (path.StartsWith("/api/", StringComparison.Ordinal))
            {
                Send(response, 404, contentTypes[".json"], "{\"error\":\"unknown endpoint\"}");
                return;
            }
            ServeFile(response, path == "/" ? "/index.html" : path);
        }
        catch (Exception e)
        {
            Log.Warn($"Request {request.Url}: {e.Message}");
            try
            {
                Send(response, 500, "text/plain; charset=utf-8", "Internal error.");
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Commands must come from our own page: browsers always send Origin on a POST, and the custom token
    /// header makes a cross-site request need a CORS preflight, which this server never grants.
    /// </summary>
    private static void HandleCommand(HttpListenerRequest request, HttpListenerResponse response, string path, string origin)
    {
        if (origin == null || request.Headers["X-Overview-Token"] != Commands.Token)
        {
            Send(response, 403, contentTypes[".json"], "{\"error\":\"forbidden\"}");
            return;
        }
        if (path != "/api/goto" && path != "/api/key" && path != "/api/suggestion" && path != "/api/audio" && path != "/api/research")
        {
            Send(response, 404, contentTypes[".json"], "{\"error\":\"unknown command\"}");
            return;
        }
        GoToRequest body;
        using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
        {
            string text = reader.ReadToEnd();
            body = text.Length <= 1024 ? JsonSerializer.Deserialize<GoToRequest>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
        }
        if (path == "/api/suggestion")
        {
            // {"key": a message key from /api/messages, then "accept": true | false (suggestions) or "button": yes | no | other | show}
            if (body == null || string.IsNullOrEmpty(body.Key) || (body.Accept == null && body.Button is not ("yes" or "no" or "other" or "show")))
            {
                Send(response, 400, contentTypes[".json"], "{\"error\":\"expected {key, accept}\"}");
                return;
            }
            if (body.Button != null) Commands.PressDialogButton(body.Key, body.Button);
            else Commands.AnswerSuggestion(body.Key, body.Accept.Value);
            Send(response, 202, contentTypes[".json"], "{\"queued\":true}");
            return;
        }
        if (path == "/api/audio")
        {
            // {"mute": true | false} and/or {"music": 0..1, or -1 for the volume before music was muted}
            if (body == null || (body.Mute == null && body.Music == null))
            {
                Send(response, 400, contentTypes[".json"], "{\"error\":\"expected {mute} or {music}\"}");
                return;
            }
            var state = Commands.OnGameThread(game => Audio.Set(game, body.Mute, body.Music));
            Send(response, state == null ? 503 : 200, contentTypes[".json"], JsonSerializer.Serialize(state ?? new { error = "game busy" }, apiJson));
            return;
        }
        if (path == "/api/research")
        {
            // {"id": a research project id from /api/research}: the game's research screen, on that project
            if (body == null || body.Id < 0)
            {
                Send(response, 400, contentTypes[".json"], "{\"error\":\"expected {id}\"}");
                return;
            }
            Commands.OpenResearch(body.Id);
            Send(response, 202, contentTypes[".json"], "{\"queued\":true}");
            return;
        }
        if (path == "/api/key")
        {
            // {"id": index into the key mappings listed by /api/commands}
            if (body == null || body.Id < 0)
            {
                Send(response, 400, contentTypes[".json"], "{\"error\":\"expected {id}\"}");
                return;
            }
            Commands.PressKey(body.Id);
            Send(response, 202, contentTypes[".json"], "{\"queued\":true}");
            return;
        }
        if (body == null || (body.Type != "ship" && body.Type != "orb" && body.Type != "fleet") || body.Id <= 0)
        {
            Send(response, 400, contentTypes[".json"], "{\"error\":\"expected {type: ship|orb|fleet, id}\"}");
            return;
        }
        Commands.GoTo(body.Type, body.Id);
        Send(response, 202, contentTypes[".json"], "{\"queued\":true}");
    }

    private static readonly JsonSerializerOptions apiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>GET /api/search?q=... : the game's "Search Known Items", run on the game thread.</summary>
    private static void HandleSearch(HttpListenerRequest request, HttpListenerResponse response)
    {
        Snapshot.Touch();
        string q = (request.QueryString["q"] ?? "").Trim();
        if (q.Length == 0 || q.Length > 64)
        {
            Send(response, 200, contentTypes[".json"], "{\"items\":[]}");
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = Commands.OnGameThread(game =>
        {
            var empire = (game.Renderer as DistantWorlds2.ScaledRenderer)?.PlayerEmpire;
            return game.Galaxy == null || empire == null ? null : Views.Search(game.Galaxy, empire, q, 50);
        });
        sw.Stop();
        if (rows == null)
        {
            Send(response, 503, contentTypes[".json"], "{\"error\":\"game busy or not loaded\",\"items\":[]}");
            return;
        }
        Send(response, 200, contentTypes[".json"], JsonSerializer.Serialize(new { q, ms = Math.Round(sw.Elapsed.TotalMilliseconds, 1), items = rows }, apiJson));
    }

    private sealed class GoToRequest
    {
        public string Type { get; set; }
        public int Id { get; set; }
        public string Key { get; set; }
        public bool? Accept { get; set; }
        public string Button { get; set; }
        public bool? Mute { get; set; }
        public float? Music { get; set; }
    }

    private static void ServeFile(HttpListenerResponse response, string urlPath)
    {
        string relative = Uri.UnescapeDataString(urlPath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        {
            Send(response, 404, "text/plain; charset=utf-8", "Not found.");
            return;
        }
        string type = contentTypes.GetValueOrDefault(Path.GetExtension(full), "application/octet-stream");
        Send(response, 200, type, File.ReadAllBytes(full));
    }

    private static void Send(HttpListenerResponse response, int status, string type, string body) =>
        Send(response, status, type, Encoding.UTF8.GetBytes(body));

    private static void Send(HttpListenerResponse response, int status, string type, byte[] body)
    {
        response.StatusCode = status;
        response.ContentType = type;
        response.ContentLength64 = body.Length;
        response.OutputStream.Write(body, 0, body.Length);
        response.Close();
    }
}
