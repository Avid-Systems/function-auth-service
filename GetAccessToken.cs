using System.Collections.Specialized;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Avid.Function
{
    public class GetAccessToken(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<GetAccessToken>();
        private readonly string[] scopes = [$"{Environment.GetEnvironmentVariable("OrgUrl")}/.default"];

        [Function("GetAccessToken")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("HTTP GetToken");

            var config = new NameValueCollection
                {
                    { "clientId", Environment.GetEnvironmentVariable("ClientId") },
                    { "clientSecret", Environment.GetEnvironmentVariable("ClientSecret") },
                    { "tenantId", Environment.GetEnvironmentVariable("TenantId") }
                };



            var app = ConfidentialClientApplicationBuilder.Create(config["clientId"])
                .WithClientSecret(config["clientSecret"])
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{config["tenantId"]}"))
                .Build();

            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
                var tokenResponse = new { token = result.AccessToken };

                await response.WriteAsJsonAsync(tokenResponse);
                response.StatusCode = HttpStatusCode.OK;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error acquiring token.");
                response.StatusCode = HttpStatusCode.InternalServerError;
            }

            return response;
        }
    }
}
