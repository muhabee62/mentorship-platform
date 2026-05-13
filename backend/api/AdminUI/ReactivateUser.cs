using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using Microsoft.Data.SqlClient;
using System.Net;
using System.Threading.Tasks;

namespace MentorshipPlatform.Api.Admin
{
    public class ReactivateUser
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly AuthHelper _authHelper;
        private readonly ILogger<ReactivateUser> _logger;

        public ReactivateUser(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<ReactivateUser> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // Only Admins can reactivate users
        [RequireRole("Admin")]
        [Function("ReactivateUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/users/{id:int}/reactivate")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing ReactivateUser request...");

            // Ensure the caller exists in DB (JIT provisioning)
            await _authHelper.GetOrCreateUserIdAsync(context);

            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            // -----------------------------------------
            // Check if user exists
            // -----------------------------------------
            var checkCmd = new SqlCommand(
                "SELECT IsActive FROM Users WHERE UserId = @UserId",
                conn);

            checkCmd.Parameters.AddWithValue("@UserId", id);

            bool isActive;

            using (var reader = await checkCmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"User with ID {id} not found.");
                    return notFound;
                }

                isActive = reader.GetBoolean(0);
            }

            // -----------------------------------------
            // Validate state
            // -----------------------------------------
            if (isActive)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("User is already active.");
                return conflict;
            }

            // -----------------------------------------
            // Reactivate user
            // -----------------------------------------
            var updateCmd = new SqlCommand(
                "UPDATE Users SET IsActive = 1 WHERE UserId = @UserId",
                conn);

            updateCmd.Parameters.AddWithValue("@UserId", id);
            await updateCmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("User successfully reactivated.");
            return response;
        }
    }
}