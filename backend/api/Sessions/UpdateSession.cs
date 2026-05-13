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
    public class UpdateSession
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<UpdateSession> _logger;
        private readonly AuthHelper _authHelper;

        public UpdateSession(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<UpdateSession> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only mentors can update sessions
        [RequireRole("Mentor")]
        [Function("UpdateSession")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sessions/{id:int}")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing UpdateSession request...");

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
                await forbidden.WriteStringAsync("Only mentors can update sessions.");
                return forbidden;
            }

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            var update = JsonSerializer.Deserialize<UpdateSessionDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (update == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid request body.");
                return bad;
            }

            using var conn2 = await _connectionFactory.CreateAsync();
            await conn2.OpenAsync();

            // -----------------------------------------
            // Fetch existing session (IBAC check)
            // -----------------------------------------
            var getSessionCmd = new SqlCommand(@"
                SELECT MentorId, MenteeId
                FROM Sessions
                WHERE SessionId = @SessionId",
                conn2);

            getSessionCmd.Parameters.AddWithValue("@SessionId", id);

            int? existingMentorId = null;

            using (var reader = await getSessionCmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"Session with ID {id} not found.");
                    return notFound;
                }

                existingMentorId = reader.GetInt32(0);
            }

            // -----------------------------------------
            // IBAC: Mentor can only update their own sessions
            // -----------------------------------------
            if (existingMentorId != loggedInMentorId)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only update sessions you own.");
                return forbidden;
            }

            // -----------------------------------------
            // Validate time range if provided
            // -----------------------------------------
            if (update.ScheduledStartUtc.HasValue && update.ScheduledEndUtc.HasValue)
            {
                if (update.ScheduledEndUtc <= update.ScheduledStartUtc)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("ScheduledEndUtc must be after ScheduledStartUtc.");
                    return bad;
                }
            }

            // -----------------------------------------
            // Perform update
            // -----------------------------------------
            var cmd = new SqlCommand(@"
                UPDATE Sessions
                SET 
                    ScheduledStartUtc = COALESCE(@Start, ScheduledStartUtc),
                    ScheduledEndUtc = COALESCE(@End, ScheduledEndUtc),
                    Status = COALESCE(@Status, Status),
                    Notes = COALESCE(@Notes, Notes)
                WHERE SessionId = @SessionId",
                conn2);

            cmd.Parameters.AddWithValue("@SessionId", id);
            cmd.Parameters.AddWithValue("@Start", (object?)update.ScheduledStartUtc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@End", (object?)update.ScheduledEndUtc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)update.Status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Notes", (object?)update.Notes ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Session updated successfully.");
            return response;
        }
    }

    public class UpdateSessionDto
    {
        public DateTime? ScheduledStartUtc { get; set; }
        public DateTime? ScheduledEndUtc { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
    }
}