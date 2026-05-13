using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using Microsoft.Data.SqlClient;
using System.Net;

namespace MentorshipPlatform.Api.Users
{
    public class GetUsers
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetUsers> _logger;
        private readonly AuthHelper _authHelper;

        public GetUsers(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetUsers> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Only Admin can list all users
        [RequireRole("Admin")]
        [Function("GetUsers")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users")] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetUsers request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Parse optional query filter
            // -----------------------------------------
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var activeParam = query["active"];

            string sql = "SELECT UserId, FullName, Email, Role, CreatedAt, IsActive FROM Users";

            bool hasFilter = false;
            bool filterValue = false;

            if (!string.IsNullOrWhiteSpace(activeParam) &&
                bool.TryParse(activeParam, out bool parsed))
            {
                hasFilter = true;
                filterValue = parsed;
                sql += " WHERE IsActive = @IsActive";
            }

            var users = new List<UserDto>();

            using (var conn = await _connectionFactory.CreateAsync())
            {
                await conn.OpenAsync();

                using var cmd = new SqlCommand(sql, conn);

                if (hasFilter)
                {
                    cmd.Parameters.AddWithValue("@IsActive", filterValue);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    users.Add(new UserDto
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Email = reader.GetString(2),
                        Role = reader.GetString(3),
                        CreatedAt = reader.GetDateTime(4),
                        IsActive = reader.GetBoolean(5)
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(users);
            return response;
        }
    }

    public class UserDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}