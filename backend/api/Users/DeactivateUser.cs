using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using Microsoft.Data.SqlClient;
using System.Net;

namespace MentorshipPlatform.Api.Users
{
    public class DeactivateUser
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<DeactivateUser> _logger;
        private readonly AuthHelper _authHelper;

        public DeactivateUser(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<DeactivateUser> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only Admin can deactivate users
        [RequireRole("Admin")]
        [Function("DeactivateUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "users/{id:int}")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing DeactivateUser request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // IBAC: Prevent Admin from deactivating self
            // -----------------------------------------
            if (loggedInUserId == id)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You cannot deactivate your own account.");
                return forbidden;
            }

            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            // -----------------------------------------
            // Check if user exists
            // -----------------------------------------
            var checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Users WHERE UserId = @UserId",
                conn);

            checkCmd.Parameters.AddWithValue("@UserId", id);

            int exists = (int)await checkCmd.ExecuteScalarAsync();
            if (exists == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"User with ID {id} not found.");
                return notFound;
            }

            // -----------------------------------------
            // Deactivate user
            // -----------------------------------------
            var cmd = new SqlCommand(@"
                UPDATE Users
                SET IsActive = 0
                WHERE UserId = @UserId",
                conn);

            cmd.Parameters.AddWithValue("@UserId", id);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("User deactivated successfully.");
            return response;
        }
    }
}