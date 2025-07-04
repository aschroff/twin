using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Code.Processes.Meshcapade
{
    public abstract class WebApiProcess : ProcessSync
    {
        protected readonly HttpClient HttpClient = new HttpClient();

        protected async Task<HttpResponseMessage> PostAsync(string url, string authToken, string content = null, string contentType = "application/json")
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {authToken}");
            if (content != null)
            {
                request.Content = new StringContent(content, Encoding.UTF8, contentType);
            }
            return await HttpClient.SendAsync(request);
        }

        protected async Task<HttpResponseMessage> PutAsync(string url, string content, string contentType)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Content = new StringContent(content, Encoding.UTF8, contentType);
            return await HttpClient.SendAsync(request);
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