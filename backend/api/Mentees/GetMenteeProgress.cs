using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using Microsoft.Data.SqlClient;
using System.Net;

namespace MentorshipPlatform.Api.Mentees
{
    public class GetMenteeProgress
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetMenteeProgress> _logger;
        private readonly AuthHelper _authHelper;

        public GetMenteeProgress(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetMenteeProgress> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin OR Mentor OR Mentee
        [RequireRole("Admin", "Mentor", "Mentee")]
        [Function("GetMenteeProgress")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mentees/{id:int}/progress")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetMenteeProgress request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's role (Mentor/Mentee)
            // -----------------------------------------
            int? loggedInMentorId = null;
            int? loggedInMenteeId = null;

            var principal = context.GetAuthenticatedUser();
            bool isAdmin = principal.IsInRole("Admin");

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
            // IBAC Enforcement
            // -----------------------------------------
            if (!isAdmin)
            {
                if (isMentee)
                {
                    // Mentee can only view their own progress
                    if (loggedInMenteeId != id)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You may only view your own progress.");
                        return forbidden;
                    }
                }
                else if (isMentor)
                {
                    // Mentor can only view progress for mentees they are matched with
                    using var conn = await _connectionFactory.CreateAsync();
                    await conn.OpenAsync();

                    var matchCmd = new SqlCommand(@"
                        SELECT COUNT(*)
                        FROM Matches
                        WHERE MentorId = @MentorId AND MenteeId = @MenteeId",
                        conn);

                    matchCmd.Parameters.AddWithValue("@MentorId", loggedInMentorId);
                    matchCmd.Parameters.AddWithValue("@MenteeId", id);

                    int matchCount = (int)await matchCmd.ExecuteScalarAsync();

                    if (matchCount == 0)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You may only view progress for mentees you are matched with.");
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
            // Validate mentee exists
            // -----------------------------------------
            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var checkMentee = new SqlCommand(
                    "SELECT COUNT(*) FROM Mentees WHERE MenteeId = @MenteeId",
                    conn);

                checkMentee.Parameters.AddWithValue("@MenteeId", id);

                if ((int)await checkMentee.ExecuteScalarAsync() == 0)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"Mentee with ID {id} not found.");
                    return notFound;
                }
            }

            // -----------------------------------------
            // Build progress DTO
            // -----------------------------------------
            var progress = new MenteeProgressDto();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                // Session stats
                var sessionStatsSql = @"
                    SELECT 
                        COUNT(*) AS TotalSessions,
                        SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedSessions,
                        MIN(ScheduledStartUtc) AS FirstSession,
                        MAX(ScheduledStartUtc) AS LastSession
                    FROM Sessions
                    WHERE MenteeId = @MenteeId";

                using (var cmd = new SqlCommand(sessionStatsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MenteeId", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        progress.TotalSessions = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        progress.CompletedSessions = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        progress.FirstSessionUtc = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                        progress.LastSessionUtc = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                    }
                }

                // Rating stats
                var ratingSql = @"
                    SELECT 
                        AVG(CAST(f.Rating AS FLOAT)) AS AvgRating,
                        MAX(f.CreatedAt) AS MostRecentFeedbackDate
                    FROM SessionFeedback f
                    INNER JOIN Sessions s ON f.SessionId = s.SessionId
                    WHERE s.MenteeId = @MenteeId AND f.Rating IS NOT NULL";

                using (var cmd = new SqlCommand(ratingSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MenteeId", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        progress.AverageRating = reader.IsDBNull(0) ? null : reader.GetDouble(0);
                        progress.MostRecentFeedbackDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                    }
                }

                // Rating trend
                var trendSql = @"
                    SELECT TOP 5 f.Rating
                    FROM SessionFeedback f
                    INNER JOIN Sessions s ON f.SessionId = s.SessionId
                    WHERE s.MenteeId = @MenteeId AND f.Rating IS NOT NULL
                    ORDER BY f.CreatedAt DESC";

                progress.RatingTrend = new List<int>();

                using (var cmd = new SqlCommand(trendSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MenteeId", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        progress.RatingTrend.Add(reader.GetInt32(0));
                    }
                }

                // Recent comments
                var commentsSql = @"
                    SELECT TOP 5 f.Comments, f.CreatedAt
                    FROM SessionFeedback f
                    INNER JOIN Sessions s ON f.SessionId = s.SessionId
                    WHERE s.MenteeId = @MenteeId AND f.Comments IS NOT NULL
                    ORDER BY f.CreatedAt DESC";

                progress.RecentComments = new List<FeedbackCommentDto>();

                using (var cmd = new SqlCommand(commentsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MenteeId", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        progress.RecentComments.Add(new FeedbackCommentDto
                        {
                            Comment = reader.GetString(0),
                            CreatedAt = reader.GetDateTime(1)
                        });
                    }
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(progress);
            return response;
        }
    }

    public class MenteeProgressDto
    {
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public System.DateTime? FirstSessionUtc { get; set; }
        public System.DateTime? LastSessionUtc { get; set; }
        public double? AverageRating { get; set; }
        public System.DateTime? MostRecentFeedbackDate { get; set; }
        public List<int> RatingTrend { get; set; }
        public List<FeedbackCommentDto> RecentComments { get; set; }
    }

    public class FeedbackCommentDto
    {
        public string Comment { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }
}