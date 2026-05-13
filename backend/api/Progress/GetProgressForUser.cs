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

namespace MentorshipPlatform.Api.Progress
{
    public class GetProgressForUser
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetProgressForUser> _logger;
        private readonly AuthHelper _authHelper;

        public GetProgressForUser(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetProgressForUser> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin OR Mentor OR Mentee
        [RequireRole("Admin", "Mentor", "Mentee")]
        [Function("GetProgressForUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "progress/{userId:int}")] HttpRequestData req,
            int userId,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetProgressForUser request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's roles
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
            // Determine target user's role
            // -----------------------------------------
            int? targetMentorId = null;
            int? targetMenteeId = null;

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                // Target mentor?
                var mentorCmd = new SqlCommand(
                    "SELECT MentorId FROM Mentors WHERE UserId = @UserId",
                    conn);
                mentorCmd.Parameters.AddWithValue("@UserId", userId);
                var mentorResult = await mentorCmd.ExecuteScalarAsync();
                if (mentorResult != null)
                    targetMentorId = (int)mentorResult;

                // Target mentee?
                var menteeCmd = new SqlCommand(
                    "SELECT MenteeId FROM Mentees WHERE UserId = @UserId",
                    conn);
                menteeCmd.Parameters.AddWithValue("@UserId", userId);
                var menteeResult = await menteeCmd.ExecuteScalarAsync();
                if (menteeResult != null)
                    targetMenteeId = (int)menteeResult;
            }

            // -----------------------------------------
            // IBAC Enforcement
            // -----------------------------------------
            if (!isAdmin)
            {
                if (isMentee)
                {
                    // Mentee can only view their own progress
                    if (loggedInUserId != userId)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You may only view your own progress.");
                        return forbidden;
                    }
                }
                else if (isMentor)
                {
                    // Mentor viewing their own progress
                    if (loggedInUserId == userId)
                    {
                        // allowed
                    }
                    // Mentor viewing a mentee's progress
                    else if (targetMenteeId != null)
                    {
                        using var conn = await _connectionFactory.CreateAsync();
                        await conn.OpenAsync();

                        var matchCmd = new SqlCommand(@"
                            SELECT COUNT(*)
                            FROM Matches
                            WHERE MentorId = @MentorId AND MenteeId = @MenteeId",
                            conn);

                        matchCmd.Parameters.AddWithValue("@MentorId", loggedInMentorId);
                        matchCmd.Parameters.AddWithValue("@MenteeId", targetMenteeId);

                        int count = (int)await matchCmd.ExecuteScalarAsync();

                        if (count == 0)
                        {
                            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                            await forbidden.WriteStringAsync("You may only view progress for mentees you are matched with.");
                            return forbidden;
                        }
                    }
                    // Mentor trying to view another mentor or admin
                    else
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You are not authorized to view this user's progress.");
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
            // Fetch progress entries
            // -----------------------------------------
            var results = new List<object>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT ProgressId, GoalId, SessionId, UserId, Source, Summary, CreatedAt
                    FROM Progress
                    WHERE UserId = @UserId
                    ORDER BY CreatedAt DESC;",
                    conn);

                cmd.Parameters.AddWithValue("@UserId", userId);

                var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        ProgressId = reader.GetInt32(0),
                        GoalId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                        SessionId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                        UserId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                        Source = reader.GetString(4),
                        Summary = reader.GetString(5),
                        CreatedAt = reader.GetDateTime(6)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(results));
            return response;
        }
    }
}