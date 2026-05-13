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
    public class CreateSession
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateSession> _logger;
        private readonly AuthHelper _authHelper;

        public CreateSession(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateSession> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only Mentors can create sessions
        [RequireRole("Mentor")]
        [Function("CreateSession")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sessions")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateSession request...");

            // -----------------------------------------
            // Identify logged-in mentor (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

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
                await forbidden.WriteStringAsync("Only mentors can create sessions.");
                return forbidden;
            }

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            var session = JsonSerializer.Deserialize<CreateSessionDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (session == null ||
                session.MentorId <= 0 ||
                session.MenteeId <= 0 ||
                session.ScheduledStartUtc == default ||
                session.ScheduledEndUtc == default)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid request body.");
                return bad;
            }

            // -----------------------------------------
            // IBAC: Mentor can only create sessions for themselves
            // -----------------------------------------
            if (loggedInMentorId != session.MentorId)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only create sessions for yourself.");
                return forbidden;
            }

            if (session.ScheduledEndUtc <= session.ScheduledStartUtc)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("ScheduledEndUtc must be after ScheduledStartUtc.");
                return bad;
            }

            using var conn2 = await _connectionFactory.CreateAsync();
            await conn2.OpenAsync();

            // Validate mentor exists
            var checkMentor = new SqlCommand("SELECT COUNT(*) FROM Mentors WHERE MentorId = @MentorId", conn2);
            checkMentor.Parameters.AddWithValue("@MentorId", session.MentorId);
            if ((int)await checkMentor.ExecuteScalarAsync() == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Mentor with ID {session.MentorId} does not exist.");
                return notFound;
            }

            // Validate mentee exists
            var checkMentee = new SqlCommand("SELECT COUNT(*) FROM Mentees WHERE MenteeId = @MenteeId", conn2);
            checkMentee.Parameters.AddWithValue("@MenteeId", session.MenteeId);
            if ((int)await checkMentee.ExecuteScalarAsync() == 0)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Mentee with ID {session.MenteeId} does not exist.");
                return notFound;
            }

            // Validate they are matched
            var checkMatch = new SqlCommand(
                @"SELECT COUNT(*) FROM Matches 
                  WHERE MentorId = @MentorId AND MenteeId = @MenteeId", conn2);

            checkMatch.Parameters.AddWithValue("@MentorId", session.MentorId);
            checkMatch.Parameters.AddWithValue("@MenteeId", session.MenteeId);

            if ((int)await checkMatch.ExecuteScalarAsync() == 0)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("This mentor and mentee are not matched.");
                return conflict;
            }

            // Insert session
            var cmd = new SqlCommand(
                @"INSERT INTO Sessions 
                  (MentorId, MenteeId, ScheduledStartUtc, ScheduledEndUtc, Status, Notes, CreatedAt)
                  VALUES (@MentorId, @MenteeId, @Start, @End, 'Scheduled', @Notes, SYSUTCDATETIME())",
                conn2);

            cmd.Parameters.AddWithValue("@MentorId", session.MentorId);
            cmd.Parameters.AddWithValue("@MenteeId", session.MenteeId);
            cmd.Parameters.AddWithValue("@Start", session.ScheduledStartUtc);
            cmd.Parameters.AddWithValue("@End", session.ScheduledEndUtc);
            cmd.Parameters.AddWithValue("@Notes", (object?)session.Notes ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Session scheduled successfully.");
            return response;
        }
    }

    public class CreateSessionDto
    {
        public int MentorId { get; set; }
        public int MenteeId { get; set; }
        public DateTime ScheduledStartUtc { get; set; }
        public DateTime ScheduledEndUtc { get; set; }
        public string Notes { get; set; }
    }
}