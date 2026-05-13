using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using System.Net;

namespace MentorshipPlatform.Api.Feedback
{
    public class SubmitSessionFeedback
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<SubmitSessionFeedback> _logger;
        private readonly AuthHelper _authHelper;

        public SubmitSessionFeedback(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<SubmitSessionFeedback> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        public class FeedbackDto
        {
            public int SessionId { get; set; }
            public int? Rating { get; set; }
            public string? Comments { get; set; }
        }

        // IBAC: Only mentees can submit feedback
        [RequireRole("Mentee")]
        [Function("SubmitSessionFeedback")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sessions/{sessionId:int}/feedback")] HttpRequestData req,
            int sessionId,
            FunctionContext context)
        {
            _logger.LogInformation("Processing SubmitSessionFeedback request...");

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<FeedbackDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null || dto.SessionId != sessionId)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid feedback payload.");
                return bad;
            }

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Ensure logged-in user is a mentee
            // -----------------------------------------
            int? menteeId = null;

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var menteeCmd = new SqlCommand(
                    "SELECT MenteeId FROM Mentees WHERE UserId = @UserId",
                    conn);

                menteeCmd.Parameters.AddWithValue("@UserId", loggedInUserId);

                var result = await menteeCmd.ExecuteScalarAsync();
                if (result != null)
                    menteeId = (int)result;
            }

            if (menteeId == null)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("Only mentees can submit session feedback.");
                return forbidden;
            }

            // -----------------------------------------
            // Insert feedback (SubmittedByUserId comes from token)
            // -----------------------------------------
            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    INSERT INTO SessionFeedback (SessionId, SubmittedByUserId, Rating, Comments, CreatedAt)
                    VALUES (@SessionId, @SubmittedByUserId, @Rating, @Comments, @CreatedAt);",
                    conn);

                cmd.Parameters.AddWithValue("@SessionId", sessionId);
                cmd.Parameters.AddWithValue("@SubmittedByUserId", loggedInUserId);
                cmd.Parameters.AddWithValue("@Rating", (object?)dto.Rating ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Comments", (object?)dto.Comments ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();
            }

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Session feedback submitted.");
            return response;
        }
    }
}