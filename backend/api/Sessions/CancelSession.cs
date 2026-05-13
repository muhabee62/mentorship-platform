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
    public class CancelSession
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CancelSession> _logger;
        private readonly AuthHelper _authHelper;

        public CancelSession(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CancelSession> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only mentors can cancel sessions
        [RequireRole("Mentor")]
        [Function("CancelSession")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sessions/{id:int}")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CancelSession request...");

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
                await forbidden.WriteStringAsync("Only mentors can cancel sessions.");
                return forbidden;
            }

            // -----------------------------------------
            // Parse optional cancellation notes
            // -----------------------------------------
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            var cancel = string.IsNullOrWhiteSpace(body)
                ? null
                : JsonSerializer.Deserialize<CancelSessionDto>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            using var conn2 = await _connectionFactory.CreateAsync();
            await conn2.OpenAsync();

            // -----------------------------------------
            // Fetch existing session (IBAC check)
            // -----------------------------------------
            var getSessionCmd = new SqlCommand(@"
                SELECT MentorId
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
            // IBAC: Mentor can only cancel their own sessions
            // -----------------------------------------
            if (existingMentorId != loggedInMentorId)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only cancel sessions you own.");
                return forbidden;
            }

            // -----------------------------------------
            // Perform cancellation
            // -----------------------------------------
            var cmd = new SqlCommand(@"
                UPDATE Sessions
                SET 
                    Status = 'Cancelled',
                    Notes = COALESCE(@Notes, Notes)
                WHERE SessionId = @SessionId",
                conn2);

            cmd.Parameters.AddWithValue("@SessionId", id);
            cmd.Parameters.AddWithValue("@Notes", (object?)cancel?.Notes ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Session cancelled successfully.");
            return response;
        }
    }

    public class CancelSessionDto
    {
        public string Notes { get; set; }
    }
}