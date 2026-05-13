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
    public class GetSessionsForMentor
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetSessionsForMentor> _logger;
        private readonly AuthHelper _authHelper;

        public GetSessionsForMentor(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetSessionsForMentor> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Mentor only
        [RequireRole("Mentor")]
        [Function("GetSessionsForMentor")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mentors/{id:int}/sessions")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetSessionsForMentor request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's MentorId
            // -----------------------------------------
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

            if (loggedInMentorId == null)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("Only mentors can access this endpoint.");
                return forbidden;
            }

            // -----------------------------------------
            // IBAC: Mentor can only view their own sessions
            // -----------------------------------------
            if (loggedInMentorId != id)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You may only view your own sessions.");
                return forbidden;
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
            // Fetch sessions
            // -----------------------------------------
            var sessions = new List<SessionForMentorDto>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        s.SessionId,
                        s.MenteeId,
                        u.FullName AS MenteeName,
                        s.ScheduledStartUtc,
                        s.ScheduledEndUtc,
                        s.Status,
                        s.Notes,
                        s.CreatedAt
                    FROM Sessions s
                    INNER JOIN Mentees me ON s.MenteeId = me.MenteeId
                    INNER JOIN Users u ON me.UserId = u.UserId
                    WHERE s.MentorId = @MentorId
                    ORDER BY s.ScheduledStartUtc DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MentorId", id);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sessions.Add(new SessionForMentorDto
                    {
                        SessionId = reader.GetInt32(0),
                        MenteeId = reader.GetInt32(1),
                        MenteeName = reader.GetString(2),
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

    public class SessionForMentorDto
    {
        public int SessionId { get; set; }
        public int MenteeId { get; set; }
        public string MenteeName { get; set; }
        public System.DateTime ScheduledStartUtc { get; set; }
        public System.DateTime ScheduledEndUtc { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }
}