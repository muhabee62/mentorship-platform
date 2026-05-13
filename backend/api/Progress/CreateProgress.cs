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

namespace MentorshipPlatform.Api.Progress
{
    public class CreateProgress
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateProgress> _logger;
        private readonly AuthHelper _authHelper;

        public CreateProgress(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateProgress> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        public class ProgressDto
        {
            public int? GoalId { get; set; }
            public int? SessionId { get; set; }
            public string Source { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
        }

        // IBAC: Mentor only
        [RequireRole("Mentor")]
        [Function("CreateProgress")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "progress")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateProgress request...");

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<ProgressDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null || string.IsNullOrWhiteSpace(dto.Source) || string.IsNullOrWhiteSpace(dto.Summary))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid progress payload.");
                return bad;
            }

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
                await forbidden.WriteStringAsync("Only mentors can create progress entries.");
                return forbidden;
            }

            // -----------------------------------------
            // IBAC: Validate GoalId (if provided)
            // -----------------------------------------
            if (dto.GoalId != null)
            {
                using var conn = await _connectionFactory.CreateAsync();
                await conn.OpenAsync();

                var goalCmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Goals g
                    INNER JOIN Matches m ON g.MenteeId = m.MenteeId
                    WHERE g.GoalId = @GoalId AND m.MentorId = @MentorId",
                    conn);

                goalCmd.Parameters.AddWithValue("@GoalId", dto.GoalId);
                goalCmd.Parameters.AddWithValue("@MentorId", loggedInMentorId);

                int count = (int)await goalCmd.ExecuteScalarAsync();

                if (count == 0)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You may only create progress for goals belonging to your matched mentees.");
                    return forbidden;
                }
            }

            // -----------------------------------------
            // IBAC: Validate SessionId (if provided)
            // -----------------------------------------
            if (dto.SessionId != null)
            {
                using var conn = await _connectionFactory.CreateAsync();
                await conn.OpenAsync();

                var sessionCmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Sessions
                    WHERE SessionId = @SessionId AND MentorId = @MentorId",
                    conn);

                sessionCmd.Parameters.AddWithValue("@SessionId", dto.SessionId);
                sessionCmd.Parameters.AddWithValue("@MentorId", loggedInMentorId);

                int count = (int)await sessionCmd.ExecuteScalarAsync();

                if (count == 0)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You may only create progress for your own sessions.");
                    return forbidden;
                }
            }

            // -----------------------------------------
            // Insert progress entry
            // -----------------------------------------
            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    INSERT INTO Progress (GoalId, SessionId, UserId, Source, Summary, CreatedAt)
                    VALUES (@GoalId, @SessionId, @UserId, @Source, @Summary, @CreatedAt);",
                    conn);

                cmd.Parameters.AddWithValue("@GoalId", (object?)dto.GoalId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SessionId", (object?)dto.SessionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserId", loggedInUserId); // IBAC: enforce identity
                cmd.Parameters.AddWithValue("@Source", dto.Source);
                cmd.Parameters.AddWithValue("@Summary", dto.Summary);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();
            }

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Progress entry created.");
            return response;
        }
    }
}