using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Processes.Meshcapade
{
    public abstract class WebApiProcess : ProcessSync
    {
        public IEnumerator PostCoroutine(string url, string authToken, string content = null, string contentType = "application/json")
        {
            var request = new UnityWebRequest(url, "POST");
            if (!string.IsNullOrEmpty(content))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(content);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            request.timeout = 60;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Request error: {request.error}");
            }
            else
            {
                Debug.Log($"Response: {request.downloadHandler.text}");
            }
        }

        
        protected async Task<UnityWebRequest> PostAsync(string url, string authToken, string content = null, string contentType = "application/json")
        {
            var request = new UnityWebRequest(url, "POST");
            if (!string.IsNullOrEmpty(content))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(content);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            request.timeout = 60; // Timeout in Sekunden

            try
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"Request error: {request.error}");
                    throw new Exception(request.error);
                }

                return request;
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred: {ex.Message}");
                throw;
            }
        }
        

        protected async Task<UnityWebRequest> PutAsync(string url, string authToken, string content = null, string contentType = "application/json")
        {
            var request = new UnityWebRequest(url, "PUT");
            if (!string.IsNullOrEmpty(content))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(content);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            request.timeout = 60; // Timeout in Sekunden

            try
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"Request error: {request.error}");
                    throw new Exception(request.error);
                }

                return request;
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred: {ex.Message}");
                throw;
            }
        }
        
        public override ProcessResult Execute(string variant = "")
        {
            return ExecuteSync(variant);
        }
        
        
        protected MeshcapadeProcessResult TryCastToMeshcapadeProcessResult(ProcessResult processResult)
        {
            return processResult as MeshcapadeProcessResult;
        }
        
        
    }
}