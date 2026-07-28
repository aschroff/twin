using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public string username;
        public string password;

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
            Debug.Log($"Step 1: Getting access token...");
            string bearerToken = null;
            yield return StartCoroutine(GetAccessTokenCoroutine(
                t => bearerToken = t,
                onError));
            if (string.IsNullOrEmpty(bearerToken))
            {
                yield break;
            }
            Debug.Log($"Got token: {bearerToken}");
            Debug.Log($"Step 2: Creating request...");
            // 2. Create avatar (init)
            string avatarId = null;
            yield return StartCoroutine(CreateAvatarRequest(bearerToken, id => avatarId = id, onError));
            if (string.IsNullOrEmpty(avatarId))
            {
                yield break;
            }
            Debug.Log($"Step 3: Creating register image upload...");
            // 3. Register image upload
            UploadInfo uploadInfo = null;
            yield return StartCoroutine(RegisterImageRequest(avatarId, bearerToken, info => uploadInfo = info,
                onError));
            if (uploadInfo == null)
            {
                yield break;
            }
            Debug.Log($"Step 4: Load image...");
            // 4. Get image bytes
            byte[] imageBytes = null;
            yield return StartCoroutine(getImageBytesCoroutine().Wrap(obj => imageBytes = obj as byte[]));
            if (imageBytes == null)
            {
                onError?.Invoke("Image bytes were null");
                yield break;
            }
            Debug.Log($"Step 5: Upload image...");
            // 5. Upload image
            yield return StartCoroutine(UploadImageBytes(uploadInfo, imageBytes, onError));
            Debug.Log($"Step 6: Fit image...");
            // 6. Trigger fit-to-images

            var fittingBody = new FinalFittingImageBody
            {
                data = new FittingImageData
                {
                    type = "fit-to-images",
                    attributes = new FittingImageAttributes
                    {
                        avatarname = "meerjungfrau2",
                        gender = "female",
                        imageMode = "AFI"
                    }
                }
            };

            yield return StartCoroutine(TriggerFitToImagesRequest(avatarId, bearerToken, onError, fittingBody));
            Debug.Log($"Step 7: Poll until complete...");
            // 7. Poll for completion
            AvatarResult result = null;


            yield return StartCoroutine(PollUntilReadyRequest(avatarId, bearerToken, r => result = r, onError));
            if (result == null)
            {
                yield break;
            }
            // 8. Requesting export
            Debug.Log("Step 8: Requesting export...");
            string exportUrl = null;

            var exportBody = new FinalExportRequestBody
            {
                data = new ExportRequestData
                {
                    type = "export",
                    attributes = new ExportRequestAttributes
                    {
                        format = "OBJ" // Change to OBJ/FBX if needed
                    }
                }
            };

            yield return StartCoroutine(RequestExport(avatarId, bearerToken, exportBody,
                url => exportUrl = url,
                onError));

            if (string.IsNullOrEmpty(exportUrl))
                yield break;

            Debug.Log($"Export ready: {exportUrl}");

            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 9. Success → return final result
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            AvatarResult finalResult = new AvatarResult
            {
                avatarId = avatarId,
                assetUrl = exportUrl // store export download URL
            };

            onComplete?.Invoke(finalResult);

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
                    var resp = JsonConvert.DeserializeObject<JsonApiSingle<Resource<AvatarAttributes>>>(json);
                    if (resp?.data?.id != null)
                    {
                        onAvatarId?.Invoke(resp.data.id);
                    }
                    else
                    {
                        onError?.Invoke("CreateAvatar: response missing data.id. Raw JSON: " + json);
                    }
                }
                catch (Exception e)
                {
                    onError?.Invoke($"CreateAvatar JSON parse error: {e.Message}. Raw JSON: {json}");
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
                string json = req.downloadHandler.text;
                try
                {
                    // Deserialize into the typed wrapper for Resource<ImageRegisterAttributes>
                    var resp = JsonConvert.DeserializeObject<JsonApiSingle<Resource<ImageRegisterAttributes>>>(json);

                    string presignedUrl = null;
                    string httpMethod = null;

                    // 1) Try attributes.url (preferred)
                    if (resp?.data?.attributes?.url != null)
                    {
                        var urlObj = resp.data.attributes.url;
                        presignedUrl = urlObj.GetLink();
                        httpMethod = urlObj.GetMethod();
                    }

                    // 2) Fallback: try links.upload (some responses provide the upload URL in links)
                    if (string.IsNullOrEmpty(presignedUrl) && resp?.data?.links != null)
                    {
                        // links is a Dictionary<string,string> in our Resource<T>. Try "upload" key.
                        if (resp.data.links.TryGetValue("upload", out var uploadLink) && !string.IsNullOrEmpty(uploadLink))
                        {
                            presignedUrl = uploadLink;
                        }
                    }

                    if (string.IsNullOrEmpty(presignedUrl))
                    {
                        onError?.Invoke("RegisterImage: upload url missing in response. Raw JSON: " + json);
                        yield break;
                    }

                    UploadInfo ui = new UploadInfo
                    {
                        UploadUrl = presignedUrl,
                        Method = string.IsNullOrEmpty(httpMethod) ? "PUT" : httpMethod.ToUpperInvariant(),
                        ContentType = null // not provided by the API in this response — keep null and set default when uploading if needed
                    };

                    onUploadInfo?.Invoke(ui);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"RegisterImage JSON parse error: {e.Message}. Raw JSON: {json}");
                    yield break;
                }

               
            }
        }

        private IEnumerator UploadImageBytes(UploadInfo uploadInfo, byte[] imageBytes, Action<string> onError)
        {
            using (UnityWebRequest req = UnityWebRequest.Put(uploadInfo.UploadUrl, imageBytes))
            {
                // Some presigned endpoints expect no Content-Type header, some do.
                // If the API supplies a content type, set it; otherwise optionally set to image/jpeg.
                if (!string.IsNullOrEmpty(uploadInfo.ContentType))
                    req.SetRequestHeader("Content-Type", uploadInfo.ContentType);
                else
                    req.SetRequestHeader("Content-Type", "image/jpeg"); // or omit header if upload fails

                // If the presigned upload requires specific headers, you must set them exactly as returned by API.
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Upload image error: {req.error}, code: {req.responseCode}, body: {req.downloadHandler?.text}");
                    yield break;
                }
            }

        }

        private IEnumerator TriggerFitToImagesRequest(string avatarId, string bearerToken, Action<string> onError, FinalFittingImageBody bodyOptions)
        {
            string url = $"{apiBaseUrl}/avatars/{avatarId}/fit-to-images";
            JObject bodyJson = bodyOptions != null ? JObject.FromObject(bodyOptions) : new JObject();

            bodyJson["imageMode"] = "AFI";
            bodyJson["gender"] = "female";
            string body = bodyJson.ToString(Formatting.None);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            /*string body = JsonConvert.SerializeObject(bodyOptions);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);*/

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", bearerToken);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Fit-to-images error: {req.error}, code: {req.responseCode}, body: {req.downloadHandler?.text}");
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
                    req.SetRequestHeader("Content-Type", "application/json");
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"Status request error: {req.error}, code: {req.responseCode}");
                        yield break;
                    }

                    string json = req.downloadHandler.text;
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<JsonApiSingle<Resource<AvatarStatusAttributes>>>(json);
                        var state = resp?.data?.attributes?.state;
                        if (!string.IsNullOrEmpty(state))
                        {
                            if (state.Equals("READY", StringComparison.OrdinalIgnoreCase) || state.Equals("succeeded", StringComparison.OrdinalIgnoreCase))
                            {
                                // optionally get asset_url from attributes or links
                                string assetUrl = resp.data.attributes.asset_url;
                                // If the API does not return a direct asset_url, you will need to call export endpoint next (see below).
                                AvatarResult r = new AvatarResult { avatarId = avatarId, assetUrl = assetUrl };
                                onResult?.Invoke(r);
                                yield break;
                            }
                            else if (state.Equals("FAILED", StringComparison.OrdinalIgnoreCase) || state.Equals("failure", StringComparison.OrdinalIgnoreCase) || state.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                            {
                                onError?.Invoke("Avatar processing failed. Attempt: " + attempts + ", Raw JSON: " + json);
                                yield break;
                            }
                            // else still processing -> continue polling
                        }
                        else
                        {
                            onError?.Invoke("Avatar status response missing state. Raw JSON: " + json);
                            yield break;
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Status JSON parse error: {e.Message}. Raw JSON: {json}");
                        yield break;
                    }

                }

                attempts++;
                yield return new WaitForSeconds(delay);
            }

            onError?.Invoke("Avatar creation timed out.");
        }
        
        private IEnumerator RequestExport(string avatarId, string bearerToken, FinalExportRequestBody exportBody, Action<string> onExportUrl, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/avatars/{avatarId}/export";
            JObject bodyJson = exportBody != null ? JObject.FromObject(exportBody) : new JObject();

            bodyJson["filename"] = exportBody.data.attributes.filename;
            bodyJson["format"] = exportBody.data.attributes.format;
            bodyJson["pose"] = exportBody.data.attributes.pose;

            string body = bodyJson.ToString(Formatting.None);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            /*string body = JsonConvert.SerializeObject(exportBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);*/

            string statusUrl = "";
            // 1) Trigger export
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", bearerToken);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Export request failed: {req.error}, code: {req.responseCode}, body: {req.downloadHandler?.text}");
                    yield break;
                }


                var resp = JsonConvert.DeserializeObject<JsonApiSingle<Resource<ExportAttributes>>>(req.downloadHandler.text);

                // try to get self link for requesting status
                if (resp?.data?.links != null && resp.data.links.TryGetValue("self", out var selfLink))
                {
                    statusUrl = selfLink;
                    Debug.Log("Step 8: Got Url for checking status");
                }
                else
                {
                    // Fallback if links aren't provided: GET /meshes/{meshID}
                    statusUrl = $"{apiBaseUrl}/meshes/{resp.data.id}";
                }
            }



            // 2) Poll export status (simple loop)
            int attempts = 0;
            const int maxAttempts = 60;
            const float delay = 2f;

            while (attempts < maxAttempts)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(statusUrl)) 
                {
                    req.SetRequestHeader("Authorization", bearerToken);
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"Export status error: {req.error}, code: {req.responseCode}");
                        yield break;
                    }

                    string json = req.downloadHandler.text;
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<JsonApiSingle<Resource<ExportAttributes>>>(json);
                        var state = resp?.data?.attributes?.state;
                        if (!string.IsNullOrEmpty(state))
                        {
                            if (state.Equals("READY", StringComparison.OrdinalIgnoreCase))
                            {
                                string downloadUrl = resp?.data?.attributes?.url.path; //download link
                                /*if (!string.IsNullOrEmpty(downloadUrl))
                                {
                                    onExportUrl?.Invoke(downloadUrl);
                                    yield break;
                                }*/
                                if (string.IsNullOrEmpty(downloadUrl) && resp?.data?.links != null && resp.data.links.TryGetValue("download", out var dl)) // fallback: check links
                                {
                                    downloadUrl = dl;
                                }

                                if (!string.IsNullOrEmpty(downloadUrl))
                                {
                                    //onExportUrl?.Invoke(downloadUrl);
                                    StartCoroutine(DownloadAvatarContent(downloadUrl));
                                    yield break;
                                }
                                else
                                {
                                    onError?.Invoke("Export ready but download url not found. Raw JSON: " + json);
                                    yield break;
                                }
                            }
                            else if (state.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
                            {
                                onError?.Invoke("Export failed. Raw JSON: " + json);
                                yield break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Export status parse error: {e.Message}. Raw JSON: {json}");
                        yield break;
                    }
                }

                attempts++;
                yield return new WaitForSeconds(delay);
            }

            onError?.Invoke("Export timed out");
        }

        IEnumerator DownloadAvatarContent(string url)
        {
            string myData = "";
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to download file: " + www.error);
                }
                else
                {
                    myData = www.downloadHandler.text;

                    Debug.Log("Successfully stored file. Length: " + myData.Length);

                }
            }
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