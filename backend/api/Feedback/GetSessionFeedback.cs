using System.Collections.Generic;
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
    public class GetSessionFeedback
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetSessionFeedback> _logger;
        private readonly AuthHelper _authHelper;

        public GetSessionFeedback(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetSessionFeedback> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin OR Mentor OR Mentee
        [RequireRole("Admin", "Mentor", "Mentee")]
        [Function("GetSessionFeedback")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions/{sessionId:int}/feedback")] HttpRequestData req,
            int sessionId,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetSessionFeedback request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Fetch feedback for the session
            // -----------------------------------------
            var results = new List<object>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT FeedbackId, SessionId, SubmittedByUserId, Rating, Comments, CreatedAt
                    FROM SessionFeedback
                    WHERE SessionId = @SessionId
                    ORDER BY CreatedAt DESC;",
                    conn);

                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        FeedbackId = reader.GetInt32(0),
                        SessionId = reader.GetInt32(1),
                        SubmittedByUserId = reader.GetInt32(2),
                        Rating = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                        Comments = reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(results));
            return response;
        }
    }
}