# Twin versions on the server

Uploading a twin version to the backend, listing what is already up there, and fetching one back
down. Sits on top of `Code.Net.Auth` — every call here is signed by `TwinAuth.AuthorizeAsync`,
which is also what renews the access token when it is close to expiring.

The backend lives in the separate `twin-server` repository; its OpenAPI document is the source of
truth for the wire format. This file covers the client.

---

## 1. The files

| File | What it is |
|---|---|
| `TwinVersionsClient.cs` | The three calls: list, upload, download. Stateless. |
| `TwinVersionModels.cs` | The wire types, the error codes, `TwinApiException`. |
| `../../View/Manager/TwinVersionSyncManager.cs` | The upload screen: merges local and server versions, drives the upload. |
| `../../View/Manager/TwinVersionDownloadManager.cs` | The download screen. Same merge, opposite direction. |
| `../../View/Item/TwinVersionRow.cs` | One row of either screen. Its `Role` says which way round to read a state. |
| `Assets/Tests/EditMode/TwinVersionsClientTests.cs` | Seventeen tests against a real HTTP server on loopback. |
| `Assets/Tests/EditMode/TwinVersionRowTests.cs` | Nine tests for which row can be ticked on which screen. |

---

## 2. Using it

```csharp
var client = new TwinVersionsClient();

// Everything the server has for this twin, newest first.
List<TwinVersionInfo> versions = await client.ListAllAsync("LipEdema");

// One version up. The archive is the zip the app's export produces.
string zip = DataPersistenceManager.instance.ExportZipForVersion("LipEdema.001");
TwinVersionInfo uploaded = await client.UploadAsync("LipEdema", "001", File.ReadAllBytes(zip));

// One version down. Addressed by id - the name and version pair is a filter that can match
// nothing, the id is the entry. The bytes are already checked against the server's digest.
byte[] archive = await client.DownloadArchiveAsync(versions[0].Id);
File.WriteAllBytes(path, archive);
DataPersistenceManager.instance.ImportConfig(path);
```

A twin's identity on the server is the same pair the app uses on disk: a profile id is
`<name>.<version>`, which is exactly `twin_name` and `twin_version`.

---

## 3. Which errors are worth retrying, and which are not

| `TwinApiException` | Means | What to do |
|---|---|---|
| `IsAlreadyExists` | That name and version are already up there | **Not transient.** Nothing the server does will free the pair. Bump the version, or delete the existing entry. |
| `IsArchiveTooLarge` | Over the server's size ceiling | Tell the person. Retrying is pointless. |
| `IsArchiveInvalid` | The upload was not a ZIP | A bug on this side. |
| `IsNotAuthorised` | 401/403 — expired, revoked, or missing scope | Send them to sign in again. |
| `IsNetworkFailure` | Never reached the server (`StatusCode == 0`) | Retrying may work. |
| `IsNotFound` | No such version | **Not transient.** The listing is stale; refresh it. |
| `IsArchiveNotStored` | The entry is listed, its bytes are not there | **Not transient.** A deleted version looks like this. Only a fresh upload brings it back. |
| `IsServiceUnavailable` | 503 — the server cannot reach its own object storage | Retrying is the right response. Also true for a 503 with no envelope. |
| `IsChecksumMismatch` | **Raised here, not by the server.** What arrived does not match `X-Checksum-SHA256` | A truncated transfer or a proxy that rewrote the body. The archive must not be imported. |

Branch on these flags and never on the message: the backend documents the `code` values as
contract and the messages as free text.

---

## 4. Two decisions worth knowing

**A version already on the server cannot be selected for upload.** Not tidiness — the duplicate is
refused permanently, so the tick would promise something that cannot happen. The row shows a
ticked, disabled box instead, which reads as "done" rather than as an offer.

**A version that is not the twin currently open is packed without saving first.**
`FileDataHandler.ExportZip` saves before packing, which is right for the open twin — its painted
texture and config only exist in memory until then. For any other version it would rewrite the
file and move its modification time, and `GetMostRecentlyUpdatedProfileId` uses exactly that to
decide which twin the app opens next. Uploading an old version would silently change what comes
back on the next start. `ExportZipForVersion` routes around it via `CompressExisting`.

---

## 5. What does not exist yet

`DELETE` exists on the server and is deliberately absent from this client: nothing in the app
offers it, and an unused method is a contract nobody tested.

Both directions hold the whole archive in memory — the upload as one multipart body, the download
as one `DownloadHandlerBuffer`. That is fine for the twins seen so far; a resumable or streamed
transfer is the thing to reach for when it is not.

**Deletion on the server is not restricted to the uploader, or to their organisation.** Any
signed-in user can delete any uploaded version — the backend pins this in
`test_a_user_in_another_organisation_can_delete_the_upload`. Nothing in this app can trigger it,
but it is the reason the client has no delete rather than an accident.
