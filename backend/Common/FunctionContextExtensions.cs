using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;

namespace MentorshipPlatform.Common
{
    public static class FunctionContextExtensions
    {
        /// <summary>
        /// Returns the authenticated ClaimsPrincipal stored by JwtValidationMiddleware.
        /// </summary>
        public static ClaimsPrincipal? GetAuthenticatedUser(this FunctionContext context)
        {
            if (context.Items.TryGetValue("User", out var userObj) &&
                userObj is ClaimsPrincipal principal)
            {
                return principal;
            }

            return null;
        }

        /// <summary>
        /// Returns the authenticated user's objectId (sub claim).
        /// </summary>
        public static string? GetUserId(this FunctionContext context)
        {
            var user = context.GetAuthenticatedUser();
            return user?.FindFirst("sub")?.Value;
        }

        /// <summary>
        /// Returns the authenticated user's roles.
        /// </summary>
        public static IEnumerable<string> GetUserRoles(this FunctionContext context)
        {
            if (context.Items.TryGetValue("UserRoles", out var rolesObj) &&
                rolesObj is IEnumerable<string> roles)
            {
                return roles;
            }

            return Enumerable.Empty<string>();
        }
    }
}