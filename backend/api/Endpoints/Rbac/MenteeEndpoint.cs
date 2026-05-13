using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Api.Auth;
using System.Net;

namespace MentorshipPlatform.Api.Users
{
    public class MenteeEndpoint
    {
        private readonly AuthHelper _authHelper;
        private readonly ILogger<MenteeEndpoint> _logger;

        public MenteeEndpoint(AuthHelper authHelper, ILogger<MenteeEndpoint> logger)
        {
            _authHelper = authHelper;
            _logger = logger;
        }

        // Only Mentees can access this endpoint
        [RequireRole("Mentee")]
        [Function("RbacMentee")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rbac/mentee")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Mentee-only RBAC endpoint invoked.");

            // Ensure user exists in DB (JIT provisioning + CIAM role sync)
            await _authHelper.GetOrCreateUserIdAsync(context);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Welcome Mentee!");
            return response;
        }
    }
}
