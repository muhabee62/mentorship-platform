using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using Microsoft.Data.SqlClient;
using System.Net;

namespace MentorshipPlatform.Api.Sessions
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

        // IBAC: Admin, Mentor, Mentee
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
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's role
            // -----------------------------------------
            var principal = context.GetAuthenticatedUser();
            bool isAdmin = principal.IsInRole("Admin");

            int? loggedInMentorId = null;
            int? loggedInMenteeId = null;

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                // Mentor check
                var mentorCmd = new SqlCommand(
                    "SELECT MentorId FROM Mentors WHERE UserId = @UserId",
                    conn);
                mentorCmd.Parameters.AddWithValue("@UserId", loggedInUserId);
                var mentorResult = await mentorCmd.ExecuteScalarAsync();
                if (mentorResult != null)
                    loggedInMentorId = (int)mentorResult;

                // Mentee check
                var menteeCmd = new SqlCommand(
                    "SELECT MenteeId FROM Mentees WHERE UserId = @UserId",
                    conn);
                menteeCmd.Parameters.AddWithValue("@UserId", loggedInUserId);
                var menteeResult = await menteeCmd.ExecuteScalarAsync();
                if (menteeResult != null)
                    loggedInMenteeId = (int)menteeResult;
            }

            bool isMentor = loggedInMentorId != null;
            bool isMentee = loggedInMenteeId != null;

            // -----------------------------------------
            // Fetch session ownership
            // -----------------------------------------
            int sessionMentorId = 0;
            int sessionMenteeId = 0;

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var checkSession = new SqlCommand(@"
                    SELECT MentorId, MenteeId
                    FROM Sessions
                    WHERE SessionId = @SessionId",
                    conn);

                checkSession.Parameters.AddWithValue("@SessionId", sessionId);

                using var reader = await checkSession.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"Session with ID {sessionId} not found.");
                    return notFound;
                }

                sessionMentorId = reader.GetInt32(0);
                sessionMenteeId = reader.GetInt32(1);
            }

            // -----------------------------------------
            // IBAC Enforcement
            // -----------------------------------------
            if (!isAdmin)
            {
                if (isMentor)
                {
                    if (loggedInMentorId != sessionMentorId)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You may only view feedback for your own sessions.");
                        return forbidden;
                    }
                }
                else if (isMentee)
                {
                    if (loggedInMenteeId != sessionMenteeId)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You may only view feedback for sessions you attended.");
                        return forbidden;
                    }
                }
                else
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("Unauthorized role.");
                    return forbidden;
                }
            }

            // -----------------------------------------
            // Fetch feedback
            // -----------------------------------------
            var feedbackList = new List<SessionFeedbackDto>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        f.FeedbackId,
                        f.SubmittedByUserId,
                        u.FullName AS SubmittedByName,
                        f.Rating,
                        f.Comments,
                        f.CreatedAt
                    FROM SessionFeedback f
                    INNER JOIN Users u ON f.SubmittedByUserId = u.UserId
                    WHERE f.SessionId = @SessionId
                    ORDER BY f.CreatedAt DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    feedbackList.Add(new SessionFeedbackDto
                    {
                        FeedbackId = reader.GetInt32(0),
                        SubmittedByUserId = reader.GetInt32(1),
                        SubmittedByName = reader.GetString(2),
                        Rating = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        Comments = reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(feedbackList);
            return response;
        }
    }

    public class SessionFeedbackDto
    {
        public int FeedbackId { get; set; }
        public int SubmittedByUserId { get; set; }
        public string SubmittedByName { get; set; }
        public int? Rating { get; set; }
        public string Comments { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }
}