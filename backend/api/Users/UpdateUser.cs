using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using Microsoft.Data.SqlClient;
using System.Net;
using System.Text.Json;

namespace MentorshipPlatform.Api.Users
{
    public class UpdateUser
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<UpdateUser> _logger;
        private readonly AuthHelper _authHelper;

        public UpdateUser(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<UpdateUser> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin, Mentor, Mentee
        [RequireRole("Admin", "Mentor", "Mentee")]
        [Function("UpdateUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "users/{id:int}")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing UpdateUser request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's role
            // -----------------------------------------
            var principal = context.GetAuthenticatedUser();
            bool isAdmin = principal?.IsInRole("Admin") ?? false;

            // -----------------------------------------
            // IBAC:
            // - Admin can update anyone
            // - Mentor/Mentee can only update themselves
            // -----------------------------------------
            if (!isAdmin && loggedInUserId != id)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only update your own profile.");
                return forbidden;
            }

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            var update = JsonSerializer.Deserialize<UserUpdateDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (update == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid request body.");
                return bad;
            }

            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            // -----------------------------------------
            // Ensure user exists
            // -----------------------------------------
            var checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Users WHERE UserId = @UserId",
                conn);

            checkCmd.Parameters.AddWithValue("@UserId", id);

            var result = await checkCmd.ExecuteScalarAsync();
            int exists = result != null && result != DBNull.Value ? (int)result : 0;
            if (exists == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"User with ID {id} not found.");
                return notFound;
            }

            // -----------------------------------------
            // Perform update
            // -----------------------------------------
            var cmd = new SqlCommand(@"
                UPDATE Users
                SET FullName = @FullName,
                    Email = @Email,
                    Role = @Role,
                    IsActive = @IsActive
                WHERE UserId = @UserId",
                conn);

            cmd.Parameters.AddWithValue("@UserId", id);
            cmd.Parameters.AddWithValue("@FullName", update.FullName);
            cmd.Parameters.AddWithValue("@Email", update.Email);
            cmd.Parameters.AddWithValue("@Role", update.Role);
            cmd.Parameters.AddWithValue("@IsActive", update.IsActive);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("User updated successfully.");
            return response;
        }
    }

    public class UserUpdateDto
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
    }
}