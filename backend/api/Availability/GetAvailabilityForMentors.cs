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

namespace MentorshipPlatform.Api.Availability
{
    public class GetAvailabilityForMentor
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetAvailabilityForMentor> _logger;
        private readonly AuthHelper _authHelper;

        public GetAvailabilityForMentor(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetAvailabilityForMentor> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin OR Mentor
        [RequireRole("Admin", "Mentor")]
        [Function("GetAvailabilityForMentor")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "availability/{mentorId:int}")] HttpRequestData req,
            int mentorId,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetAvailabilityForMentor request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine if logged-in user is a mentor
            // -----------------------------------------
            int? loggedInMentorId = null;
            var principal = context.GetAuthenticatedUser();
            bool isAdmin = principal.IsInRole("Admin");

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
                    await forbidden.WriteStringAsync("Only mentors can view mentor availability.");
                    return forbidden;
                }

                if (loggedInMentorId != mentorId)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You may only view your own availability.");
                    return forbidden;
                }
            }

            // -----------------------------------------
            // Fetch availability
            // -----------------------------------------
            var results = new List<object>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT AvailabilityId, MentorId, StartTimeUtc, EndTimeUtc, IsRecurring
                    FROM Availability
                    WHERE MentorId = @MentorId;",
                    conn);

                cmd.Parameters.AddWithValue("@MentorId", mentorId);

                var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        AvailabilityId = reader.GetInt32(0),
                        MentorId = reader.GetInt32(1),
                        StartTimeUtc = reader.GetDateTime(2),
                        EndTimeUtc = reader.GetDateTime(3),
                        IsRecurring = reader.GetBoolean(4)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(results));
            return response;
        }
    }
}