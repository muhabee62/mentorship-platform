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
    public class GetMenteesForMentor
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetMenteesForMentor> _logger;
        private readonly AuthHelper _authHelper;

        public GetMenteesForMentor(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetMenteesForMentor> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Mentor only
        [RequireRole("Mentor")]
        [Function("GetMenteesForMentor")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mentors/{id:int}/mentees")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetMenteesForMentor request...");

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
            // IBAC: Mentor can only view their own mentees
            // -----------------------------------------
            if (loggedInMentorId != id)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You may only view your own mentees.");
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

                int exists = (int)await checkMentor.ExecuteScalarAsync();
                if (exists == 0)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"Mentor with ID {id} not found.");
                    return notFound;
                }
            }

            // -----------------------------------------
            // Fetch mentees for this mentor
            // -----------------------------------------
            var mentees = new List<MenteeForMentorDto>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        me.MenteeId,
                        u.FullName AS MenteeName,
                        me.Goals,
                        m.CreatedAt AS MatchedOn
                    FROM Matches m
                    INNER JOIN Mentees me ON m.MenteeId = me.MenteeId
                    INNER JOIN Users u ON me.UserId = u.UserId
                    WHERE m.MentorId = @MentorId
                    ORDER BY m.CreatedAt DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MentorId", id);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    mentees.Add(new MenteeForMentorDto
                    {
                        MenteeId = reader.GetInt32(0),
                        MenteeName = reader.GetString(1),
                        Goals = reader.IsDBNull(2) ? null : reader.GetString(2),
                        MatchedOn = reader.GetDateTime(3)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(mentees);
            return response;
        }
    }

    public class MenteeForMentorDto
    {
        public int MenteeId { get; set; }
        public string MenteeName { get; set; }
        public string Goals { get; set; }
        public System.DateTime MatchedOn { get; set; }
    }
}