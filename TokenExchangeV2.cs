using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Specialized;
using System.Text.Json;
using Microsoft.Identity.Client;

namespace dataverse_services
{
    public class TokenExchangeV2
    {
        private readonly ILogger _logger;

        public TokenExchangeV2(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TokenExchangeV2>();
        }

        [Function("TokenExchangeV2")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            var config = new NameValueCollection
                {
                    { "clientId", Environment.GetEnvironmentVariable("ClientId") },
                    { "clientSecret", Environment.GetEnvironmentVariable("ClientSecret") }
                    // { "tenantId", Environment.GetEnvironmentVariable("TenantId") }
                };

            string[] scopes = ["api://gray-mud-083bf1b0f.5.azurestaticapps.net/35b9ee25-fa46-4546-8d90-b048d45ee55d/access_as_user"];


            _logger.LogInformation("read requestBody");



            string requestBody = String.Empty;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            dynamic data = JsonSerializer.Deserialize<dynamic>(requestBody);

            _logger.LogInformation("read requestBody done");
            // var requestBody = await req.ReadAsStringAsync();
            // if (requestBody == null)
            // {
            //     var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            //     await badRequestResponse.WriteStringAsync("Request body cannot be empty.");
            //     return badRequestResponse;
            // }
            // var data = JsonSerializer.Deserialize<dynamic>(requestBody);
            string? tenantId = data?.tenantId;
            string? token = data?.token;

            _logger.LogInformation($"tenantId ; {tenantId}");

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(token))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Please provide tenantId and token in the request body.");
                return badRequestResponse;
            }

            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(config["clientId"])
           .WithClientSecret(config["clientSecret"])
           .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
           .Build();

            UserAssertion userAssertion = new(token);
            AuthenticationResult result;

            try
            {
                result = await app.AcquireTokenOnBehalfOf(scopes, userAssertion).ExecuteAsync();
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "Error acquiring token: {Message}", ex.Message);
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Error acquiring token: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { result.AccessToken });
            return response;
        }
    }
}
