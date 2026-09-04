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
    /// <para>Download and delete exist on the server but are not here yet: this
    /// ticket displays server-only versions and uploads local ones, and an unused
    /// method is a contract nobody tested.</para>
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
        private static async Task<T> SendAsync<T>(Func<UnityWebRequest> build, int timeoutSeconds, string operation)
            where T : class
        {
            using var request = build();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");

            // Throws when nobody is signed in, which is a programming error here:
            // the UI is expected to have checked before offering the action.
            await TwinAuth.AuthorizeAsync(request);

            var tcs = new TaskCompletionSource<T>();

            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    var status = request.responseCode;
                    var responseText = request.downloadHandler?.text;

                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        tcs.SetException(new TwinApiException(
                            0, null,
                            $"Could not reach the Twin API at {TwinAuth.BaseUrl} ({operation}): {request.error}."));
                        return;
                    }

                    if (status >= 400)
                    {
                        tcs.SetException(BuildApiException(status, responseText, operation));
                        return;
                    }

                    var parsed = JsonConvert.DeserializeObject<T>(responseText);
                    if (parsed == null)
                    {
                        tcs.SetException(new TwinApiException(
                            status, null, $"Could not read the {operation} response as {typeof(T).Name}."));
                        return;
                    }

                    tcs.SetResult(parsed);
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
