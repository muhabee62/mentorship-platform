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
    public class GetSessionsForMentee
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetSessionsForMentee> _logger;
        private readonly AuthHelper _authHelper;

        public GetSessionsForMentee(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetSessionsForMentee> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only mentees can view their own sessions
        [RequireRole("Mentee")]
        [Function("GetSessionsForMentee")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mentees/{id:int}/sessions")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetSessionsForMentee request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's MenteeId
            // -----------------------------------------
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
                await forbidden.WriteStringAsync("Only mentees can access this endpoint.");
                return forbidden;
            }

            // -----------------------------------------
            // IBAC: Mentee can only view their own sessions
            // -----------------------------------------
            if (loggedInMenteeId != id)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You may only view your own sessions.");
                return forbidden;
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
            // Fetch sessions
            // -----------------------------------------
            var sessions = new List<SessionForMenteeDto>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        s.SessionId,
                        s.MentorId,
                        u.FullName AS MentorName,
                        s.ScheduledStartUtc,
                        s.ScheduledEndUtc,
                        s.Status,
                        s.Notes,
                        s.CreatedAt
                    FROM Sessions s
                    INNER JOIN Mentors mt ON s.MentorId = mt.MentorId
                    INNER JOIN Users u ON mt.UserId = u.UserId
                    WHERE s.MenteeId = @MenteeId
                    ORDER BY s.ScheduledStartUtc DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MenteeId", id);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sessions.Add(new SessionForMenteeDto
                    {
                        SessionId = reader.GetInt32(0),
                        MentorId = reader.GetInt32(1),
                        MentorName = reader.GetString(2),
                        ScheduledStartUtc = reader.GetDateTime(3),
                        ScheduledEndUtc = reader.GetDateTime(4),
                        Status = reader.GetString(5),
                        Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                        CreatedAt = reader.GetDateTime(7)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sessions);
            return response;
        }
    }

    public class SessionForMenteeDto
    {
        public int SessionId { get; set; }
        public int MentorId { get; set; }
        public string MentorName { get; set; }
        public System.DateTime ScheduledStartUtc { get; set; }
        public System.DateTime ScheduledEndUtc { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }
}