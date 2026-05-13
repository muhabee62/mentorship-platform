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

namespace MentorshipPlatform.Api.Availability
{
    public class CreateAvailability
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateAvailability> _logger;
        private readonly AuthHelper _authHelper;

        public CreateAvailability(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateAvailability> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        public class AvailabilityDto
        {
            public DateTime StartTimeUtc { get; set; }
            public DateTime EndTimeUtc { get; set; }
            public bool IsRecurring { get; set; }
        }

        // IBAC: Mentor only
        [RequireRole("Mentor")]
        [Function("CreateAvailability")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "availability")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateAvailability request...");

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<AvailabilityDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null || dto.StartTimeUtc >= dto.EndTimeUtc)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid availability payload.");
                return bad;
            }

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine if user is a mentor
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
                await forbidden.WriteStringAsync("Only mentors can create availability.");
                return forbidden;
            }

            // -----------------------------------------
            // Insert availability
            // -----------------------------------------
            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    INSERT INTO Availability (MentorId, StartTimeUtc, EndTimeUtc, IsRecurring)
                    VALUES (@MentorId, @StartTimeUtc, @EndTimeUtc, @IsRecurring);",
                    conn);

                cmd.Parameters.AddWithValue("@MentorId", loggedInMentorId);
                cmd.Parameters.AddWithValue("@StartTimeUtc", dto.StartTimeUtc);
                cmd.Parameters.AddWithValue("@EndTimeUtc", dto.EndTimeUtc);
                cmd.Parameters.AddWithValue("@IsRecurring", dto.IsRecurring);

                await cmd.ExecuteNonQueryAsync();
            }

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Availability created.");
            return response;
        }
    }
}