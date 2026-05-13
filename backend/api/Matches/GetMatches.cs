using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using Microsoft.Data.SqlClient;
using System.Net;

namespace MentorshipPlatform.Api.Matches
{
    public class GetMatches
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetMatches> _logger;
        private readonly AuthHelper _authHelper;

        public GetMatches(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetMatches> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin OR Mentor OR Mentee
        [RequireRole("Admin", "Mentor", "Mentee")]
        [Function("GetMatches")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "matches")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetMatches request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Fetch matches
            // -----------------------------------------
            var matches = new List<MatchDto>();

            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            var sql = @"
                SELECT 
                    m.MatchId,
                    m.MentorId,
                    u1.FullName AS MentorName,
                    m.MenteeId,
                    u2.FullName AS MenteeName,
                    m.CreatedAt
                FROM Matches m
                INNER JOIN Mentors mt ON m.MentorId = mt.MentorId
                INNER JOIN Users u1 ON mt.UserId = u1.UserId
                INNER JOIN Mentees me ON m.MenteeId = me.MenteeId
                INNER JOIN Users u2 ON me.UserId = u2.UserId
                ORDER BY m.CreatedAt DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                matches.Add(new MatchDto
                {
                    MatchId = reader.GetInt32(0),
                    MentorId = reader.GetInt32(1),
                    MentorName = reader.GetString(2),
                    MenteeId = reader.GetInt32(3),
                    MenteeName = reader.GetString(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(matches);
            return response;
        }
    }

    public class MatchDto
    {
        public int MatchId { get; set; }
        public int MentorId { get; set; }
        public string MentorName { get; set; }
        public int MenteeId { get; set; }
        public string MenteeName { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }
}