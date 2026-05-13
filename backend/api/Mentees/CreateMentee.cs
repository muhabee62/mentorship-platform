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

namespace MentorshipPlatform.Api.Mentees
{
    public class CreateMentee
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateMentee> _logger;
        private readonly AuthHelper _authHelper;

        public CreateMentee(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateMentee> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin only
        [RequireRole("Admin")]
        [Function("CreateMentee")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mentees")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateMentee request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            var mentee = JsonSerializer.Deserialize<CreateMenteeDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (mentee == null || mentee.UserId <= 0)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid request body.");
                return bad;
            }

            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            // -----------------------------------------
            // Validate user exists
            // -----------------------------------------
            var checkUser = new SqlCommand(
                "SELECT COUNT(*) FROM Users WHERE UserId = @UserId",
                conn);

            checkUser.Parameters.AddWithValue("@UserId", mentee.UserId);

            int exists = (int)await checkUser.ExecuteScalarAsync();
            if (exists == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"User with ID {mentee.UserId} does not exist.");
                return notFound;
            }

            // -----------------------------------------
            // Insert mentee record
            // -----------------------------------------
            var cmd = new SqlCommand(@"
                INSERT INTO Mentees (UserId, Goals)
                VALUES (@UserId, @Goals)",
                conn);

            cmd.Parameters.AddWithValue("@UserId", mentee.UserId);
            cmd.Parameters.AddWithValue("@Goals", (object?)mentee.Goals ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Mentee created successfully.");
            return response;
        }
    }

    public class CreateMenteeDto
    {
        public int UserId { get; set; }
        public string Goals { get; set; }
    }
}