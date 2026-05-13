using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Common
{
    public class AuthFilter : IFunctionsWorkerMiddleware
    {
        private readonly AuthSettings _settings;
        private readonly ILogger<AuthFilter> _logger;

        public AuthFilter(AuthSettings settings, ILogger<AuthFilter> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            // Skip for test function
            if (context.FunctionDefinition.Name == "HttpTrigger1")
            {
                await next(context);
                return;
            }

            var req = await context.GetHttpRequestDataAsync();
            if (req == null)
            {
                await next(context);
                return;
            }

            // Extract Authorization header
            string? header = null;
            if (req.Headers.TryGetValues("Authorization", out var values))
                header = values.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer "))
            {
                await ReturnUnauthorized(context, "Missing or invalid Authorization header.");
                return;
            }

            // Principal should already be set by JwtValidationMiddleware
            var principal = context.Items["User"] as ClaimsPrincipal;
            if (principal == null)
            {
                await ReturnUnauthorized(context, "Invalid or expired token.");
                return;
            }

            // Enforce role attributes
            if (!await EnforceRoleRequirement(context, principal))
                return;

            await next(context);
        }

        private async Task<bool> EnforceRoleRequirement(FunctionContext context, ClaimsPrincipal principal)
        {
            var entryPoint = context.FunctionDefinition.EntryPoint;

            var lastDot = entryPoint.LastIndexOf('.');
            var typeName = entryPoint.Substring(0, lastDot);
            var methodName = entryPoint.Substring(lastDot + 1);

            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetType(typeName);
            if (type == null)
            {
                _logger.LogWarning($"AuthFilter: Could not resolve type '{typeName}'.");
                return true;
            }

            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                _logger.LogWarning($"AuthFilter: Could not resolve method '{methodName}' on '{typeName}'.");
                return true;
            }

            // Read [RequireRole] attributes
            var roleAttributes = method
                .GetCustomAttributes(typeof(RequireRoleAttribute), false)
                .Cast<RequireRoleAttribute>()
                .ToArray();

            if (roleAttributes.Length == 0)
                return true;

            // Extract roles from context (set by JwtValidationMiddleware)
            var extractedRoles = context.Items["UserRoles"] as List<string>;

            // Fallback to claims if needed
            var userRoles = extractedRoles ?? principal.Claims
                .Where(c =>
                    c.Type == "roles" ||
                    c.Type == ClaimTypes.Role ||
                    c.Type.EndsWith("/claims/role") ||
                    c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            var roleSet = userRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Check if user has any required role
            foreach (var attr in roleAttributes)
            {
                if (attr.Roles.Any(r => roleSet.Contains(r)))
                    return true;
            }

            var required = string.Join(", ", roleAttributes.SelectMany(a => a.Roles));
            await ReturnUnauthorized(context, $"Missing required role(s): {required}");
            return false;
        }

        private async Task ReturnUnauthorized(FunctionContext context, string message)
        {
            var req = await context.GetHttpRequestDataAsync();
            var res = req!.CreateResponse(HttpStatusCode.Unauthorized);
            await res.WriteStringAsync(message);
            context.GetInvocationResult().Value = res;
        }
    }
}
