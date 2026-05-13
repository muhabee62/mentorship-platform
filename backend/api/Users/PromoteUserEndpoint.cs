using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Api.Auth;
using MentorshipPlatform.Common;
using MentorshipPlatform.Services;

namespace MentorshipPlatform.Api.Users
{
    public class PromoteUserRequest
    {
        public string Role { get; set; } = default!; // "Admin" | "Mentor" | "Mentee"
    }

    public class PromoteUserEndpoint
    {
        private readonly UserRepository _repo;
        private readonly CiamRoleService _ciam;
        private readonly ILogger<PromoteUserEndpoint> _logger;

        public PromoteUserEndpoint(
            UserRepository repo,
            CiamRoleService ciam,
            ILogger<PromoteUserEndpoint> logger)
        {
            _repo = repo;
            _ciam = ciam;
            _logger = logger;
        }

        // Only Admins can promote/demote users
        [RequireRole("Admin")]
        [Function("PromoteUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", 
                Route = "users/{userId:int}/role")] HttpRequestData req,
            int userId,
            FunctionContext context)
        {
            _logger.LogInformation("Processing user role update for UserId={UserId}", userId);

            var body = await req.ReadFromJsonAsync<PromoteUserRequest>();
            var newRole = body?.Role;

            if (string.IsNullOrWhiteSpace(newRole) ||
                !(newRole == "Admin" || newRole == "Mentor" || newRole == "Mentee"))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid role. Must be Admin, Mentor, or Mentee.");
                return bad;
            }

            // 1. Update SQL role + role tables
            await _repo.UpdateUserRoleAsync(userId, newRole);

            // 2. Update CIAM app role assignment
            await _ciam.UpdateUserAppRoleAsync(userId, newRole);

            _logger.LogInformation("User {UserId} successfully updated to role {Role}", userId, newRole);

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteStringAsync($"User {userId} promoted to {newRole}.");
            return ok;
        }
    }
}
