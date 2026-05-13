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

namespace MentorshipPlatform.Api.Matches
{
    public class CreateMatch
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateMatch> _logger;
        private readonly AuthHelper _authHelper;

        public CreateMatch(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateMatch> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin only
        [RequireRole("Admin")]
        [Function("CreateMatch")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "matches")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateMatch request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            var match = JsonSerializer.Deserialize<CreateMatchDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (match == null || match.MentorId <= 0 || match.MenteeId <= 0)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid request body.");
                return bad;
            }

            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            // -----------------------------------------
            // Validate mentor exists
            // -----------------------------------------
            var checkMentor = new SqlCommand(
                "SELECT COUNT(*) FROM Mentors WHERE MentorId = @MentorId",
                conn);

            checkMentor.Parameters.AddWithValue("@MentorId", match.MentorId);

            int mentorExists = (int)await checkMentor.ExecuteScalarAsync();
            if (mentorExists == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Mentor with ID {match.MentorId} does not exist.");
                return notFound;
            }

            // -----------------------------------------
            // Validate mentee exists
            // -----------------------------------------
            var checkMentee = new SqlCommand(
                "SELECT COUNT(*) FROM Mentees WHERE MenteeId = @MenteeId",
                conn);

            checkMentee.Parameters.AddWithValue("@MenteeId", match.MenteeId);

            int menteeExists = (int)await checkMentee.ExecuteScalarAsync();
            if (menteeExists == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Mentee with ID {match.MenteeId} does not exist.");
                return notFound;
            }

            // -----------------------------------------
            // Prevent duplicate match
            // -----------------------------------------
            var checkDuplicate = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM Matches 
                WHERE MentorId = @MentorId AND MenteeId = @MenteeId",
                conn);

            checkDuplicate.Parameters.AddWithValue("@MentorId", match.MentorId);
            checkDuplicate.Parameters.AddWithValue("@MenteeId", match.MenteeId);

            int duplicate = (int)await checkDuplicate.ExecuteScalarAsync();
            if (duplicate > 0)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("This mentor and mentee are already matched.");
                return conflict;
            }

            // -----------------------------------------
            // Insert match
            // -----------------------------------------
            var cmd = new SqlCommand(@"
                INSERT INTO Matches (MentorId, MenteeId)
                VALUES (@MentorId, @MenteeId)",
                conn);

            cmd.Parameters.AddWithValue("@MentorId", match.MentorId);
            cmd.Parameters.AddWithValue("@MenteeId", match.MenteeId);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Match created successfully.");
            return response;
        }
    }

    public class CreateMatchDto
    {
        public int MentorId { get; set; }
        public int MenteeId { get; set; }
    }
}