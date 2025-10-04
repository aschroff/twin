using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Code.Proc.Meshcapade
{

    public class MeshcapadeAvatarClient : MonoBehaviour
    {
        private readonly string apiBaseUrl = "https://api.meshcapade.com/api/v1";

        // Token / auth endpoint (from article)
        private readonly string tokenUrl =
            "https://auth.meshcapade.com/realms/meshcapade-me/protocol/openid-connect/token";

        // You should supply these (or inject them)
        public string username="andreas@schroff-online.com";
        public string password="Maieutec2019#";

        public string clientId = "meshcapade-me";

        // If the client requires a client_secret, add that too:
        public string clientSecret = null; // or set it

        // --- Entry point exposed to caller ---
        public void CreateAvatarFromImage(
            Func<IEnumerator> getImageBytesCoroutine,
            Action<AvatarResult> onComplete,
            Action<string> onError)
        {
            StartCoroutine(CreateAvatarFromImageRoutine(getImageBytesCoroutine, onComplete, onError));
        }

        // --- Main coroutine that drives the flow ---
        private IEnumerator CreateAvatarFromImageRoutine(
            Func<IEnumerator> getImageBytesCoroutine,
            Action<AvatarResult> onComplete,
            Action<string> onError)
        {
            // 1. Get access token
            string bearerToken = null;
            yield return StartCoroutine(GetAccessTokenCoroutine(
                t => bearerToken = t,
                onError));
            if (string.IsNullOrEmpty(bearerToken))
            {
                yield break;
            }

            // 2. Create avatar (init)
            string avatarId = null;
            yield return StartCoroutine(CreateAvatarRequest(bearerToken, id => avatarId = id, onError));
            if (string.IsNullOrEmpty(avatarId))
            {
                yield break;
            }

            // 3. Register image upload
            UploadInfo uploadInfo = null;
            yield return StartCoroutine(RegisterImageRequest(avatarId, bearerToken, info => uploadInfo = info,
                onError));
            if (uploadInfo == null)
            {
                yield break;
            }

            // 4. Get image bytes
            byte[] imageBytes = null;
            yield return StartCoroutine(getImageBytesCoroutine().Wrap(obj => imageBytes = obj as byte[]));
            if (imageBytes == null)
            {
                onError?.Invoke("Image bytes were null");
                yield break;
            }

            // 5. Upload image
            yield return StartCoroutine(UploadImageBytes(uploadInfo, imageBytes, onError));

            // 6. Trigger fit-to-images
            yield return StartCoroutine(TriggerFitToImagesRequest(avatarId, bearerToken, onError));

            // 7. Poll for completion
            AvatarResult result = null;
            yield return StartCoroutine(PollUntilReadyRequest(avatarId, bearerToken, r => result = r, onError));
            if (result == null)
            {
                yield break;
            }

            // Success
            onComplete?.Invoke(result);
        }

        // --- Token retrieval ---  
        public IEnumerator GetAccessTokenCoroutine(Action<string> onToken, Action<string> onError)
        {
            // Prepare form data
            WWWForm form = new WWWForm();
            form.AddField("grant_type", "password");
            form.AddField("client_id", clientId);
            if (!string.IsNullOrEmpty(clientSecret))
            {
                form.AddField("client_secret", clientSecret);
            }

            form.AddField("username", username);
            form.AddField("password", password);

            using (UnityWebRequest req = UnityWebRequest.Post(tokenUrl, form))
            {
                req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Token request failed: {req.error}, code: {req.responseCode}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                try
                {
                    TokenResponse tr = JsonConvert.DeserializeObject<TokenResponse>(json);
                    if (!string.IsNullOrEmpty(tr.access_token))
                    {
                        // Prepend “Bearer ”
                        string bearer = "Bearer " + tr.access_token;
                        onToken?.Invoke(bearer);
                    }
                    else
                    {
                        onError?.Invoke("Token response did not include access_token");
                    }
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Token JSON parse error: {e.Message}");
                }
            }
        }

        // --- API calls (same as before, using bearerToken) ---  
        private IEnumerator CreateAvatarRequest(string bearerToken, Action<string> onAvatarId, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/avatars/create/from-images";
            using (UnityWebRequest req = UnityWebRequest.PostWwwForm(url, ""))
            {
                req.SetRequestHeader("Authorization", bearerToken);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Create avatar error: {req.error}, code: {req.responseCode}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (dict.TryGetValue("id", out object idObj))
                    {
                        onAvatarId?.Invoke(idObj.ToString());
                    }
                    else
                    {
                        onError?.Invoke("CreateAvatar: JSON missing `id` field");
                    }
                }
                catch (Exception e)
                {
                    onError?.Invoke($"CreateAvatar JSON parse error: {e.Message}");
                }
            }
        }

        private IEnumerator RegisterImageRequest(string avatarId, string bearerToken, Action<UploadInfo> onUploadInfo,
            Action<string> onError)
        {
            string url = $"{apiBaseUrl}/avatars/{avatarId}/images";
            using (UnityWebRequest req = UnityWebRequest.PostWwwForm(url, ""))
            {
                req.SetRequestHeader("Authorization", bearerToken);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Register image error: {req.error}, code: {req.responseCode}");
                    yield break;
                }

                try
                {
                    UploadInfo ui = JsonConvert.DeserializeObject<UploadInfo>(req.downloadHandler.text);
                    onUploadInfo?.Invoke(ui);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"RegisterImage JSON parse error: {e.Message}");
                }
            }
        }

        private IEnumerator UploadImageBytes(UploadInfo uploadInfo, byte[] imageBytes, Action<string> onError)
        {
            using (UnityWebRequest req = UnityWebRequest.Put(uploadInfo.uploadUrl, imageBytes))
            {
                req.SetRequestHeader("Content-Type", uploadInfo.contentType ?? "image/jpeg");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Upload image error: {req.error}, code: {req.responseCode}");
                    yield break;
                }
            }
        }

        private IEnumerator TriggerFitToImagesRequest(string avatarId, string bearerToken, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/avatars/{avatarId}/fit-to-images";
            using (UnityWebRequest req = UnityWebRequest.PostWwwForm(url, ""))
            {
                req.SetRequestHeader("Authorization", bearerToken);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Fit-to-images error: {req.error}, code: {req.responseCode}");
                    yield break;
                }
            }
        }

        private IEnumerator PollUntilReadyRequest(string avatarId, string bearerToken, Action<AvatarResult> onResult,
            Action<string> onError)
        {
            string url = $"{apiBaseUrl}/avatars/{avatarId}";
            int attempts = 0;
            const int maxAttempts = 60;
            const float delay = 2f;

            while (attempts < maxAttempts)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.SetRequestHeader("Authorization", bearerToken);
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"Status request error: {req.error}, code: {req.responseCode}");
                        yield break;
                    }

                    string json = req.downloadHandler.text;
                    try
                    {
                        AvatarStatusResponse status = JsonConvert.DeserializeObject<AvatarStatusResponse>(json);
                        if (status.status == "succeeded")
                        {
                            AvatarResult r = new AvatarResult
                            {
                                avatarId = avatarId,
                                assetUrl = status.asset_url
                            };
                            onResult?.Invoke(r);
                            yield break;
                        }
                        else if (status.status == "failed")
                        {
                            onError?.Invoke("Avatar processing failed.");
                            yield break;
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Status JSON parse error: {e.Message}");
                        yield break;
                    }
                }

                attempts++;
                yield return new WaitForSeconds(delay);
            }

            onError?.Invoke("Avatar creation timed out.");
        }

        // --- Data types for JSON serialization ---
        private class TokenResponse
        {
            [JsonProperty("access_token")] public string access_token;
            [JsonProperty("expires_in")] public int expires_in;
            [JsonProperty("refresh_expires_in")] public int refresh_expires_in;

            [JsonProperty("token_type")] public string token_type;
            // There may be other fields like refresh_token, scope, etc.
        }

        [Serializable]
        public class UploadInfo
        {
            [JsonProperty("uploadUrl")] public string uploadUrl;
            [JsonProperty("contentType")] public string contentType;
        }

        [Serializable]
        public class AvatarStatusResponse
        {
            [JsonProperty("status")] public string status;
            [JsonProperty("asset_url")] public string asset_url;
        }

        public class AvatarResult
        {
            public string avatarId;
            public string assetUrl;
        }
    }

// --- Coroutine wrapper helper ---
    public static class CoroutineExtensions
    {
        public static IEnumerator Wrap(this IEnumerator routine, Action<object> onDone)
        {
            object result = null;
            while (routine.MoveNext())
            {
                result = routine.Current;
                yield return result;
            }

            onDone?.Invoke(result);
        }
    }
}