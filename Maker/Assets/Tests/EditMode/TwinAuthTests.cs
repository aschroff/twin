using System;
using System.Collections;
using System.Threading.Tasks;
using Code.Net.Auth;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace EditModeTests
{
    /// <summary>
    /// What the Unity client puts on the wire, checked against a real HTTP
    /// server on loopback rather than a mock.
    /// </summary>
    /// <remarks>
    /// <para>These exist because the backend is not always reachable, and because
    /// the ways this client can be wrong are all invisible from inside Unity: a
    /// form-encoded body instead of JSON, a doubled <c>/v1</c> in the path, a
    /// renamed JSON key. Each of those compiles, runs, and fails only against a
    /// live server — as a 422 or a 404 that says nothing about the cause.</para>
    ///
    /// <para>The expected shapes here are taken from the backend's own Pydantic
    /// schemas (<c>contexts/identity/schemas.py</c>). Its request models are
    /// declared <c>extra="forbid"</c>, so an extra key is a 422 rather than a
    /// field the server politely ignores — which is why the body assertions
    /// check the exact set of keys and not just that the expected ones are
    /// present.</para>
    /// </remarks>
    public class TwinAuthTests
    {
        private FakeTwinApiServer _server;
        private TwinApiConfig _config;

        /// <summary>A JWT whose payload is {"sub":"user-1","org":"org-1","exp":9999999999}.</summary>
        private const string AccessTokenWithClaims =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
            "eyJzdWIiOiJ1c2VyLTEiLCJvcmciOiJvcmctMSIsImV4cCI6OTk5OTk5OTk5OX0." +
            "not-a-real-signature";

        [SetUp]
        public void SetUp()
        {
            _server = new FakeTwinApiServer();
            _config = TwinApiConfig.Create(_server.BaseUrl, timeoutSeconds: 10);
        }

        [TearDown]
        public void TearDown()
        {
            // Leave no shared session behind for the next test to inherit.
            TwinAuth.OverrideForTests(null, null);
            _server?.Dispose();
        }

        // --- the wire contract -------------------------------------------------

        [UnityTest]
        public IEnumerator Login_posts_json_to_v1_auth_login()
        {
            _server.RespondWithTokens("access-1", "refresh-1");
            var client = new TwinAuthClient(_config);

            var task = client.LoginAsync("anna@example.com", "correct-horse-battery-staple");
            yield return WaitFor(task);
            AssertSucceeded(task);

            var request = _server.LastRequest;
            Assert.AreEqual("POST", request.Method, "The API accepts only POST on the auth routes.");
            Assert.AreEqual("/v1/auth/login", request.Path);
            StringAssert.StartsWith("application/json", request.ContentType,
                "A form-encoded body is answered with a 422 by the server's Pydantic model.");

            var body = JObject.Parse(request.Body);
            CollectionAssert.AreEquivalent(new[] { "email", "password" }, PropertyNames(body),
                "LoginRequest is extra=\"forbid\": any other key makes this a 422.");
            Assert.AreEqual("anna@example.com", (string)body["email"]);
            Assert.AreEqual("correct-horse-battery-staple", (string)body["password"]);
        }

        [UnityTest]
        public IEnumerator Login_trims_surrounding_whitespace_from_the_address()
        {
            _server.RespondWithTokens("access-1", "refresh-1");
            var client = new TwinAuthClient(_config);

            var task = client.LoginAsync("  anna@example.com  ", "pw");
            yield return WaitFor(task);
            AssertSucceeded(task);

            Assert.AreEqual("anna@example.com", (string)JObject.Parse(_server.LastRequest.Body)["email"],
                "A trailing space typed on a phone keyboard must not read as a different address.");
        }

        [UnityTest]
        public IEnumerator Login_reads_the_token_pair_out_of_the_response()
        {
            _server.RespondWithTokens("access-1", "refresh-1", expiresIn: 900);
            var client = new TwinAuthClient(_config);

            var task = client.LoginAsync("anna@example.com", "pw");
            yield return WaitFor(task);
            AssertSucceeded(task);

            var tokens = task.Result;
            Assert.AreEqual("access-1", tokens.AccessToken);
            Assert.AreEqual("refresh-1", tokens.RefreshToken);
            Assert.AreEqual("bearer", tokens.TokenType);
            Assert.AreEqual(900, tokens.ExpiresIn);
            Assert.AreEqual("twin:read twin:write", tokens.Scope);
        }

        [UnityTest]
        public IEnumerator Login_maps_a_rejected_credential_pair_to_IsInvalidCredentials()
        {
            _server.RespondWithError(401, "INVALID_CREDENTIALS", "Email address or password is incorrect.");
            var client = new TwinAuthClient(_config);

            var task = client.LoginAsync("anna@example.com", "wrong");
            yield return WaitFor(task);

            var error = AssertFailed(task);
            Assert.IsTrue(error.IsInvalidCredentials);
            Assert.IsFalse(error.IsNetworkFailure, "A 401 reached the server; it is not a connection problem.");
            Assert.AreEqual(401, error.StatusCode);
        }

        [UnityTest]
        public IEnumerator An_unreachable_server_is_a_network_failure_not_a_rejection()
        {
            // Port 1 refuses immediately. The distinction matters: a rejection
            // signs the person out, a network failure must not.
            var client = new TwinAuthClient(TwinApiConfig.Create("http://127.0.0.1:1", timeoutSeconds: 5));

            var task = client.LoginAsync("anna@example.com", "pw");
            yield return WaitFor(task, seconds: 30);

            var error = AssertFailed(task);
            Assert.IsTrue(error.IsNetworkFailure);
            Assert.IsFalse(error.IsInvalidCredentials);
            Assert.AreEqual(0, error.StatusCode);
        }

        [UnityTest]
        public IEnumerator Refresh_posts_the_token_under_the_snake_case_key()
        {
            _server.RespondWithTokens("access-2", "refresh-2");
            var client = new TwinAuthClient(_config);

            var task = client.RefreshAsync("refresh-1");
            yield return WaitFor(task);
            AssertSucceeded(task);

            var request = _server.LastRequest;
            Assert.AreEqual("POST", request.Method);
            Assert.AreEqual("/v1/auth/refresh", request.Path);

            var body = JObject.Parse(request.Body);
            CollectionAssert.AreEquivalent(new[] { "refresh_token" }, PropertyNames(body),
                "The server's field is refresh_token; refreshToken would be a 422.");
            Assert.AreEqual("refresh-1", (string)body["refresh_token"]);
        }

        [UnityTest]
        public IEnumerator Logout_posts_the_token_and_accepts_an_empty_204()
        {
            _server.Respond(204);
            var client = new TwinAuthClient(_config);

            var task = client.LogoutAsync("refresh-1");
            yield return WaitFor(task);
            AssertSucceeded(task);

            var request = _server.LastRequest;
            Assert.AreEqual("/v1/auth/logout", request.Path);
            Assert.AreEqual("refresh-1", (string)JObject.Parse(request.Body)["refresh_token"]);
        }

        [UnityTest]
        public IEnumerator A_base_url_with_a_trailing_slash_does_not_double_the_separator()
        {
            _server.RespondWithTokens("access-1", "refresh-1");
            var client = new TwinAuthClient(TwinApiConfig.Create(_server.BaseUrl + "/", timeoutSeconds: 10));

            var task = client.LoginAsync("anna@example.com", "pw");
            yield return WaitFor(task);
            AssertSucceeded(task);

            Assert.AreEqual("/v1/auth/login", _server.LastRequest.Path,
                "//v1/auth/login is a 404, and an easy thing to type into the config asset.");
        }

        // --- what a later data call actually gets ------------------------------

        [UnityTest]
        public IEnumerator After_signing_in_a_request_carries_the_bearer_token()
        {
            // The whole question this feature exists to answer: sign in on the
            // settings page, and a data call made later somewhere else is
            // authenticated without being handed anything.
            _server.RespondWithTokens(AccessTokenWithClaims, "refresh-1");
            UseTestSession();

            var signIn = TwinAuth.SignInAsync("anna@example.com", "pw");
            yield return WaitFor(signIn);
            AssertSucceeded(signIn);

            using var request = UnityWebRequest.Get($"{TwinAuth.BaseUrl}/v1/twins");
            var authorize = TwinAuth.AuthorizeAsync(request);
            yield return WaitFor(authorize);
            AssertSucceeded(authorize);

            Assert.AreEqual($"Bearer {AccessTokenWithClaims}", request.GetRequestHeader("Authorization"));
            Assert.IsTrue(TwinAuth.IsSignedIn);
            Assert.AreEqual("user-1", TwinAuth.Session.UserId, "Read from the token's sub claim.");
        }

        [UnityTest]
        public IEnumerator Signing_in_spends_no_refresh_token_afterwards()
        {
            // Only the login call may reach the server. A restore that ran after
            // a fresh sign-in would spend the brand-new refresh token, and the
            // server treats a token presented twice as stolen — revoking every
            // session this user has, on every device.
            _server.RespondWithTokens(AccessTokenWithClaims, "refresh-1");
            UseTestSession();

            var signIn = TwinAuth.SignInAsync("anna@example.com", "pw");
            yield return WaitFor(signIn);
            AssertSucceeded(signIn);

            var token = TwinAuth.GetAccessTokenAsync();
            yield return WaitFor(token);
            AssertSucceeded(token);

            Assert.AreEqual(1, _server.Requests.Count,
                "The access token was still fresh, so nothing else should have been sent.");
            Assert.AreEqual("/v1/auth/login", _server.Requests[0].Path);
        }

        [UnityTest]
        public IEnumerator Concurrent_callers_share_one_restore_rather_than_racing_it()
        {
            // Two screens asking for a token at launch must produce one refresh,
            // not two. Two would present the same single-use token twice, which
            // the server reads as theft.
            _server.RespondWithTokens(AccessTokenWithClaims, "refresh-2");

            var store = new InMemoryTokenStore();
            store.Save("refresh-1");
            UseTestSession(store);

            var first = TwinAuth.GetAccessTokenAsync();
            var second = TwinAuth.GetAccessTokenAsync();
            var third = TwinAuth.RestoreAsync();

            yield return WaitFor(Task.WhenAll(first, second, third));
            AssertSucceeded(first);
            AssertSucceeded(second);

            Assert.AreEqual(1, _server.Requests.Count, "Exactly one refresh should have been sent.");
            Assert.AreEqual("/v1/auth/refresh", _server.Requests[0].Path);
            Assert.AreEqual("refresh-1", (string)JObject.Parse(_server.Requests[0].Body)["refresh_token"]);
            Assert.AreEqual(AccessTokenWithClaims, first.Result);
            Assert.AreEqual(first.Result, second.Result);
        }

        [UnityTest]
        public IEnumerator With_nothing_stored_the_app_starts_signed_out_without_calling_the_server()
        {
            UseTestSession();

            var restored = TwinAuth.RestoreAsync();
            yield return WaitFor(restored);
            AssertSucceeded(restored);

            Assert.IsFalse(restored.Result);
            Assert.IsFalse(TwinAuth.IsSignedIn);
            Assert.AreEqual(0, _server.Requests.Count, "There was no token to exchange.");
        }

        [UnityTest]
        public IEnumerator A_stored_token_the_server_no_longer_honours_ends_the_session_quietly()
        {
            _server.RespondWithError(401, "INVALID_TOKEN", "no");

            var store = new InMemoryTokenStore();
            store.Save("revoked-elsewhere");
            UseTestSession(store);

            var restored = TwinAuth.RestoreAsync();
            yield return WaitFor(restored);
            AssertSucceeded(restored, "A refused token at startup is an ordinary outcome, not an exception.");

            Assert.IsFalse(restored.Result);
            Assert.IsFalse(TwinAuth.IsSignedIn);
            Assert.IsNull(store.Load(), "The dead token should not be left on disk.");
        }

        /// <summary>
        /// A restore at startup is a best-effort attempt: the app has to come up
        /// signed out rather than throwing, whatever the server said. Only two
        /// failures were ever handled - <c>StatusCode == 0</c> (nothing answered)
        /// and the server's own <c>INVALID_TOKEN</c>/<c>TOKEN_EXPIRED</c> envelope.
        /// Anything else escaped <c>TwinAuth.Start</c>, which is <c>async void</c>,
        /// and surfaced as an unhandled exception on scene load - which broke the
        /// app for anyone offline and failed every PlayMode test with it.
        /// </summary>
        /// <remarks>
        /// 404 is the case seen in practice: a base URL where something answers
        /// but the API is not there - a stale port, a moved route, a proxy or a
        /// captive portal. Note that "no server at all" was never the problem;
        /// a refused connection is status 0 and was handled.
        /// </remarks>
        [UnityTest]
        public IEnumerator A_restore_that_gets_a_404_starts_signed_out_instead_of_throwing()
        {
            _server.Respond(404, "{\"detail\":\"Not Found\"}");

            var store = new InMemoryTokenStore();
            store.Save("token-for-a-server-that-moved");
            UseTestSession(store);

            var restored = TwinAuth.RestoreAsync();
            yield return WaitFor(restored);
            AssertSucceeded(restored, "A 404 at startup must be an ordinary outcome, not an exception");

            Assert.IsFalse(restored.Result);
            Assert.IsFalse(TwinAuth.IsSignedIn);
        }

        /// <summary>The same for a server that is there but broken.</summary>
        [UnityTest]
        public IEnumerator A_restore_that_gets_a_500_starts_signed_out_instead_of_throwing()
        {
            _server.Respond(500, "{\"detail\":\"Internal Server Error\"}");

            var store = new InMemoryTokenStore();
            store.Save("token-the-server-could-not-check");
            UseTestSession(store);

            var restored = TwinAuth.RestoreAsync();
            yield return WaitFor(restored);
            AssertSucceeded(restored, "A 500 at startup must not take the app down with it");

            Assert.IsFalse(restored.Result);
            Assert.IsFalse(TwinAuth.IsSignedIn);
        }

        /// <summary>
        /// And for an answer that is not our envelope at all - the ingress page or
        /// a captive portal's login form, which is HTML, not JSON.
        /// </summary>
        [UnityTest]
        public IEnumerator A_restore_answered_with_something_that_is_not_json_starts_signed_out()
        {
            _server.Respond(200, "<html><body>Sign in to the guest network</body></html>");

            var store = new InMemoryTokenStore();
            store.Save("token-behind-a-captive-portal");
            UseTestSession(store);

            var restored = TwinAuth.RestoreAsync();
            yield return WaitFor(restored);
            AssertSucceeded(restored, "An unreadable answer at startup must not throw");

            Assert.IsFalse(restored.Result);
            Assert.IsFalse(TwinAuth.IsSignedIn);
        }

        /// <summary>
        /// A token that cannot be exchanged is worthless, so it must not be left
        /// behind to fail again on every launch - the same rule the recognised
        /// rejection already follows.
        /// </summary>
        [UnityTest]
        public IEnumerator A_restore_that_fails_does_not_leave_the_token_on_disk()
        {
            _server.Respond(404, "{\"detail\":\"Not Found\"}");

            var store = new InMemoryTokenStore();
            store.Save("token-for-a-server-that-moved");
            UseTestSession(store);

            var restored = TwinAuth.RestoreAsync();
            yield return WaitFor(restored);
            AssertSucceeded(restored);

            Assert.IsNull(store.Load(), "A token that cannot be exchanged should not be kept.");
        }

        // --- helpers -----------------------------------------------------------

        /// <summary>
        /// Point the shared session at the fake server, with a token store that
        /// lives in memory so a test never touches the real PlayerPrefs entry.
        /// </summary>
        private void UseTestSession(ITokenStore store = null)
        {
            var session = new TwinSession(new TwinAuthClient(_config), store ?? new InMemoryTokenStore());
            TwinAuth.OverrideForTests(_config, session);
        }

        private static string[] PropertyNames(JObject body)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var property in body.Properties()) names.Add(property.Name);
            return names.ToArray();
        }

        /// <summary>
        /// Pump the editor until the task finishes. <c>UnityWebRequest</c> only
        /// makes progress while the loop runs, so a plain <c>.Wait()</c> here
        /// would deadlock rather than fail.
        /// </summary>
        private static IEnumerator WaitFor(Task task, int seconds = 20)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail($"The call did not finish within {seconds}s.");
                }

                yield return null;
            }
        }

        private static void AssertSucceeded(Task task, string because = null)
        {
            if (task.IsFaulted)
            {
                var inner = task.Exception?.Flatten().InnerException;
                Assert.Fail($"{because ?? "The call should have succeeded"}, but it threw: {inner}");
            }
        }

        private static TwinAuthException AssertFailed(Task task)
        {
            Assert.IsTrue(task.IsFaulted, "The call should have failed, but it succeeded.");
            var inner = task.Exception?.Flatten().InnerException;
            Assert.IsInstanceOf<TwinAuthException>(inner, $"Expected a TwinAuthException, got: {inner}");
            return (TwinAuthException)inner;
        }
    }
}
