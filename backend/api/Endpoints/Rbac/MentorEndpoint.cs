using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Api.Auth;
using System.Net;

namespace MentorshipPlatform.Api.Users
{
    public class MentorEndpoint
    {
        private readonly AuthHelper _authHelper;
        private readonly ILogger<MentorEndpoint> _logger;

        public MentorEndpoint(AuthHelper authHelper, ILogger<MentorEndpoint> logger)
        {
            _authHelper = authHelper;
            _logger = logger;
        }

        // Only Mentors can access this endpoint
        [RequireRole("Mentor")]
        [Function("RbacMentor")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rbac/mentor")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Mentor-only RBAC endpoint invoked.");

            // Ensure user exists in DB (JIT provisioning + CIAM role sync)
            await _authHelper.GetOrCreateUserIdAsync(context);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Welcome Mentor!");
            return response;
        }
    }
}
