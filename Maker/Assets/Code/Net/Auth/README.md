# Signing in to the Twin Maker API

Everything the Unity client needs to authenticate against the backend, and the
reasoning behind the parts that are not obvious.

The backend lives in a separate repository, `twin-server`. Its documentation is
the source of truth for anything server-side; this file covers only the client.

> **Status.** The backend runs locally and on a local Kubernetes cluster. It is
> **not deployed to AWS yet**, so the production URL does not exist. Everything
> marked 🔴 below is waiting on that. Nothing else is blocked by it — the whole
> login flow can be built and tested against a local backend today.

---

## 1. Five minutes to a working login

**Get a backend running.** In a checkout of `twin-server`:

```bash
just services-start
```

That starts Postgres, Redis and MinIO in Docker, applies the database
migrations, creates a development user and starts the API on
<http://localhost:8000>. `just services-stop` shuts it all down again.

**Check it answers:**

```bash
curl http://localhost:8000/ready
```

Expect `{"status":"ready","database":true,"redis":true}`.

**In Unity**, create the config asset once: `Assets > Create > Twin > API Config`,
put it in a `Resources` folder, name it `TwinApiConfig`, and set **Base Url** to
`http://localhost:8000`.

**Then sign in:**

```csharp
var session = new TwinSession();                       // loads Resources/TwinApiConfig
await session.SignInAsync("anna@example.com", "correct-horse-battery-staple");
Debug.Log($"Signed in as {session.UserId}");
```

Those are the development credentials. They exist because `just services-start`
creates them; there is no sign-up endpoint (see §8).

`LoginPanelExample.cs` is a complete, working login screen. Read it, then write
your own — it has no design and is not meant to ship.

---

## 2. The files

| File | What it is |
|---|---|
| `TwinSession.cs` | **Start here.** The signed-in state of the app. Handles token refresh for you. |
| `TwinAuthClient.cs` | The three HTTP calls, and nothing else. Stateless. |
| `TwinApiConfig.cs` | ScriptableObject: base URL, timeout, logging. |
| `AuthModels.cs` | Wire types, error codes, `TwinAuthException`. |
| `TokenStore.cs` | Where the refresh token is kept between runs. |
| `JwtClaims.cs` | Reads the token payload — **without verifying it**. See §7. |
| `LoginPanelExample.cs` | A worked example. Replace it. |

You normally only touch `TwinSession`. The split exists because the two halves
change for different reasons: `TwinAuthClient` changes when the server's contract
changes, `TwinSession` when the app's idea of "signed in" changes.

---

## 3. Configuration

`TwinApiConfig.BaseUrl` is the **origin only** — no trailing slash, no `/v1`. The
client appends the rest.

| Where the backend runs | Base URL |
|---|---|
| Natively, via `just services-start` | `http://localhost:8000` |
| In the local k3d cluster, via `just k3d-deploy` | `http://localhost` |
| AWS dev | 🔴 not deployed yet |
| AWS prod | 🔴 not deployed yet |

Nothing secret goes in this asset. It ships inside the build and can be read out
of it. The only credentials in this system are the ones a person types in.

---

## 4. Using it

### Sign in

```csharp
try
{
    await session.SignInAsync(email, password);
}
catch (TwinAuthException ex) when (ex.IsInvalidCredentials)
{
    Show("Email address or password is incorrect.");
}
catch (TwinAuthException ex) when (ex.IsNetworkFailure)
{
    Show("Could not reach the server. Check your connection.");
}
```

Login takes a few hundred milliseconds **by design** — the server hashes the
password with argon2, and being slow is the point. Show a spinner and disable the
button; do not shorten the timeout below ~10 seconds.

### Restore on launch

```csharp
if (await session.TryRestoreAsync())
    GoToMainScreen();
else
    GoToLoginScreen();
```

Returns `false` when there was no stored session or the server refused it — both
ordinary outcomes, so neither throws. A **network failure does throw**, because
the session may be perfectly valid and the connection merely absent. Signing
someone out because they opened the app on a train would be the wrong call.

### Authorise a request

```csharp
using var request = UnityWebRequest.Get($"{config.BaseUrl}/v1/twins");
request.SetRequestHeader("Authorization", $"Bearer {await session.GetAccessTokenAsync()}");
```

Call `GetAccessTokenAsync()` immediately before each request rather than caching
the string. That is what makes the refresh invisible: the token lasts 15 minutes,
and this call quietly renews it when it is close to expiring.

### Sign out

```csharp
await session.SignOutAsync();
```

Revokes this device's token only; other devices keep their sessions. The local
session is cleared whether or not the server call succeeds.

### React to a session ending

```csharp
session.SignedOut += () => GoToLoginScreen();
```

Subscribe once, at startup. A session can end at any moment — the refresh token
expired, or someone revoked it from another device — and this is how you find
out. Handling it at each call site means remembering to, every time.

---

## 5. The token model

Two tokens, with different jobs. Getting these confused is the main way this
kind of code goes wrong.

**Access token** — a signed JWT, sent as `Authorization: Bearer <token>` on every
request. Lives **15 minutes**. Cannot be revoked, which is exactly why it is
short-lived. Held in memory only; never written to disk.

**Refresh token** — an opaque string whose only job is to obtain new access
tokens. Lives **30 days**. Can be revoked, because it is a row in the server's
database. Stored between runs (see §6).

Three properties worth internalising:

**It is single-use.** Spending a refresh token returns a new one and kills the
old one. Store the new one *before* discarding the old — if the app dies between
the two, the person signs in again, which is the safe direction to fail in.

**Reuse is treated as theft.** Presenting a refresh token that was already spent
makes the server revoke **every token this user holds, on every device**. That is
deliberate: a token appearing twice means a copy exists somewhere.

**Therefore: never refresh twice at once.** Two screens refreshing simultaneously
would trigger exactly that revocation, against your own user.
`TwinSession` serialises refreshes behind a semaphore, and a second caller awaits
the first one's result instead of starting its own. **If you write your own
session handling, you must do the same.**

---

## 6. Where the refresh token is stored

`PlayerPrefsTokenStore` is the default and is **provisional**. PlayerPrefs is a
plist on iOS and the registry on Windows: unencrypted, readable by anything with
access to the device's file system. A stolen refresh token is a live session for
up to 30 days.

The intended replacement is the platform keychain — Keychain Services on iOS,
Keystore on Android — which needs a native plugin, which is why it is not here
yet. That work is one new implementation of `ITokenStore`; nothing else changes.
🔴

Two things make the current state tolerable meanwhile: the token is revocable
server-side, and it rotates on every use, so a copied token stops working as soon
as the real client refreshes.

`InMemoryTokenStore` is also provided — for tests, and for a "do not remember me"
option:

```csharp
var session = new TwinSession(new TwinAuthClient(config), new InMemoryTokenStore());
```

---

## 7. `JwtClaims` does not verify anything

`JwtClaims.ReadUnverified()` decodes the access token payload so you can show who
is signed in. It does **not** check the signature, and it cannot: verifying needs
the server's signing key, and a client that could verify a token could also mint
one.

So everything it returns is *claimed*, not proven.

- **Fine:** showing the signed-in user, tagging a log line, choosing a start screen.
- **Not fine:** deciding whether someone is allowed to do something.

Every authorisation decision belongs on the server, which verifies the signature
on every request. A client-side check is a convenience for the honest user and no
obstacle at all to a dishonest one.

Available claims: `sub` (user id), `org` (organisation id), `scope`, `exp`. Note
that `org` is a UUID and the API has no endpoint that turns it into a name yet, so
do not put it in front of a user. 🔴

---

## 8. Errors

Everything throws `TwinAuthException`. Branch on the helper properties, not on
the message text — messages are English, unlocalised, and not a contract.

| Property | Means | Do |
|---|---|---|
| `IsInvalidCredentials` | Address or password rejected | Show one generic message, clear the password field |
| `IsSessionEnded` | Refresh token unknown, spent or expired | Send them to the login screen |
| `IsNetworkFailure` | Never reached the server (`StatusCode == 0`) | Offer a retry; do **not** sign out |
| anything else | 5xx, a proxy, a broken deployment | Log the detail, show something generic |

**One message for wrong password and unknown address.** The server answers
identically for both — and takes the same time doing it — so that the login form
cannot be used to discover which addresses have accounts. Do not try to be more
specific in the UI than the server is; there is nothing to be specific with.

Server-side codes, if you need them directly: `INVALID_CREDENTIALS`,
`INVALID_TOKEN`, `TOKEN_EXPIRED`, `VALIDATION_FAILED` — see `TwinAuthErrorCodes`.

Where each one comes from is worth knowing. The three `/v1/auth` endpoints only
ever return `INVALID_CREDENTIALS` (login) or `INVALID_TOKEN` (refresh) —
including for an address that does not exist, which is what stops the form being
an enumeration oracle. `TOKEN_EXPIRED` comes from *other* endpoints, when the
access token you sent them has run out; `GetAccessTokenAsync()` is what keeps you
from ever seeing it.

---

## 9. The wire contract

Three endpoints. Full schema at <http://localhost:8000/docs> when the backend is
running.

```
POST /v1/auth/login     {"email": "...", "password": "..."}      → 200 TokenResponse | 401
POST /v1/auth/refresh   {"refresh_token": "..."}                 → 200 TokenResponse | 401
POST /v1/auth/logout    {"refresh_token": "..."}                 → 204
```

```jsonc
// TokenResponse
{
  "access_token": "eyJ...",     // JWT, send as Bearer
  "refresh_token": "...",       // single-use
  "token_type": "bearer",
  "expires_in": 900,            // seconds
  "scope": "..."                // space-separated, sorted
}

// every error
{ "error": { "code": "INVALID_CREDENTIALS", "message": "...", "details": {} } }
```

These types are hand-written in `AuthModels.cs`. The long-term plan is to
generate them with NSwag from the backend's published `openapi.json` — until that
pipeline exists, three small types are cheaper than the pipeline. If the server
contract changes, `AuthModels.cs` is the file to update. 🔴

---

## 10. What does not exist yet

Do not go looking for these — they are not hidden, they are unbuilt.

| | |
|---|---|
| Sign-up / registration | No endpoint. Users are provisioned during onboarding, deliberately. |
| Password reset | 🔴 Not built. |
| "Current user" endpoint | 🔴 None — hence the raw UUIDs. |
| Apple / Google sign-in | 🔴 Open decision on the server side. |
| Keychain storage | 🔴 See §6. |
| Production URL | 🔴 Nothing deployed to AWS yet. |

Everything above is a server-side decision. Ask before designing UI that assumes
one of them.

**Data routes, on the other hand, do exist.** Uploading and listing twin versions is built and
runs through `TwinAuth.AuthorizeAsync` like any other authenticated call — see
`Assets/Code/Net/Twins/README.md`. Downloading a version back onto the device is not built yet.

---

## 11. Troubleshooting

| Symptom | Cause |
|---|---|
| `Could not reach the Twin API at …` | Backend not running (`just services-start`), or wrong `BaseUrl`. |
| Every request 404s | `BaseUrl` includes `/v1`. It should be the origin only — the client appends the path. |
| HTTP 422 on login | Request body did not match the schema. Almost always a changed JSON property name in `AuthModels.cs`. |
| Signed out unexpectedly on every launch | The refresh token is not being persisted — check that the store is not `InMemoryTokenStore`. |
| Signed out on *all* devices at once | Two refreshes ran concurrently, so the server treated the second as a stolen token. See §5. |
| `No TwinApiConfig found at Resources/TwinApiConfig` | The asset is missing or not in a `Resources` folder. Create it, or pass a config explicitly. |

Turn on **Verbose Logging** in the config asset to see each request and its
status code. It never logs tokens or passwords.
