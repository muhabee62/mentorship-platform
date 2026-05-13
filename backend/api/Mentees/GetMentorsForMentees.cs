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
    public class GetMentorsForMentee
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetMentorsForMentee> _logger;
        private readonly AuthHelper _authHelper;

        public GetMentorsForMentee(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetMentorsForMentee> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only mentees can view their mentors
        [RequireRole("Mentee")]
        [Function("GetMentorsForMentee")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mentees/{id:int}/mentors")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetMentorsForMentee request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine if logged-in user is a mentee
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
                await forbidden.WriteStringAsync("Only mentees can view their mentors.");
                return forbidden;
            }

            // Mentee can only view their own mentors
            if (loggedInMenteeId != id)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You may only view your own mentors.");
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

                int exists = (int)await checkMentee.ExecuteScalarAsync();
                if (exists == 0)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"Mentee with ID {id} not found.");
                    return notFound;
                }
            }

            // -----------------------------------------
            // Fetch mentors for this mentee
            // -----------------------------------------
            var mentors = new List<MentorForMenteeDto>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        mt.MentorId,
                        u.FullName AS MentorName,
                        mt.Expertise,
                        m.CreatedAt AS MatchedOn
                    FROM Matches m
                    INNER JOIN Mentors mt ON m.MentorId = mt.MentorId
                    INNER JOIN Users u ON mt.UserId = u.UserId
                    WHERE m.MenteeId = @MenteeId
                    ORDER BY m.CreatedAt DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MenteeId", id);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    mentors.Add(new MentorForMenteeDto
                    {
                        MentorId = reader.GetInt32(0),
                        MentorName = reader.GetString(1),
                        Expertise = reader.IsDBNull(2) ? null : reader.GetString(2),
                        MatchedOn = reader.GetDateTime(3)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(mentors);
            return response;
        }
    }

    public class MentorForMenteeDto
    {
        public int MentorId { get; set; }
        public string MentorName { get; set; }
        public string Expertise { get; set; }
        public System.DateTime MatchedOn { get; set; }
    }
}