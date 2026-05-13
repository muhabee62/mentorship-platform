using System.Linq;
using System.Security.Claims;

namespace MentorshipPlatform.Common
{
    public static class UserIdentityExtensions
    {
        public static string? GetEmail(this ClaimsPrincipal user)
        {
            if (user == null)
                return null;

            // External ID often uses "emails" (array)
            var email = user.Claims
                .Where(c => c.Type == "emails")
                .Select(c => c.Value)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(email))
                return email;

            // Fallbacks
            email = user.FindFirst("preferred_username")?.Value;
            if (!string.IsNullOrEmpty(email))
                return email;

            email = user.FindFirst("email")?.Value;
            if (!string.IsNullOrEmpty(email))
                return email;

            email = user.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
                return email;

            email = user.FindFirst("upn")?.Value;
            if (!string.IsNullOrEmpty(email))
                return email;

            return null;
        }
    }
}