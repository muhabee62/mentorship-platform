using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

public class JwtValidationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly AuthSettings _settings;
    private readonly ILogger<JwtValidationMiddleware> _logger;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public JwtValidationMiddleware(AuthSettings settings, ILogger<JwtValidationMiddleware> logger)
    {
        _settings = settings;
        _logger = logger;

        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_settings.Authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
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
            await next(context);
            return;
        }

        var token = header.Substring("Bearer ".Length).Trim();

        _logger.LogWarning("RAW TOKEN RECEIVED BY BACKEND: {Token}", token);

        try
        {
            var config = await _configManager.GetConfigurationAsync(default);

            var validationParams = new TokenValidationParameters
            {
                ValidIssuer = config.Issuer,
                ValidAudiences = new[] { _settings.Audience },
                IssuerSigningKeys = config.SigningKeys,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParams, out _);

            // Store principal
            context.Items["User"] = principal;

            // Extract roles
            var roles = principal.Claims
                .Where(c =>
                    c.Type == "roles" ||
                    c.Type == ClaimTypes.Role ||
                    c.Type.EndsWith("/claims/role") ||
                    c.Type == "role")
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            context.Items["UserRoles"] = roles;

            // Extract scopes
            context.Items["UserScopes"] = principal.Claims
                .Where(c => c.Type == "scp")
                .Select(c => c.Value)
                .ToList();

            // Extract OID (needed for CIAM role assignment)
            var oid = principal.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;
            if (!string.IsNullOrEmpty(oid))
                context.Items["UserOid"] = oid;

            _logger.LogWarning("JWT ROLES IN ACCESS TOKEN: {Roles}",
                roles.Count == 0 ? "<none>" : string.Join(", ", roles));

            _logger.LogInformation("JWT validated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed.");
            // AuthFilter will return 401
        }

        await next(context);
    }
}
