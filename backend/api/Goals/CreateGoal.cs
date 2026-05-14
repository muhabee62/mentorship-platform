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

namespace MentorshipPlatform.Api.Goals
{
    public class CreateGoal
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<CreateGoal> _logger;
        private readonly AuthHelper _authHelper;

        public CreateGoal(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<CreateGoal> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        public class GoalDto
        {
            public int MenteeId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime? DueDate { get; set; }
            public string Status { get; set; } = "Active";
        }

        // IBAC: Admin OR Mentee
        [RequireRole("Admin", "Mentee")]
        [Function("CreateGoal")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "goals")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing CreateGoal request...");

            // -----------------------------------------
            // Parse request body
            // -----------------------------------------
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<GoalDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null || string.IsNullOrWhiteSpace(dto.Title))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid goal payload.");
                return bad;
            }

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine if user is a mentee
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

            var principal = context.GetAuthenticatedUser();
            bool isAdmin = principal?.IsInRole("Admin") ?? false;
            bool isMentee = loggedInMenteeId != null;

            // -----------------------------------------
            // IBAC Enforcement
            // -----------------------------------------
            if (!isAdmin)
            {
                if (!isMentee)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("Only mentees can create goals.");
                    return forbidden;
                }

                if (loggedInMenteeId != dto.MenteeId)
                {
                    var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbidden.WriteStringAsync("You may only create goals for yourself.");
                    return forbidden;
                }
            }

            // -----------------------------------------
            // Insert goal
            // -----------------------------------------
            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    INSERT INTO Goals (MenteeId, Title, Description, StartDate, DueDate, Status)
                    VALUES (@MenteeId, @Title, @Description, @StartDate, @DueDate, @Status);",
                    conn);

                cmd.Parameters.AddWithValue("@MenteeId", dto.MenteeId);
                cmd.Parameters.AddWithValue("@Title", dto.Title);
                cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@StartDate", dto.StartDate);
                cmd.Parameters.AddWithValue("@DueDate", (object?)dto.DueDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", dto.Status);

                await cmd.ExecuteNonQueryAsync();
            }

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync("Goal created successfully.");
            return response;
        }
    }
}