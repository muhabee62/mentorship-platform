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

namespace MentorshipPlatform.Api.Mentors
{
    public class CreateMentor
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateMentor> _logger;
        private readonly AuthHelper _authHelper;

        public CreateMentor(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateMentor> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin only
        [RequireRole("Admin")]
        [Function("CreateMentor")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mentors")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateMentor request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            var mentor = JsonSerializer.Deserialize<CreateMentorDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (mentor == null || mentor.UserId <= 0)
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

            checkUser.Parameters.AddWithValue("@UserId", mentor.UserId);

            int exists = (int)await checkUser.ExecuteScalarAsync();
            if (exists == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"User with ID {mentor.UserId} does not exist.");
                return notFound;
            }

            // -----------------------------------------
            // Insert mentor record
            // -----------------------------------------
            var cmd = new SqlCommand(@"
                INSERT INTO Mentors (UserId, Expertise, Bio)
                VALUES (@UserId, @Expertise, @Bio)",
                conn);

            cmd.Parameters.AddWithValue("@UserId", mentor.UserId);
            cmd.Parameters.AddWithValue("@Expertise", (object?)mentor.Expertise ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Bio", (object?)mentor.Bio ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Mentor created successfully.");
            return response;
        }
    }

    public class CreateMentorDto
    {
        public int UserId { get; set; }
        public string Expertise { get; set; }
        public string Bio { get; set; }
    }
}