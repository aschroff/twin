using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Code.Net.Twins
{
    /// <summary>
    /// One twin version as it exists on the server.
    /// </summary>
    /// <remarks>
    /// Hand-written against the backend's <c>TwinVersionOut</c> schema, in the
    /// same style as the auth wire types: every JSON name spelled out, because
    /// the server speaks snake_case and a naming strategy is one refactor away
    /// from silently sending the wrong field.
    ///
    /// The two names are the same pair the app already uses locally: a profile
    /// id is <c>"&lt;TwinName&gt;.&lt;Version&gt;"</c>, which splits into
    /// <see cref="TwinName"/> and <see cref="VersionName"/>.
    /// </remarks>
    public sealed class TwinVersionInfo
    {
        [JsonProperty("id")] public string Id { get; set; }

        /// <summary>Case-sensitive, exactly as the app exported it.</summary>
        [JsonProperty("twin_name")] public string TwinName { get; set; }

        /// <summary>The version part alone, e.g. <c>000</c>.</summary>
        [JsonProperty("twin_version")] public string VersionName { get; set; }

        /// <summary>Address of the uploader, as it read at the time of upload.</summary>
        [JsonProperty("uploaded_by_email")] public string UploadedByEmail { get; set; }

        /// <summary>
        /// When the upload completed, in UTC.
        /// </summary>
        /// <remarks>
        /// A <see cref="DateTimeOffset"/> rather than a <see cref="DateTime"/>:
        /// Newtonsoft converts the latter to local time on the way in and the
        /// offset is then lost, which makes "same instant?" comparisons wrong for
        /// anyone not sitting in UTC.
        /// </remarks>
        [JsonProperty("uploaded_at")] public DateTimeOffset UploadedAtUtc { get; set; }

        /// <summary>Size of the ZIP archive in bytes.</summary>
        [JsonProperty("size_bytes")] public long SizeBytes { get; set; }

        /// <summary>Hex SHA-256 of the archive. A download can be verified against it.</summary>
        [JsonProperty("checksum_sha256")] public string ChecksumSha256 { get; set; }

        [JsonProperty("organisation_id")] public string OrganisationId { get; set; }

        /// <summary>
        /// Optimistic concurrency token — send it back on a write. Unrelated to
        /// <see cref="VersionName"/>, which is the twin's version; this one is the
        /// row's.
        /// </summary>
        [JsonProperty("version")] public int ConcurrencyVersion { get; set; }

        /// <summary>The local profile id this corresponds to.</summary>
        public string ProfileId => $"{TwinName}.{VersionName}";
    }

    /// <summary>One page of <see cref="TwinVersionInfo"/>, newest first.</summary>
    public sealed class TwinVersionPage
    {
        [JsonProperty("items")] public List<TwinVersionInfo> Items { get; set; } = new();

        /// <summary>Pass as <c>cursor</c> to fetch the next page. Null was the last.</summary>
        [JsonProperty("next_cursor")] public string NextCursor { get; set; }
    }

    /// <summary>
    /// The <c>code</c> values the twin-version routes produce.
    /// </summary>
    /// <remarks>
    /// Branch on these and never on the message — the backend documents the codes
    /// as contract and the messages as free text.
    /// </remarks>
    public static class TwinApiErrorCodes
    {
        /// <summary>
        /// That name and version pair is already on the server.
        /// </summary>
        /// <remarks>
        /// Not transient: retrying cannot help, and the server will not overwrite
        /// on its own. The way out is a new version, or deleting the existing one.
        /// </remarks>
        public const string AlreadyExists = "TWIN_VERSION_ALREADY_EXISTS";

        public const string NotFound = "TWIN_VERSION_NOT_FOUND";

        /// <summary>The archive is larger than the server accepts.</summary>
        public const string ArchiveTooLarge = "TWIN_ARCHIVE_TOO_LARGE";

        /// <summary>The uploaded file is not a ZIP.</summary>
        public const string ArchiveInvalid = "TWIN_ARCHIVE_INVALID";

        public const string InvalidCursor = "INVALID_CURSOR";
    }

    /// <summary>
    /// Thrown for every failed twin-version call.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>TwinAuthException</c>: the two contexts fail for
    /// different reasons and a caller here branches on upload outcomes, not on
    /// credentials. What they share is the envelope, which is parsed once in
    /// <see cref="TwinVersionsClient"/>.
    /// </remarks>
    public class TwinApiException : Exception
    {
        /// <summary>HTTP status, or 0 when the request never reached the server.</summary>
        public long StatusCode { get; }

        /// <summary>The server's own code, or null — see <see cref="TwinApiErrorCodes"/>.</summary>
        public string ErrorCode { get; }

        public TwinApiException(long statusCode, string errorCode, string message, Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        /// <summary>The server was not reached at all. Retrying may work.</summary>
        public bool IsNetworkFailure => StatusCode == 0;

        /// <summary>This exact version is already up there. Retrying will not help.</summary>
        public bool IsAlreadyExists => ErrorCode == TwinApiErrorCodes.AlreadyExists;

        public bool IsArchiveTooLarge => ErrorCode == TwinApiErrorCodes.ArchiveTooLarge;

        public bool IsArchiveInvalid => ErrorCode == TwinApiErrorCodes.ArchiveInvalid;

        /// <summary>
        /// The session is not usable for this call — expired, revoked, or lacking
        /// the scope. The person has to sign in again.
        /// </summary>
        public bool IsNotAuthorised => StatusCode == 401 || StatusCode == 403;
    }
}
