
using System.Collections.Specialized;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Text.Json;

namespace Avid.Function
{
    public class TokenExchange(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<TokenExchange>();
        private readonly NameValueCollection _config = new()
        {
            { "clientId", Environment.GetEnvironmentVariable("ClientId") },
            { "clientSecret", Environment.GetEnvironmentVariable("ClientSecret") }
        };

        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [Function("TokenExchange")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("TokenExchange HTTP trigger function processed a request.");

            string requestBody;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            var data = JsonSerializer.Deserialize<RequestData>(requestBody, JsonSerializerOptions);

            string? tenantId = data?.TenantId;
            string? token = data?.Token;
            string? orgUrl = data?.OrgUrl;
            string? resources = data?.Resources;

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(resources))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Please provide tenantId, token, and resources in the request body.");
                return badRequestResponse;
            }

            var tokenResponse = new TokenResponse();

            try
            {
                if (resources.Contains("dataverse", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(orgUrl))
                {
                    tokenResponse.DataverseToken = await GetDataverseToken(tenantId, token, orgUrl);
                }

                if (resources.Contains("graph", StringComparison.OrdinalIgnoreCase))
                {
                    tokenResponse.GraphToken = await GetGraphToken(tenantId, token);
                }
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "Error acquiring token: {Message}", ex.Message);
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Error acquiring token: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(tokenResponse);
            return response;
        }

        private async Task<string> GetDataverseToken(string tenantId, string exchangeToken, string orgUrl)
        {
            return await AcquireTokenAsync(tenantId, exchangeToken, $"{orgUrl}/.default");
        }

        private async Task<string> GetGraphToken(string tenantId, string exchangeToken)
        {
            return await AcquireTokenAsync(tenantId, exchangeToken, "https://graph.microsoft.com/.default");
        }

        private async Task<string> AcquireTokenAsync(string tenantId, string exchangeToken, string scope)
        {
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(_config["clientId"])
               .WithClientSecret(_config["clientSecret"])
               .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
               .Build();

            UserAssertion userAssertion = new(exchangeToken);
            AuthenticationResult result = await app.AcquireTokenOnBehalfOf([scope], userAssertion).ExecuteAsync();

            return result.AccessToken;
        }

        private class RequestData
        {
            public string TenantId { get; set; }
            public string Token { get; set; }
            public string OrgUrl { get; set; }
            public string Resources { get; set; }
        }

        private class TokenResponse
        {
            public string DataverseToken { get; set; }
            public string GraphToken { get; set; }
        }
    }
}