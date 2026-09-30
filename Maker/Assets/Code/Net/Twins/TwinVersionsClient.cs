using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Code.Net.Auth;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Net.Twins
{
    /// <summary>
    /// The <c>/v1/twin-versions</c> routes: what is on the server, and putting
    /// one more up there.
    /// </summary>
    /// <remarks>
    /// <para>Stateless, like <c>TwinAuthClient</c>: it makes calls and returns
    /// what came back. It does not decide what to upload, does not remember what
    /// it uploaded, and holds no session — every request is signed by
    /// <see cref="TwinAuth.AuthorizeAsync"/>, which is also what renews the
    /// access token when it is close to expiring.</para>
    ///
    /// <para>Delete exists on the server and is deliberately not here: nothing in
    /// the app offers it, and an unused method is a contract nobody tested.</para>
    /// </remarks>
    public class TwinVersionsClient
    {
        private const string BasePath = "/v1/twin-versions";

        /// <summary>Reading metadata is small and quick.</summary>
        private const int ListTimeoutSeconds = 30;

        /// <summary>
        /// Uploads are not. An archive is tens of megabytes over whatever
        /// connection the tablet has, and the server hashes it while it arrives,
        /// so the read timeout has to cover the whole transfer rather than a
        /// round trip.
        /// </summary>
        private const int UploadTimeoutSeconds = 600;

        /// <summary>
        /// A download is the same archive coming the other way, so it gets the
        /// same budget as an upload rather than the list's.
        /// </summary>
        private const int DownloadTimeoutSeconds = 600;

        /// <summary>
        /// The header the server repeats the archive's digest in, so verifying
        /// what arrived does not need a second request.
        /// </summary>
        private const string ChecksumHeader = "X-Checksum-SHA256";

        /// <summary>
        /// One page of the versions of one twin, newest first.
        /// </summary>
        /// <param name="twinName">Case-sensitive, exactly as the app exported it.</param>
        /// <param name="cursor">From a previous page's <c>NextCursor</c>, or null to start.</param>
        /// <param name="limit">Server default when 0.</param>
        public Task<TwinVersionPage> ListAsync(string twinName, string cursor = null, int limit = 0)
        {
            if (string.IsNullOrWhiteSpace(twinName))
            {
                throw new ArgumentException("Twin name must not be empty.", nameof(twinName));
            }

            var query = new StringBuilder("?twin_name=").Append(UnityWebRequest.EscapeURL(twinName));
            if (!string.IsNullOrEmpty(cursor)) query.Append("&cursor=").Append(UnityWebRequest.EscapeURL(cursor));
            if (limit > 0) query.Append("&limit=").Append(limit);

            return SendAsync<TwinVersionPage>(
                () => UnityWebRequest.Get(Url(BasePath + query)),
                ListTimeoutSeconds,
                "list twin versions");
        }

        /// <summary>
        /// Every version of one twin, following the cursors.
        /// </summary>
        /// <remarks>
        /// The list this feeds is one twin's versions — tens of rows, not
        /// thousands — so reading it whole is honest here. <paramref name="maxPages"/>
        /// exists so a server that kept handing out cursors could not spin this
        /// forever.
        /// </remarks>
        public async Task<List<TwinVersionInfo>> ListAllAsync(string twinName, int maxPages = 20)
        {
            var all = new List<TwinVersionInfo>();
            string cursor = null;

            for (var page = 0; page < maxPages; page++)
            {
                var result = await ListAsync(twinName, cursor);
                if (result?.Items != null) all.AddRange(result.Items);

                cursor = result?.NextCursor;
                if (string.IsNullOrEmpty(cursor)) break;
            }

            return all;
        }

        /// <summary>
        /// Put one version's archive on the server.
        /// </summary>
        /// <param name="archive">
        /// The ZIP the app's export produces — see <c>FileDataHandler</c>. Passed
        /// as bytes rather than a path because that is what a multipart section
        /// takes; the caller reads the file it just wrote.
        /// </param>
        /// <exception cref="TwinApiException">
        /// <c>IsAlreadyExists</c> when that name and version are already up there
        /// — a permanent answer, not a reason to retry. <c>IsArchiveTooLarge</c>,
        /// <c>IsArchiveInvalid</c>, <c>IsNotAuthorised</c>, or
        /// <c>IsNetworkFailure</c> for the rest.
        /// </exception>
        public Task<TwinVersionInfo> UploadAsync(string twinName, string versionName, byte[] archive)
        {
            if (string.IsNullOrWhiteSpace(twinName))
                throw new ArgumentException("Twin name must not be empty.", nameof(twinName));
            if (string.IsNullOrWhiteSpace(versionName))
                throw new ArgumentException("Version must not be empty.", nameof(versionName));
            if (archive == null || archive.Length == 0)
                throw new ArgumentException("Archive must not be empty.", nameof(archive));

            var sections = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("twin_name", twinName),
                new MultipartFormDataSection("twin_version", versionName),
                // The content type is checked by the server, which rejects
                // anything that is not a ZIP with its own error code.
                new MultipartFormFileSection("archive", archive, $"{twinName}.{versionName}.zip", "application/zip"),
            };

            // Post(url, sections) builds the boundary itself. Composing the body
            // by hand is the classic way to end up with a 422 that says nothing.
            return SendAsync<TwinVersionInfo>(
                () => UnityWebRequest.Post(Url(BasePath), sections),
                UploadTimeoutSeconds,
                "upload twin version");
        }

        /// <summary>
        /// Fetch one version's archive, verified against the digest the server
        /// sent with it.
        /// </summary>
        /// <param name="twinVersionId">
        /// <see cref="TwinVersionInfo.Id"/> from a listing. The server addresses
        /// an archive by id and not by name and version: the pair is a filter
        /// that can match nothing, the id is the entry.
        /// </param>
        /// <returns>The ZIP, ready to hand to the app's import.</returns>
        /// <exception cref="TwinApiException">
        /// <c>IsNotFound</c> when there is no such version and
        /// <c>IsArchiveNotStored</c> when the entry is listed but its bytes are
        /// gone — both permanent. <c>IsServiceUnavailable</c> when the server
        /// could not reach its storage, which is worth retrying.
        /// <c>IsChecksumMismatch</c> when what arrived is not what the server
        /// says it sent. Plus <c>IsNotAuthorised</c> and <c>IsNetworkFailure</c>
        /// as everywhere else.
        /// </exception>
        public Task<byte[]> DownloadArchiveAsync(string twinVersionId)
        {
            if (string.IsNullOrWhiteSpace(twinVersionId))
            {
                throw new ArgumentException("Twin version id must not be empty.", nameof(twinVersionId));
            }

            var path = BasePath + "/" + UnityWebRequest.EscapeURL(twinVersionId) + "/archive";

            return SendAsync(
                () => UnityWebRequest.Get(Url(path)),
                DownloadTimeoutSeconds,
                "download twin version archive",
                // Not JSON: this route answers with the ZIP itself. The default
                // DownloadHandlerBuffer is what holds it, which is the same
                // whole-archive-in-memory trade the upload makes.
                ArchiveOnly,
                accept: ArchiveContentType);
        }

        /// <summary>The media type the archive route answers with.</summary>
        private const string ArchiveContentType = "application/zip";

        /// <summary>
        /// Take the bytes out of a finished download, refusing them when they do
        /// not match the digest that came with them.
        /// </summary>
        /// <remarks>
        /// Verified here rather than by the caller, because a caller that forgot
        /// would write a corrupt ZIP into the twin store and find out later. A
        /// server that sends no digest is not treated as a failure — the check is
        /// what the header is for, not a second authentication.
        /// </remarks>
        private static byte[] ArchiveOnly(UnityWebRequest request)
        {
            byte[] bytes = request.downloadHandler?.data;
            if (bytes == null || bytes.Length == 0)
            {
                throw new TwinApiException(
                    request.responseCode, null, "The server answered the download with an empty body.");
            }

            string expected = request.GetResponseHeader(ChecksumHeader);
            if (string.IsNullOrWhiteSpace(expected)) return bytes;

            string actual = Sha256Hex(bytes);
            if (!string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new TwinApiException(
                    request.responseCode,
                    TwinApiErrorCodes.ChecksumMismatch,
                    $"The downloaded archive does not match the server's digest " +
                    $"(expected {expected}, got {actual}).");
            }

            return bytes;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);

            var text = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) text.Append(b.ToString("x2"));
            return text.ToString();
        }

        private static string Url(string pathAndQuery) => TwinAuth.BaseUrl + pathAndQuery;

        /// <summary>
        /// Send a request that is signed, timed out, and whose failures are turned
        /// into <see cref="TwinApiException"/>.
        /// </summary>
        /// <remarks>
        /// The request is built by a factory rather than passed in, so it is
        /// created after the token has been fetched and cannot sit around while
        /// that await runs.
        /// </remarks>
        private static Task<T> SendAsync<T>(Func<UnityWebRequest> build, int timeoutSeconds, string operation)
            where T : class
        {
            return SendAsync(
                build,
                timeoutSeconds,
                operation,
                request => ReadJson<T>(request, operation),
                accept: "application/json");
        }

        /// <summary>
        /// The same request, for a route that does not answer with JSON.
        /// </summary>
        /// <param name="read">
        /// Turns the finished, successful request into the result. Runs inside
        /// the completion handler, so a <see cref="TwinApiException"/> it throws
        /// reaches the caller unchanged — which is how the checksum check reports
        /// itself.
        /// </param>
        /// <param name="accept">
        /// What this route answers with. Sent rather than assumed, because the
        /// error path answers with the JSON envelope either way and a server is
        /// entitled to hold us to what we asked for.
        /// </param>
        private static async Task<T> SendAsync<T>(
            Func<UnityWebRequest> build,
            int timeoutSeconds,
            string operation,
            Func<UnityWebRequest, T> read,
            string accept)
        {
            using var request = build();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Accept", accept);

            // Throws when nobody is signed in, which is a programming error here:
            // the UI is expected to have checked before offering the action.
            await TwinAuth.AuthorizeAsync(request);

            var tcs = new TaskCompletionSource<T>();

            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    var status = request.responseCode;

                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        tcs.SetException(new TwinApiException(
                            0, null,
                            $"Could not reach the Twin API at {TwinAuth.BaseUrl} ({operation}): {request.error}."));
                        return;
                    }

                    if (status >= 400)
                    {
                        // The envelope is JSON on every route, including the one
                        // whose success body is a ZIP.
                        tcs.SetException(BuildApiException(status, request.downloadHandler?.text, operation));
                        return;
                    }

                    tcs.SetResult(read(request));
                }
                catch (TwinApiException ex)
                {
                    // Raised by `read` itself - a body that did not parse, or an
                    // archive that did not match its digest. It already says what
                    // went wrong and carries the code a caller branches on, so
                    // wrapping it would only bury both.
                    tcs.SetException(ex);
                }
                catch (Exception ex)
                {
                    tcs.SetException(new TwinApiException(
                        request.responseCode, null, $"Unexpected failure handling the {operation} response.", ex));
                }
            };

            return await tcs.Task;
        }

        /// <summary>
        /// Read a successful response as <typeparamref name="T"/>.
        /// </summary>
        private static T ReadJson<T>(UnityWebRequest request, string operation) where T : class
        {
            var parsed = JsonConvert.DeserializeObject<T>(request.downloadHandler?.text);
            if (parsed == null)
            {
                throw new TwinApiException(
                    request.responseCode, null, $"Could not read the {operation} response as {typeof(T).Name}.");
            }

            return parsed;
        }

        /// <summary>
        /// Prefer the server's own code over anything inferred from the status.
        /// </summary>
        private static TwinApiException BuildApiException(long status, string responseText, string operation)
        {
            string code = null;
            string message = null;

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    var envelope = JsonConvert.DeserializeObject<ApiErrorResponse>(responseText);
                    code = envelope?.Error?.Code;
                    message = envelope?.Error?.Message;
                }
                catch (JsonException)
                {
                    // Not our envelope — an ingress 502, a proxy, a captive
                    // portal. The generic message below is the honest one.
                    Debug.Log($"[{nameof(TwinVersionsClient)}] HTTP {status} with a body that is not the API envelope ({operation}).");
                }
            }

            return new TwinApiException(status, code, message ?? $"The {operation} request failed with HTTP {status}.");
        }
    }
}
