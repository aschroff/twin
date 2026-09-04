# Twin versions on the server

Uploading a twin version to the backend, and listing what is already up there. Sits on top of
`Code.Net.Auth` — every call here is signed by `TwinAuth.AuthorizeAsync`, which is also what
renews the access token when it is close to expiring.

The backend lives in the separate `twin-server` repository; its OpenAPI document is the source of
truth for the wire format. This file covers the client.

---

## 1. The files

| File | What it is |
|---|---|
| `TwinVersionsClient.cs` | The two calls: list, upload. Stateless. |
| `TwinVersionModels.cs` | The wire types, the error codes, `TwinApiException`. |
| `../../View/Manager/TwinVersionSyncManager.cs` | The screen: merges local and server versions, drives the upload. |
| `../../View/Item/TwinVersionRow.cs` | One row of that screen. |
| `Assets/Tests/EditMode/TwinVersionsClientTests.cs` | Nine tests against a real HTTP server on loopback. |

---

## 2. Using it

```csharp
var client = new TwinVersionsClient();

// Everything the server has for this twin, newest first.
List<TwinVersionInfo> versions = await client.ListAllAsync("LipEdema");

// One version up. The archive is the zip the app's export produces.
string zip = DataPersistenceManager.instance.ExportZipForVersion("LipEdema.001");
TwinVersionInfo uploaded = await client.UploadAsync("LipEdema", "001", File.ReadAllBytes(zip));
```

A twin's identity on the server is the same pair the app uses on disk: a profile id is
`<name>.<version>`, which is exactly `twin_name` and `twin_version`.

---

## 3. The one error that is not a retry

| `TwinApiException` | Means | What to do |
|---|---|---|
| `IsAlreadyExists` | That name and version are already up there | **Not transient.** Nothing the server does will free the pair. Bump the version, or delete the existing entry. |
| `IsArchiveTooLarge` | Over the server's size ceiling | Tell the person. Retrying is pointless. |
| `IsArchiveInvalid` | The upload was not a ZIP | A bug on this side. |
| `IsNotAuthorised` | 401/403 — expired, revoked, or missing scope | Send them to sign in again. |
| `IsNetworkFailure` | Never reached the server (`StatusCode == 0`) | Retrying may work. |

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

`GET /{id}/archive` (download) and `DELETE` exist on the server and are deliberately absent from
this client: an unused method is a contract nobody tested. Add them with tests when the reading
side is built.

The upload sends the whole archive as one multipart body held in memory. That is fine for the
twins seen so far; a resumable or streamed upload is the thing to reach for when it is not.
