using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BIMBotPlugin.AI
{
    /// <summary>
    /// Thin client for the Power BI REST API.
    /// All methods take an access token from PowerBIOAuthHelper.
    /// </summary>
    public static class PowerBIApiClient
    {
        private const string BaseUrl = "https://api.powerbi.com/v1.0/myorg";

        public static async Task<JArray> GetWorkspacesAsync(string accessToken)
        {
            using (var client = CreateClient(accessToken))
            {
                var resp = await client.GetStringAsync($"{BaseUrl}/groups");
                var data = JObject.Parse(resp);
                return data["value"] as JArray ?? new JArray();
            }
        }

        public static async Task<JArray> GetReportsAsync(string accessToken, string workspaceId)
        {
            using (var client = CreateClient(accessToken))
            {
                var resp = await client.GetStringAsync($"{BaseUrl}/groups/{workspaceId}/reports");
                var data = JObject.Parse(resp);
                return data["value"] as JArray ?? new JArray();
            }
        }

        /// <summary>
        /// Gets the embed URL and generates an embed token for a report.
        /// Returns { embedUrl, embedToken, reportName }.
        /// </summary>
        public static async Task<JObject> GetEmbedInfoAsync(string accessToken, string workspaceId, string reportId)
        {
            using (var client = CreateClient(accessToken))
            {
                // Get the report metadata (includes embedUrl)
                var reportResp = await client.GetStringAsync(
                    $"{BaseUrl}/groups/{workspaceId}/reports/{reportId}");
                var report = JObject.Parse(reportResp);
                var embedUrl = report["embedUrl"]?.ToString() ?? "";
                var reportName = report["name"]?.ToString() ?? "";

                // Generate embed token
                var tokenBody = new StringContent(
                    "{\"accessLevel\":\"View\"}", Encoding.UTF8, "application/json");
                var tokenResp = await client.PostAsync(
                    $"{BaseUrl}/groups/{workspaceId}/reports/{reportId}/GenerateToken", tokenBody);

                var tokenRespBody = await tokenResp.Content.ReadAsStringAsync();

                if (!tokenResp.IsSuccessStatusCode)
                    throw new System.Exception($"GenerateToken failed ({(int)tokenResp.StatusCode}): {tokenRespBody}");

                var tokenData = JObject.Parse(tokenRespBody);

                return new JObject
                {
                    ["embedUrl"] = embedUrl,
                    ["embedToken"] = tokenData["token"]?.ToString(),
                    ["reportName"] = reportName
                };
            }
        }

        private static HttpClient CreateClient(string accessToken)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }
    }
}
