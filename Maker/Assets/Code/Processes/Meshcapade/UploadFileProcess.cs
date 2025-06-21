using System.Net.Http;
using System.Threading.Tasks;
using Code.Processes.Meshcapade;

namespace Code.Processes
{
    public class UploadFileProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "", ProcessResult previousResult = null)
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(previousResult);
            var presignedPutUrl = meshcapadeResult.presignedPutUrl;
            var fileContent = meshcapadeResult.fileContent; // Datei-Inhalt aus `ProcessResult`

            var response = PutAsync(presignedPutUrl, fileContent, "image/jpeg").Result;
            response.EnsureSuccessStatusCode();

            return meshcapadeResult;
        }
    }
}