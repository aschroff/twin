using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Code.Net.Auth;
using Code.Net.Twins;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace EditModeTests
{
    /// <summary>
    /// What the twin-version client puts on the wire, checked against a real HTTP
    /// server on loopback.
    /// </summary>
    /// <remarks>
    /// <para>Same reasoning as <see cref="TwinAuthTests"/>: the ways this client
    /// can be wrong are invisible from inside Unity. A missing <c>twin_name</c>
    /// filter shows every twin's versions rather than this one's; a body that is
    /// not multipart is a 422; a form field spelled differently is a 422 that
    /// names a field the client believes it sent.</para>
    ///
    /// <para>The expected shapes come from the backend's own schemas
    /// (<c>contexts/twins/schemas.py</c>) and its documented error codes
    /// (<c>contexts/twins/exceptions.py</c>), which the backend states are
    /// contract.</para>
    /// </remarks>
    public class TwinVersionsClientTests
    {
        private FakeTwinApiServer _server;
        private TwinApiConfig _config;

        /// <summary>A JWT whose payload is {"sub":"user-1","org":"org-1","exp":9999999999}.</summary>
        private const string AccessToken =
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
            TwinAuth.OverrideForTests(null, null);
            _server?.Dispose();
        }

        // --- listing ----------------------------------------------------------

        [UnityTest]
        public IEnumerator Listing_asks_for_one_twin_and_signs_the_request()
        {
            yield return SignIn();
            _server.Respond(200, "{\"items\":[],\"next_cursor\":null}");

            var task = new TwinVersionsClient().ListAsync("Anna Beispiel");
            yield return WaitFor(task);
            AssertSucceeded(task);

            var request = _server.LastRequest;
            Assert.AreEqual("GET", request.Method);
            Assert.AreEqual("/v1/twin-versions", request.Path);
            StringAssert.Contains("twin_name=", request.Query,
                "Without the filter this lists every twin in the organisation, not this one.");
            StringAssert.StartsWith("Bearer ", request.Authorization,
                "Every twin-version route requires a token.");
        }

        [UnityTest]
        public IEnumerator Listing_reads_the_fields_the_list_shows()
        {
            yield return SignIn();
            _server.Respond(200, OnePageJson("2026-09-03T17:42:11Z"));

            var task = new TwinVersionsClient().ListAsync("Anna Beispiel");
            yield return WaitFor(task);
            AssertSucceeded(task);

            var item = task.Result.Items[0];
            Assert.AreEqual("Anna Beispiel", item.TwinName);
            Assert.AreEqual("000", item.VersionName);
            Assert.AreEqual("anna@example.com", item.UploadedByEmail, "Shown on an uploaded row.");
            Assert.AreEqual(4096, item.SizeBytes);
            Assert.AreEqual("Anna Beispiel.000", item.ProfileId,
                "This is what a local version is keyed by, so the two lists can be merged.");
        }

        [UnityTest]
        public IEnumerator An_upload_time_keeps_its_instant_rather_than_becoming_local()
        {
            // A DateTime would be silently converted to the machine's local zone
            // by Newtonsoft, and every displayed time would be wrong by the
            // offset — invisible in UTC+0 and wrong everywhere else.
            yield return SignIn();
            _server.Respond(200, OnePageJson("2026-09-03T17:42:11Z"));

            var task = new TwinVersionsClient().ListAsync("Anna Beispiel");
            yield return WaitFor(task);
            AssertSucceeded(task);

            Assert.AreEqual(
                new DateTimeOffset(2026, 9, 3, 17, 42, 11, TimeSpan.Zero),
                task.Result.Items[0].UploadedAtUtc.ToUniversalTime());
        }

        [UnityTest]
        public IEnumerator Listing_everything_follows_the_cursor_until_it_runs_out()
        {
            yield return SignIn();
            _server.Respond(200, "{\"items\":[" + ItemJson("000", "2026-09-03T17:42:11Z") + "],\"next_cursor\":\"page-2\"}");
            _server.Respond(200, "{\"items\":[" + ItemJson("001", "2026-09-03T18:00:00Z") + "],\"next_cursor\":null}");

            var task = new TwinVersionsClient().ListAllAsync("Anna Beispiel");
            yield return WaitFor(task);
            AssertSucceeded(task);

            Assert.AreEqual(2, task.Result.Count, "Both pages, not just the first.");
            StringAssert.Contains("cursor=page-2", _server.LastRequest.Query,
                "The second request has to carry the cursor the first page handed back.");
        }

        // --- upload -----------------------------------------------------------

        [UnityTest]
        public IEnumerator Upload_posts_the_three_form_fields_as_multipart()
        {
            yield return SignIn();
            _server.Respond(201, ItemJson("000", "2026-09-03T17:42:11Z"));

            var task = new TwinVersionsClient().UploadAsync("Anna Beispiel", "000", Encoding.UTF8.GetBytes("PK-not-really"));
            yield return WaitFor(task);
            AssertSucceeded(task);

            var request = _server.LastRequest;
            Assert.AreEqual("POST", request.Method);
            Assert.AreEqual("/v1/twin-versions", request.Path);
            StringAssert.StartsWith("multipart/form-data", request.ContentType,
                "A JSON body is answered with a 422: the route takes Form and File parameters.");

            // The names are the server's parameter names. Spelling one differently
            // is a 422 that names a field the client thinks it sent.
            StringAssert.Contains("name=\"twin_name\"", request.Body);
            StringAssert.Contains("name=\"twin_version\"", request.Body);
            StringAssert.Contains("name=\"archive\"", request.Body);
            StringAssert.Contains("Anna Beispiel", request.Body);
        }

        [UnityTest]
        public IEnumerator Uploading_a_version_that_is_already_there_is_permanent_not_transient()
        {
            // The one error the upload endpoint exists to produce. The UI must not
            // offer a retry for it — the pair will never be free again.
            yield return SignIn();
            _server.RespondWithError(409, "TWIN_VERSION_ALREADY_EXISTS", "already uploaded");

            var task = new TwinVersionsClient().UploadAsync("Anna Beispiel", "000", Encoding.UTF8.GetBytes("zip"));
            yield return WaitFor(task);

            var error = AssertFailed(task);
            Assert.IsTrue(error.IsAlreadyExists);
            Assert.IsFalse(error.IsNetworkFailure, "Retrying this one cannot help.");
        }

        [UnityTest]
        public IEnumerator An_oversized_archive_is_told_apart_from_other_failures()
        {
            yield return SignIn();
            _server.RespondWithError(413, "TWIN_ARCHIVE_TOO_LARGE", "too large");

            var task = new TwinVersionsClient().UploadAsync("Anna Beispiel", "000", Encoding.UTF8.GetBytes("zip"));
            yield return WaitFor(task);

            Assert.IsTrue(AssertFailed(task).IsArchiveTooLarge);
        }

        [UnityTest]
        public IEnumerator A_refused_token_is_reported_as_not_authorised()
        {
            yield return SignIn();
            _server.RespondWithError(401, "TOKEN_EXPIRED", "expired");

            var task = new TwinVersionsClient().ListAsync("Anna Beispiel");
            yield return WaitFor(task);

            Assert.IsTrue(AssertFailed(task).IsNotAuthorised);
        }

        [UnityTest]
        public IEnumerator An_unreachable_server_is_a_network_failure_not_a_rejection()
        {
            yield return SignIn();
            _server.Dispose();

            var task = new TwinVersionsClient().ListAsync("Anna Beispiel");
            yield return WaitFor(task, seconds: 30);

            var error = AssertFailed(task);
            Assert.IsTrue(error.IsNetworkFailure);
            Assert.AreEqual(0, error.StatusCode, "There is no HTTP status when nothing answered.");
        }

        // --- helpers ----------------------------------------------------------

        /// <summary>
        /// Put a signed-in session in place, so <c>AuthorizeAsync</c> has a token
        /// to attach. The login response is the first canned one.
        /// </summary>
        private IEnumerator SignIn()
        {
            _server.RespondWithTokens(AccessToken, "refresh-1");
            TwinAuth.OverrideForTests(_config, new TwinSession(new TwinAuthClient(_config), new InMemoryTokenStore()));

            var task = TwinAuth.SignInAsync("anna@example.com", "pw");
            yield return WaitFor(task);
            AssertSucceeded(task, "The test's own sign-in should succeed");
        }

        private static string ItemJson(string version, string uploadedAt) =>
            "{\"id\":\"11111111-1111-1111-1111-111111111111\"," +
            "\"twin_name\":\"Anna Beispiel\",\"twin_version\":\"" + version + "\"," +
            "\"uploaded_by_email\":\"anna@example.com\",\"uploaded_at\":\"" + uploadedAt + "\"," +
            "\"size_bytes\":4096,\"checksum_sha256\":\"abc\",\"organisation_id\":\"org-1\",\"version\":1}";

        private static string OnePageJson(string uploadedAt) =>
            "{\"items\":[" + ItemJson("000", uploadedAt) + "],\"next_cursor\":null}";

        private static IEnumerator WaitFor(Task task, int seconds = 20)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) Assert.Fail($"The call did not finish within {seconds}s.");
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

        private static TwinApiException AssertFailed(Task task)
        {
            Assert.IsTrue(task.IsFaulted, "The call should have failed, but it succeeded.");
            var inner = task.Exception?.Flatten().InnerException;
            Assert.IsInstanceOf<TwinApiException>(inner, $"Expected a TwinApiException, got: {inner}");
            return (TwinApiException)inner;
        }
    }
}
