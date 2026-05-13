using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Api.Auth;
using System.Net;

namespace MentorshipPlatform.Api.Users
{
    public class AdminEndpoint
    {
        private readonly AuthHelper _authHelper;
        private readonly ILogger<AdminEndpoint> _logger;

        public AdminEndpoint(AuthHelper authHelper, ILogger<AdminEndpoint> logger)
        {
            _authHelper = authHelper;
            _logger = logger;
        }

        // Only Admins can access this endpoint
        [RequireRole("Admin")]
        [Function("RbacAdmin")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rbac/admin")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Admin-only RBAC endpoint invoked.");

            // Ensure user exists in DB (JIT provisioning + CIAM role sync)
            await _authHelper.GetOrCreateUserIdAsync(context);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Welcome Admin!");
            return response;
        }
    }
}
