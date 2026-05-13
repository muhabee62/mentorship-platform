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
    public class PromoteUserToMentor
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly AuthHelper _authHelper;
        private readonly ILogger<PromoteUserToMentor> _logger;

        public PromoteUserToMentor(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<PromoteUserToMentor> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // Only Admins can promote users
        [RequireRole("Admin")]
        [Function("PromoteUserToMentor")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/users/{id:int}/promote/mentor")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing PromoteUserToMentor request...");

            // Ensure the caller exists in DB (JIT provisioning)
            await _authHelper.GetOrCreateUserIdAsync(context);

            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            // -----------------------------------------
            // Check if user exists
            // -----------------------------------------
            var checkCmd = new SqlCommand(
                "SELECT Role, IsActive FROM Users WHERE UserId = @UserId",
                conn);

            checkCmd.Parameters.AddWithValue("@UserId", id);

            string existingRole = null;
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
            // Validate state
            // -----------------------------------------
            if (!isActive)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Cannot promote an inactive user.");
                return bad;
            }

            if (existingRole == "Mentor")
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("User is already a Mentor.");
                return conflict;
            }

            // -----------------------------------------
            // Promote user to Mentor
            // -----------------------------------------
            var updateCmd = new SqlCommand(
                "UPDATE Users SET Role = 'Mentor' WHERE UserId = @UserId",
                conn);

            updateCmd.Parameters.AddWithValue("@UserId", id);
            await updateCmd.ExecuteNonQueryAsync();

            // -----------------------------------------
            // Ensure Mentors table entry exists
            // -----------------------------------------
            var mentorCheckCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Mentors WHERE UserId = @UserId",
                conn);

            mentorCheckCmd.Parameters.AddWithValue("@UserId", id);

            int mentorExists = (int)await mentorCheckCmd.ExecuteScalarAsync();

            if (mentorExists == 0)
            {
                var insertCmd = new SqlCommand(
                    "INSERT INTO Mentors (UserId) VALUES (@UserId)",
                    conn);

                insertCmd.Parameters.AddWithValue("@UserId", id);
                await insertCmd.ExecuteNonQueryAsync();
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("User successfully promoted to Mentor.");
            return response;
        }
    }
}