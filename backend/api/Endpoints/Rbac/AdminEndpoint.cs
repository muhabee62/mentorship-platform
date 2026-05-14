using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Api.Auth;
using MentorshipPlatform.Common;

namespace MentorshipPlatform.Api.Users
{
    public class AdminEndpoint
    {
        private readonly AuthHelper _authHelper;
        private readonly ILogger<AdminEndpoint> _logger;

        public AdminEndpoint(AuthHelper authHelper, ILogger<AdminEndpoint> logger)
        {
            _authHelper = authHelper;
            _logger = logger;
        }

        [RequireRole("Admin")]
        [Function("RbacAdmin")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rbac/admin")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════════");
            _logger.LogInformation("🔵 ADMIN ENDPOINT INVOKED");
            _logger.LogInformation("═══════════════════════════════════════════════════════════════");

            try
            {
                _logger.LogInformation("🚀 Admin-only RBAC endpoint processing started...");

                // Step 1 — JIT provisioning + CIAM sync
                _logger.LogInformation("⏳ Step 1/3: Provisioning user...");
                await _authHelper.GetOrCreateUserIdAsync(context);
                _logger.LogInformation("✅ Step 1/3: User provisioning complete!");

                // Step 2 — Create response
                _logger.LogInformation("⏳ Step 2/3: Creating HTTP response...");
                var response = req.CreateResponse(HttpStatusCode.OK);
                _logger.LogInformation("✅ Step 2/3: HTTP response created!");

                // Step 3 — Write body
                _logger.LogInformation("⏳ Step 3/3: Writing response body...");
                await response.WriteStringAsync("Welcome Admin!");
                _logger.LogInformation("✅ Step 3/3: Response body written!");

                _logger.LogInformation("🎉 ADMIN ENDPOINT: SUCCESS!");
                _logger.LogInformation("═══════════════════════════════════════════════════════════════");

                return response;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"⚠️ Authorization Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await errorResponse.WriteStringAsync($"Authorization Error: {ex.Message}");
                return errorResponse;
            }
            catch (Exception ex)
            {
                var contextData = new Dictionary<string, object>
                {
                    { "Endpoint", "RbacAdmin" },
                    { "HttpMethod", req.Method },
                    { "RequestUrl", req.Url.ToString() },
                    { "Timestamp", DateTime.UtcNow }
                };

                ExceptionLogger.LogFullException(
                    _logger,
                    ex,
                    "Unexpected Error in AdminEndpoint",
                    contextData
                );

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync(ExceptionLogger.GetDetailedErrorMessage(ex, "AdminEndpoint"));
                return errorResponse;
            }
        }
    }
}
