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
    public class CreateUser
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateUser> _logger;
        private readonly AuthHelper _authHelper;

        public CreateUser(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateUser> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only Admin can create users
        [RequireRole("Admin")]
        [Function("CreateUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateUser request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var user = JsonSerializer.Deserialize<User>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (user == null ||
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Name))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid user payload.");
                return badResponse;
            }

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                // -----------------------------------------
                // Prevent duplicate email accounts
                // -----------------------------------------
                var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Users WHERE Email = @Email",
                    conn);

                checkCmd.Parameters.AddWithValue("@Email", user.Email);

                int exists = (int)await checkCmd.ExecuteScalarAsync();
                if (exists > 0)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync("A user with this email already exists.");
                    return conflict;
                }

                // -----------------------------------------
                // Insert new user
                // -----------------------------------------
                var cmd = new SqlCommand(@"
                    INSERT INTO Users (FullName, Email, Role, CreatedAt, IsActive)
                    VALUES (@FullName, @Email, @Role, @CreatedAt, @IsActive)",
                    conn);

                cmd.Parameters.AddWithValue("@FullName", user.Name);
                cmd.Parameters.AddWithValue("@Email", user.Email);
                cmd.Parameters.AddWithValue("@Role", user.Role);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@IsActive", true);

                await cmd.ExecuteNonQueryAsync();
            }

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("User created successfully.");
            return response;
        }
    }
}