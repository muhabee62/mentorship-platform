using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using MentorshipPlatform.Common;
using MentorshipPlatform.Services;

namespace MentorshipPlatform.Api.Auth
{
    public class AuthHelper
    {
        private readonly UserRepository _userRepo;
        private readonly CiamRoleService _ciam;

        public AuthHelper(UserRepository userRepo, CiamRoleService ciam)
        {
            _userRepo = userRepo;
            _ciam = ciam;
        }

        public async Task<int> GetOrCreateUserIdAsync(FunctionContext context)
        {
            var principal = context.GetAuthenticatedUser();
            var email = principal.GetEmail();

            if (email == null)
                throw new UnauthorizedAccessException("Unable to determine user identity.");

            // Extract roles from token
            var roles = principal.Claims
                .Where(c =>
                    c.Type == "roles" ||
                    c.Type == "role" ||
                    c.Type.EndsWith("/claims/role"))
                .Select(c => c.Value)
                .ToList();

            // Default to Mentee if no role present
            var effectiveRole = roles.FirstOrDefault() ?? "Mentee";

            // Create or load user in SQL
            var userId = await _userRepo.GetOrCreateUserAsync(email, effectiveRole);

            // Ensure CIAM has the same role (assign Mentee on first login)
            await _ciam.EnsureUserHasRoleAsync(principal, effectiveRole);

            return userId;
        }
    }
}
