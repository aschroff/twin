using System.Net.Http;
using System.Threading.Tasks;
using Code.Processes.Meshcapade;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Processes
{
    public class UploadFileProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "")
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(processManager().processResult);
            var presignedPutUrl = meshcapadeResult.presignedPutUrl;
            var fileContent = meshcapadeResult.fileContent; // Datei-Inhalt aus `ProcessResult`

            Debug.Log("Uploading file...");
            var response = PutAsync(presignedPutUrl, null, fileContent, "image/jpeg").Result;

            if (response.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("File uploaded successfully.");
            }
            else
            {
                Debug.LogError($"Error uploading file: {response.error}");
                meshcapadeResult.ErrorCode = "UPLOAD_ERROR";
            }

            return meshcapadeResult;
        }
    }
}