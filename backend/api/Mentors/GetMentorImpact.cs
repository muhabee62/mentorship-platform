using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using Microsoft.Data.SqlClient;
using System.Net;

namespace MentorshipPlatform.Api.Mentors
{
    public class GetMentorImpact
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetMentorImpact> _logger;
        private readonly AuthHelper _authHelper;

        public GetMentorImpact(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetMentorImpact> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin OR Mentor
        [RequireRole("Admin", "Mentor")]
        [Function("GetMentorImpact")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mentors/{id:int}/impact")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetMentorImpact request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's MentorId
            // -----------------------------------------
            var principal = context.GetAuthenticatedUser();
            bool isAdmin = principal.IsInRole("Admin");

            int? loggedInMentorId = null;

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var mentorCmd = new SqlCommand(
                    "SELECT MentorId FROM Mentors WHERE UserId = @UserId",
                    conn);

                mentorCmd.Parameters.AddWithValue("@UserId", loggedInUserId);

                var result = await mentorCmd.ExecuteScalarAsync();
                if (result != null)
                    loggedInMentorId = (int)result;
            }

            bool isMentor = loggedInMentorId != null;

            // -----------------------------------------
            // IBAC Enforcement
            // -----------------------------------------
            if (!isAdmin)
            {
                if (!isMentor)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("Only mentors can view mentor impact.");
                    return forbidden;
                }

                if (loggedInMentorId != id)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You may only view your own impact.");
                    return forbidden;
                }
            }

            // -----------------------------------------
            // Validate mentor exists
            // -----------------------------------------
            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var checkMentor = new SqlCommand(
                    "SELECT COUNT(*) FROM Mentors WHERE MentorId = @MentorId",
                    conn);

                checkMentor.Parameters.AddWithValue("@MentorId", id);

                if ((int)await checkMentor.ExecuteScalarAsync() == 0)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"Mentor with ID {id} not found.");
                    return notFound;
                }
            }

            // -----------------------------------------
            // Build impact DTO
            // -----------------------------------------
            var impact = new MentorImpactDto();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                // Session stats
                var sessionStatsSql = @"
                    SELECT 
                        COUNT(*) AS TotalSessions,
                        SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedSessions,
                        COUNT(DISTINCT MenteeId) AS UniqueMentees,
                        MIN(ScheduledStartUtc) AS FirstSession,
                        MAX(ScheduledStartUtc) AS LastSession
                    FROM Sessions
                    WHERE MentorId = @MentorId";

                using (var cmd = new SqlCommand(sessionStatsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MentorId", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        impact.TotalSessions = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        impact.CompletedSessions = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        impact.UniqueMentees = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        impact.FirstSessionUtc = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                        impact.LastSessionUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                    }
                }

                // Rating stats
                var ratingSql = @"
                    SELECT 
                        AVG(CAST(f.Rating AS FLOAT)) AS AvgRating,
                        MAX(f.CreatedAt) AS MostRecentFeedbackDate
                    FROM SessionFeedback f
                    INNER JOIN Sessions s ON f.SessionId = s.SessionId
                    WHERE s.MentorId = @MentorId AND f.Rating IS NOT NULL";

                using (var cmd = new SqlCommand(ratingSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MentorId", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        impact.AverageRating = reader.IsDBNull(0) ? null : reader.GetDouble(0);
                        impact.MostRecentFeedbackDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                    }
                }

                // Rating trend
                var trendSql = @"
                    SELECT TOP 5 f.Rating
                    FROM SessionFeedback f
                    INNER JOIN Sessions s ON f.SessionId = s.SessionId
                    WHERE s.MentorId = @MentorId AND f.Rating IS NOT NULL
                    ORDER BY f.CreatedAt DESC";

                impact.RatingTrend = new List<int>();

                using (var cmd = new SqlCommand(trendSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MentorId", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        impact.RatingTrend.Add(reader.GetInt32(0));
                    }
                }

                // Recent comments
                var commentsSql = @"
                    SELECT TOP 5 f.Comments, f.CreatedAt
                    FROM SessionFeedback f
                    INNER JOIN Sessions s ON f.SessionId = s.SessionId
                    WHERE s.MentorId = @MentorId AND f.Comments IS NOT NULL
                    ORDER BY f.CreatedAt DESC";

                impact.RecentComments = new List<FeedbackCommentDto>();

                using (var cmd = new SqlCommand(commentsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MentorId", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        impact.RecentComments.Add(new FeedbackCommentDto
                        {
                            Comment = reader.GetString(0),
                            CreatedAt = reader.GetDateTime(1)
                        });
                    }
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(impact);
            return response;
        }
    }

    public class MentorImpactDto
    {
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int UniqueMentees { get; set; }
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