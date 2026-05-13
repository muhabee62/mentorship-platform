using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Graph;
using MentorshipPlatform.Common;
using System.Security.Claims;

namespace MentorshipPlatform.Services
{
    public class CiamRoleService
    {
        private readonly CiamRoleSettings _settings;
        private readonly GraphServiceClient _graph;
        private readonly UserRepository _userRepo;

        public CiamRoleService(CiamRoleSettings settings, UserRepository userRepo)
        {
            _settings = settings;
            _userRepo = userRepo;

            var credential = new ClientSecretCredential(
                _settings.TenantId,
                _settings.ClientId,
                _settings.ClientSecret);

            _graph = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
        }

        private string GetRoleId(string roleName) => roleName switch
        {
            "Admin"  => _settings.AdminRoleId,
            "Mentor" => _settings.MentorRoleId,
            _        => _settings.MenteeRoleId
        };

        private async Task<Microsoft.Graph.User?> FindUserByEmailAsync(string email)
        {
            var users = await _graph.Users
                .Request()
                .Filter($"identities/any(id:id/issuerAssignedId eq '{email}')")
                .GetAsync();

            return users.CurrentPage.FirstOrDefault();
        }

        // Called on first sign-in to ensure user has the Mentee role
        public async Task EnsureUserHasRoleAsync(ClaimsPrincipal principal, string roleName)
        {
            // Only assign Mentee automatically
            if (!string.Equals(roleName, "Mentee", StringComparison.OrdinalIgnoreCase))
                return;

            var email = principal.Claims.FirstOrDefault(c => c.Type == "emails")?.Value
                     ?? principal.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            if (string.IsNullOrEmpty(email))
                return;

            var user = await FindUserByEmailAsync(email);
            if (user == null)
                return;

            var assignments = await _graph.Users[user.Id].AppRoleAssignments
                .Request()
                .GetAsync();

            var menteeRoleId = GetRoleId("Mentee");
            var apiAppId = _settings.ApiAppId;

            var alreadyAssigned = assignments.Any(a =>
                a.AppRoleId.HasValue &&
                a.AppRoleId.Value.ToString().Equals(menteeRoleId, StringComparison.OrdinalIgnoreCase) &&
                a.ResourceId.HasValue &&
                a.ResourceId.Value.ToString().Equals(apiAppId, StringComparison.OrdinalIgnoreCase));

            if (alreadyAssigned)
                return;

            await _graph.Users[user.Id].AppRoleAssignments
                .Request()
                .AddAsync(new AppRoleAssignment
                {
                    PrincipalId = Guid.Parse(user.Id),
                    ResourceId  = Guid.Parse(apiAppId),
                    AppRoleId   = Guid.Parse(menteeRoleId)
                });
        }

        // Called by Admin promotion endpoint
        public async Task UpdateUserAppRoleAsync(int userId, string newRole)
        {
            var email = await _userRepo.GetUserEmailByIdAsync(userId);
            if (email == null)
                return;

            var user = await FindUserByEmailAsync(email);
            if (user == null)
                return;

            var assignments = await _graph.Users[user.Id].AppRoleAssignments
                .Request()
                .GetAsync();

            var apiAppId = _settings.ApiAppId;

            var apiAssignments = assignments
                .Where(a => a.ResourceId.HasValue &&
                            a.ResourceId.Value.ToString().Equals(apiAppId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Remove old assignments
            foreach (var a in apiAssignments)
            {
                await _graph.Users[user.Id].AppRoleAssignments[a.Id]
                    .Request()
                    .DeleteAsync();
            }

            // Add new assignment
            var newRoleId = GetRoleId(newRole);

            await _graph.Users[user.Id].AppRoleAssignments
                .Request()
                .AddAsync(new AppRoleAssignment
                {
                    PrincipalId = Guid.Parse(user.Id),
                    ResourceId  = Guid.Parse(apiAppId),
                    AppRoleId   = Guid.Parse(newRoleId)
                });
        }
    }
}
