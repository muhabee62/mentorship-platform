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
    public class PromoteUserToAdmin
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly AuthHelper _authHelper;
        private readonly ILogger<PromoteUserToAdmin> _logger;

        public PromoteUserToAdmin(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<PromoteUserToAdmin> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // Only Admins can promote users to Admin
        [RequireRole("Admin")]
        [Function("PromoteUserToAdmin")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/users/{id:int}/promote/admin")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing PromoteUserToAdmin request...");

            // Ensure the caller exists in DB (JIT provisioning)
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            // -----------------------------------------
            // Check if target user exists
            // -----------------------------------------
            var checkCmd = new SqlCommand(
                "SELECT Role, IsActive FROM Users WHERE UserId = @UserId",
                conn);

            checkCmd.Parameters.AddWithValue("@UserId", id);

            string? existingRole = null;
            bool isActive = false;

            using (var reader = await checkCmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"User with ID {id} not found.");
                    return notFound;
                }

                existingRole = reader.GetString(0);
                isActive = reader.GetBoolean(1);
            }

            // -----------------------------------------
            // IBAC: Admin cannot promote themselves
            // -----------------------------------------
            if (loggedInUserId == id)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You cannot promote yourself to Admin.");
                return forbidden;
            }

            // -----------------------------------------
            // Validate state
            // -----------------------------------------
            if (!isActive)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Cannot promote an inactive user.");
                return bad;
            }

            if (existingRole == "Admin")
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("User is already an Admin.");
                return conflict;
            }

            // -----------------------------------------
            // Promote user to Admin
            // -----------------------------------------
            var updateCmd = new SqlCommand(
                "UPDATE Users SET Role = 'Admin' WHERE UserId = @UserId",
                conn);

            updateCmd.Parameters.AddWithValue("@UserId", id);
            await updateCmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("User successfully promoted to Admin.");
            return response;
        }
    }
}