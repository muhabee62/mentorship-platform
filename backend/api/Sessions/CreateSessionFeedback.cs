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

namespace MentorshipPlatform.Api.Sessions
{
    public class CreateSessionFeedback
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateSessionFeedback> _logger;
        private readonly AuthHelper _authHelper;

        public CreateSessionFeedback(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateSessionFeedback> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only mentees can submit feedback
        [RequireRole("Mentee")]
        [Function("CreateSessionFeedback")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sessions/{sessionId:int}/feedback")] HttpRequestData req,
            int sessionId,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateSessionFeedback request...");

            // -----------------------------------------
            // Identify logged-in mentee (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            int? loggedInMenteeId = null;

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var menteeCmd = new SqlCommand(
                    "SELECT MenteeId FROM Mentees WHERE UserId = @UserId",
                    conn);

                menteeCmd.Parameters.AddWithValue("@UserId", loggedInUserId);

                var result = await menteeCmd.ExecuteScalarAsync();
                if (result != null)
                    loggedInMenteeId = (int)result;
            }

            if (loggedInMenteeId == null)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("Only mentees can submit feedback.");
                return forbidden;
            }

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            var feedback = JsonSerializer.Deserialize<CreateSessionFeedbackDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (feedback == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid request body.");
                return bad;
            }

            using var conn2 = await _connectionFactory.CreateAsync();
            await conn2.OpenAsync();

            // -----------------------------------------
            // Fetch session (IBAC check)
            // -----------------------------------------
            var checkSession = new SqlCommand(@"
                SELECT MentorId, MenteeId
                FROM Sessions
                WHERE SessionId = @SessionId",
                conn2);

            checkSession.Parameters.AddWithValue("@SessionId", sessionId);

            int mentorId = 0;
            int menteeId = 0;

            using (var reader = await checkSession.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"Session with ID {sessionId} not found.");
                    return notFound;
                }

                mentorId = reader.GetInt32(0);
                menteeId = reader.GetInt32(1);
            }

            // -----------------------------------------
            // IBAC: Mentee can only submit feedback for their own sessions
            // -----------------------------------------
            if (loggedInMenteeId != menteeId)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only submit feedback for sessions you attended.");
                return forbidden;
            }

            // -----------------------------------------
            // Prevent duplicate feedback
            // -----------------------------------------
            var checkDuplicate = new SqlCommand(@"
                SELECT COUNT(*) FROM SessionFeedback
                WHERE SessionId = @SessionId AND SubmittedByUserId = @UserId",
                conn2);

            checkDuplicate.Parameters.AddWithValue("@SessionId", sessionId);
            checkDuplicate.Parameters.AddWithValue("@UserId", loggedInUserId);

            if ((int)await checkDuplicate.ExecuteScalarAsync() > 0)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("Feedback already submitted for this session.");
                return conflict;
            }

            // -----------------------------------------
            // Insert feedback (override SubmittedByUserId)
            // -----------------------------------------
            var cmd = new SqlCommand(@"
                INSERT INTO SessionFeedback
                (SessionId, SubmittedByUserId, Rating, Comments)
                VALUES (@SessionId, @UserId, @Rating, @Comments)",
                conn2);

            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            cmd.Parameters.AddWithValue("@UserId", loggedInUserId);
            cmd.Parameters.AddWithValue("@Rating", (object?)feedback.Rating ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Comments", (object?)feedback.Comments ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Feedback submitted successfully.");
            return response;
        }
    }

    public class CreateSessionFeedbackDto
    {
        public int SubmittedByUserId { get; set; } // Ignored for IBAC
        public int? Rating { get; set; }
        public string Comments { get; set; }
    }
}