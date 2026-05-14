using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Services;

namespace MentorshipPlatform.Api.Auth
{
    public class AuthHelper
    {
        private readonly UserRepository _userRepo;
        private readonly CiamRoleService _ciam;
        private readonly ILogger<AuthHelper> _logger;

        public AuthHelper(UserRepository userRepo, CiamRoleService ciam, ILogger<AuthHelper> logger)
        {
            _userRepo = userRepo;
            _ciam = ciam;
            _logger = logger;
        }

        public async Task<int> GetOrCreateUserIdAsync(FunctionContext context)
        {
            try
            {
                _logger.LogInformation("🔐 GetOrCreateUserIdAsync: Starting user provisioning flow...");

                // ===== STEP 1: Extract Principal =====
                _logger.LogInformation("📝 Step 1: Extracting authenticated principal...");
                var principal = context.GetAuthenticatedUser();
                
                if (principal == null)
                {
                    _logger.LogError("❌ Principal is null!");
                    throw new UnauthorizedAccessException("Cannot extract authenticated user from context.");
                }

                _logger.LogInformation($"✅ Principal extracted. Subject: {principal.FindFirst("sub")?.Value}");

                // ===== STEP 2: Extract Email =====
                _logger.LogInformation("📧 Step 2: Extracting email from principal...");
                var email = principal.FindFirst("preferred_username")?.Value 
                    ?? principal.FindFirst("email")?.Value 
                    ?? principal.FindFirst("name")?.Value;

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogError("❌ Email not found in token claims!");
                    _logger.LogWarning($"📋 Available claims: {string.Join(", ", principal.Claims.Select(c => c.Type))}");
                    throw new UnauthorizedAccessException("Unable to determine user identity from email claim.");
                }

                _logger.LogInformation($"✅ Email extracted: {email}");

                // ===== STEP 3: Extract Roles =====
                _logger.LogInformation("🎭 Step 3: Extracting roles from token...");
                var roles = principal.Claims
                    .Where(c =>
                        c.Type == "roles" ||
                        c.Type == "role" ||
                        c.Type.EndsWith("/claims/role"))
                    .Select(c => c.Value)
                    .ToList();

                var effectiveRole = roles.FirstOrDefault() ?? "Mentee";
                _logger.LogInformation($"✅ Roles extracted. Effective Role: {effectiveRole} | All Roles: {string.Join(", ", roles)}");

                // ===== STEP 4: Create/Load User in SQL =====
                _logger.LogInformation($"💾 Step 4: Creating or loading user from database... Email: {email}");
                var userId = await _userRepo.GetOrCreateUserAsync(email, effectiveRole);
                _logger.LogInformation($"✅ User provisioned. UserID: {userId}");

                // ===== STEP 5: Sync CIAM Role =====
                _logger.LogInformation($"🔄 Step 5: Synchronizing CIAM role assignment...");
                await _ciam.EnsureUserHasRoleAsync(principal, effectiveRole);
                _logger.LogInformation($"✅ CIAM role synced successfully.");

                _logger.LogInformation($"🎉 GetOrCreateUserIdAsync: Complete! UserID={userId}, Role={effectiveRole}");

                return userId;
            }
            catch (Exception ex) when (ex.GetType().Name == "SqlException")
            {
                // SQL-specific errors
                var contextData = new Dictionary<string, object>
                {
                    { "ErrorNumber", ex.GetType().GetProperty("Errors")?.GetValue(ex) }
                };
                ExceptionLogger.LogFullException(_logger, ex, "SQL Database Connection Error", contextData);
                throw;
            }
            catch (Exception ex)
            {
                var contextData = new Dictionary<string, object>
                {
                    { "Method", nameof(GetOrCreateUserIdAsync) },
                    { "Timestamp", DateTime.UtcNow }
                };
                ExceptionLogger.LogFullException(_logger, ex, "Unexpected error in GetOrCreateUserIdAsync", contextData);
                throw;
            }
        }
    }
}
