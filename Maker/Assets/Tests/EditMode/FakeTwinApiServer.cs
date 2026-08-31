using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace EditModeTests
{
    /// <summary>
    /// A real HTTP server on loopback, standing in for the Twin backend.
    /// </summary>
    /// <remarks>
    /// <para>The point is to test what actually goes over the wire. A test that
    /// mocked out <c>UnityWebRequest</c> would confirm the client calls a method
    /// we wrote, not that it sends a POST with a JSON body to the right path —
    /// and the request line and headers are precisely where this client can be
    /// wrong. <c>UnityWebRequest.Post(url, string)</c>, for instance, has
    /// historically form-encoded the body, which a mock cannot catch and which
    /// the server would answer with a 422.</para>
    ///
    /// <para>Loopback only, on a port the OS hands out, so it needs no
    /// privileges, reaches no network and cannot collide with a colleague's
    /// run.</para>
    /// </remarks>
    public sealed class FakeTwinApiServer : IDisposable
    {
        /// <summary>One request as the server received it.</summary>
        public sealed class Captured
        {
            public string Method;
            public string Path;
            public string ContentType;
            public string Accept;
            public string Authorization;
            public string Body;
        }

        /// <summary>What to answer with, in the order requests arrive.</summary>
        private sealed class Canned
        {
            public int Status;
            public string Body;
        }

        private readonly HttpListener _listener = new();
        private readonly Queue<Canned> _responses = new();
        private readonly List<Captured> _requests = new();
        private readonly object _gate = new();
        private readonly Thread _thread;
        private volatile bool _stopping;

        public FakeTwinApiServer()
        {
            var port = FreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();

            _thread = new Thread(Serve) { IsBackground = true, Name = nameof(FakeTwinApiServer) };
            _thread.Start();
        }

        /// <summary>The origin to put in a <c>TwinApiConfig</c>.</summary>
        public string BaseUrl { get; }

        /// <summary>Every request received, in order.</summary>
        public IReadOnlyList<Captured> Requests
        {
            get { lock (_gate) return _requests.ToArray(); }
        }

        /// <summary>The only request received. Fails the caller's assertion if there were none.</summary>
        public Captured LastRequest
        {
            get { lock (_gate) return _requests.Count == 0 ? null : _requests[_requests.Count - 1]; }
        }

        /// <summary>Queue the next response. Calls are answered in the order queued.</summary>
        public FakeTwinApiServer Respond(int status, string body = null)
        {
            lock (_gate) _responses.Enqueue(new Canned { Status = status, Body = body });
            return this;
        }

        /// <summary>Queue a token pair shaped exactly like the server's <c>TokenResponse</c>.</summary>
        public FakeTwinApiServer RespondWithTokens(string accessToken, string refreshToken, int expiresIn = 900)
        {
            return Respond(200,
                "{\"access_token\":\"" + accessToken + "\"," +
                "\"refresh_token\":\"" + refreshToken + "\"," +
                "\"token_type\":\"bearer\"," +
                "\"expires_in\":" + expiresIn + "," +
                "\"scope\":\"twin:read twin:write\"}");
        }

        /// <summary>Queue an error in the backend's envelope: <c>{"error":{"code","message","details"}}</c>.</summary>
        public FakeTwinApiServer RespondWithError(int status, string code, string message = "no")
        {
            return Respond(status,
                "{\"error\":{\"code\":\"" + code + "\",\"message\":\"" + message + "\",\"details\":{}}}");
        }

        private void Serve()
        {
            while (!_stopping)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (Exception)
                {
                    // Thrown when the listener is closed from Dispose. Ordinary.
                    return;
                }

                try
                {
                    var request = context.Request;
                    string body;
                    using (var reader = new System.IO.StreamReader(request.InputStream, Encoding.UTF8))
                    {
                        body = reader.ReadToEnd();
                    }

                    lock (_gate)
                    {
                        _requests.Add(new Captured
                        {
                            Method = request.HttpMethod,
                            Path = request.Url?.AbsolutePath,
                            ContentType = request.ContentType,
                            Accept = request.Headers["Accept"],
                            Authorization = request.Headers["Authorization"],
                            Body = body,
                        });
                    }

                    Canned canned;
                    lock (_gate)
                    {
                        // An unqueued request gets a 500 rather than a hang, so a
                        // test that sends one more call than it expected fails
                        // with something readable instead of timing out.
                        canned = _responses.Count > 0
                            ? _responses.Dequeue()
                            : new Canned { Status = 500, Body = "{\"error\":{\"code\":\"NO_RESPONSE_QUEUED\",\"message\":\"unexpected request\",\"details\":{}}}" };
                    }

                    context.Response.StatusCode = canned.Status;
                    if (string.IsNullOrEmpty(canned.Body))
                    {
                        context.Response.ContentLength64 = 0;
                    }
                    else
                    {
                        var bytes = Encoding.UTF8.GetBytes(canned.Body);
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = bytes.Length;
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }

                    context.Response.OutputStream.Close();
                }
                catch (Exception)
                {
                    // A broken connection must not take the server thread with it.
                }
            }
        }

        /// <summary>
        /// A port the OS is willing to give away, found by binding to port 0 and
        /// letting go again.
        /// </summary>
        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            _stopping = true;
            try { _listener.Stop(); } catch (Exception) { }
            try { _listener.Close(); } catch (Exception) { }
            _thread?.Join(TimeSpan.FromSeconds(2));
        }
    }
}
