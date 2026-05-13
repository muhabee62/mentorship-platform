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

namespace MentorshipPlatform.Api.Goals
{
    public class GetGoalsForMentee
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetGoalsForMentee> _logger;
        private readonly AuthHelper _authHelper;

        public GetGoalsForMentee(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetGoalsForMentee> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin OR Mentor OR Mentee
        [RequireRole("Admin", "Mentor", "Mentee")]
        [Function("GetGoalsForMentee")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "goals/mentee/{menteeId:int}")] HttpRequestData req,
            int menteeId,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetGoalsForMentee request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's roles (Mentor/Mentee)
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
                    // Mentee can only view their own goals
                    if (loggedInMenteeId != menteeId)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You may only view your own goals.");
                        return forbidden;
                    }
                }
                else if (isMentor)
                {
                    // Mentor can only view goals for mentees they are matched with
                    using var conn = await _connectionFactory.CreateAsync();
                    await conn.OpenAsync();

                    var matchCmd = new SqlCommand(@"
                        SELECT COUNT(*)
                        FROM Matches
                        WHERE MentorId = @MentorId AND MenteeId = @MenteeId",
                        conn);

                    matchCmd.Parameters.AddWithValue("@MentorId", loggedInMentorId);
                    matchCmd.Parameters.AddWithValue("@MenteeId", menteeId);

                    int matchCount = (int)await matchCmd.ExecuteScalarAsync();

                    if (matchCount == 0)
                    {
                        var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                        await forbidden.WriteStringAsync("You may only view goals for mentees you are matched with.");
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
            // Fetch goals
            // -----------------------------------------
            var results = new List<object>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT GoalId, MenteeId, Title, Description, StartDate, DueDate, Status
                    FROM Goals
                    WHERE MenteeId = @MenteeId
                    ORDER BY StartDate DESC;",
                    conn);

                cmd.Parameters.AddWithValue("@MenteeId", menteeId);

                var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        GoalId = reader.GetInt32(0),
                        MenteeId = reader.GetInt32(1),
                        Title = reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        StartDate = reader.GetDateTime(4),
                        DueDate = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                        Status = reader.GetString(6)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(results));
            return response;
        }
    }
}