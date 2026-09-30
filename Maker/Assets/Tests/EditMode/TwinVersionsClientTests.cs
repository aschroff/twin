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
    [Category(Processes.ExchangeTwins)]
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

        // --- downloading ------------------------------------------------------

        [UnityTest]
        public IEnumerator Downloading_asks_for_the_archive_of_one_id_and_signs_the_request()
        {
            yield return SignIn();
            _server.RespondWithArchive(Archive);

            var task = new TwinVersionsClient().DownloadArchiveAsync(VersionId);
            yield return WaitFor(task);
            AssertSucceeded(task);

            var request = _server.LastRequest;
            Assert.AreEqual("GET", request.Method);
            Assert.AreEqual($"/v1/twin-versions/{VersionId}/archive", request.Path,
                "The archive is addressed by the entry's id, not by the name and version pair.");
            StringAssert.StartsWith("Bearer ", request.Authorization);
            Assert.AreEqual("application/zip", request.Accept,
                "This route answers with the ZIP, not with the JSON every other route sends.");
        }

        [UnityTest]
        public IEnumerator Downloading_returns_the_archive_byte_for_byte()
        {
            yield return SignIn();
            _server.RespondWithArchive(Archive);

            var task = new TwinVersionsClient().DownloadArchiveAsync(VersionId);
            yield return WaitFor(task);
            AssertSucceeded(task);

            CollectionAssert.AreEqual(Archive, task.Result,
                "What the import unpacks has to be exactly what was uploaded.");
        }

        [UnityTest]
        public IEnumerator An_archive_that_does_not_match_its_digest_is_refused()
        {
            yield return SignIn();
            // The bytes are fine; the server says they should be something else.
            // A truncated transfer or a proxy that rewrote the body looks like
            // this, and either way it must not reach the twin store.
            _server.RespondWithArchive(Archive, checksum: new string('a', 64));

            var task = new TwinVersionsClient().DownloadArchiveAsync(VersionId);
            yield return WaitFor(task);

            Assert.IsTrue(AssertFailed(task).IsChecksumMismatch,
                "A corrupt archive has to fail the download rather than be imported.");
        }

        [UnityTest]
        public IEnumerator A_server_that_sends_no_digest_is_not_treated_as_a_failure()
        {
            yield return SignIn();
            _server.RespondWithArchive(Archive, checksum: null);

            var task = new TwinVersionsClient().DownloadArchiveAsync(VersionId);
            yield return WaitFor(task);
            AssertSucceeded(task, "The header is a check, not a second authentication");

            CollectionAssert.AreEqual(Archive, task.Result);
        }

        [UnityTest]
        public IEnumerator An_unknown_version_is_reported_as_not_found()
        {
            yield return SignIn();
            _server.RespondWithError(404, "TWIN_VERSION_NOT_FOUND", "no such version");

            var task = new TwinVersionsClient().DownloadArchiveAsync(VersionId);
            yield return WaitFor(task);

            var error = AssertFailed(task);
            Assert.IsTrue(error.IsNotFound);
            Assert.IsFalse(error.IsArchiveNotStored, "The two 404s are different answers to the person.");
        }

        [UnityTest]
        public IEnumerator A_version_whose_archive_is_gone_is_told_apart_from_an_unknown_one()
        {
            yield return SignIn();
            _server.RespondWithError(404, "TWIN_ARCHIVE_NOT_STORED", "deleted");

            var task = new TwinVersionsClient().DownloadArchiveAsync(VersionId);
            yield return WaitFor(task);

            var error = AssertFailed(task);
            Assert.IsTrue(error.IsArchiveNotStored,
                "The row is still listed, so the screen has to explain why it cannot be fetched.");
            Assert.IsFalse(error.IsNotFound);
        }

        [UnityTest]
        public IEnumerator Storage_being_unreachable_is_reported_as_worth_retrying()
        {
            yield return SignIn();
            _server.RespondWithError(503, "SERVICE_UNAVAILABLE", "object storage is down");

            var task = new TwinVersionsClient().DownloadArchiveAsync(VersionId);
            yield return WaitFor(task);

            var error = AssertFailed(task);
            Assert.IsTrue(error.IsServiceUnavailable);
            Assert.IsFalse(error.IsNotFound, "Nothing is missing - the server could not reach its own storage.");
        }

        [UnityTest]
        public IEnumerator A_503_without_the_envelope_still_reads_as_unavailable()
        {
            yield return SignIn();
            // What an ingress returns when the application never sees the request.
            _server.Respond(503, "<html>Service Unavailable</html>");

            var task = new TwinVersionsClient().DownloadArchiveAsync(VersionId);
            yield return WaitFor(task);

            Assert.IsTrue(AssertFailed(task).IsServiceUnavailable,
                "A caller deciding whether to offer 'try again' gets the same answer either way.");
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

        /// <summary>The id the fake server's listing hands out.</summary>
        private const string VersionId = "11111111-1111-1111-1111-111111111111";

        /// <summary>
        /// Stands in for an exported twin. Not a real ZIP: nothing in the client
        /// unpacks it, and what is being checked is that the bytes arrive
        /// unchanged and hash to what the server said.
        /// </summary>
        private static byte[] Archive => Encoding.UTF8.GetBytes("PK\u0003\u0004 pretend this is a twin");

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
