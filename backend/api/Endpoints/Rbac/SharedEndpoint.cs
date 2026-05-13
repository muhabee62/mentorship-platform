using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Api.Auth;
using System.Net;

namespace MentorshipPlatform.Api.Users
{
    public class SharedEndpoint
    {
        private readonly AuthHelper _authHelper;
        private readonly ILogger<SharedEndpoint> _logger;

        public SharedEndpoint(AuthHelper authHelper, ILogger<SharedEndpoint> logger)
        {
            _authHelper = authHelper;
            _logger = logger;
        }

        // Any authenticated role can access this endpoint
        [RequireRole("Admin", "Mentor", "Mentee")]
        [Function("RbacShared")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rbac/shared")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Shared RBAC endpoint invoked by authenticated user.");

            // Ensure user exists in DB (JIT provisioning + CIAM role sync)
            await _authHelper.GetOrCreateUserIdAsync(context);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Welcome — any authenticated role accepted!");
            return response;
        }
    }
}
