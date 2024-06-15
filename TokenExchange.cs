using System.Collections.Specialized;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Newtonsoft.Json;

namespace Avid.Function
{
    public class TokenExchange(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<TokenExchange>();

        [Function("TokenExchange")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("TokenExchange HTTP trigger function processed a request.");

            var config = new NameValueCollection
                {
                    { "clientId", Environment.GetEnvironmentVariable("ClientId") },
                    { "clientSecret", Environment.GetEnvironmentVariable("ClientSecret") }
                };


            _logger.LogInformation("read requestBody");



            string requestBody = String.Empty;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            dynamic data = JsonConvert.DeserializeObject(requestBody);


            _logger.LogInformation("read requestBody done");




            string? tenantId = data?.tenantId;
            string? token = data?.token;
            string? orgUrl = data?.orgUrl;
            string[] scopes = [$"{orgUrl}.default"];


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
            var graphToken = string.Empty;

            try
            {
                result = await app.AcquireTokenOnBehalfOf(scopes, userAssertion).ExecuteAsync();
                graphToken = await AcquireTokenAsync(config, tenantId, token, "https://graph.microsoft.com/.default");
                _logger.LogInformation($"graphToken : {graphToken}");
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "Error acquiring token: {Message}", ex.Message);
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Error acquiring token: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);

            var tokenResponse = new
            {

                dataverseToken = result.AccessToken,
                graphToken

            };

            await response.WriteAsJsonAsync(tokenResponse);


            return response;
        }

        private static async Task<string> AcquireTokenAsync(NameValueCollection config, string tenantId, string userToken, string scope)
        {
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(config["clientId"])
               .WithClientSecret(config["clientSecret"])
               .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
               .Build();

            UserAssertion userAssertion = new(userToken);
            AuthenticationResult result = await app.AcquireTokenOnBehalfOf([scope], userAssertion).ExecuteAsync();

            return result.AccessToken;
        }
    }
}
