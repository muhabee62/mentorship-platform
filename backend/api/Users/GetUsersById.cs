using System.Linq;
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
    public class GetUserById
    {
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<GetUserById> _logger;
        private readonly AuthHelper _authHelper;

        public GetUserById(
            SqlConnectionFactory connectionFactory,
            AuthHelper authHelper,
            ILogger<GetUserById> logger)
        {
            _connectionFactory = connectionFactory;
            _authHelper = authHelper;
            _logger = logger;
        }

        // IBAC: Admin, Mentor, Mentee
        [RequireRole("Admin", "Mentor", "Mentee")]
        [Function("GetUserById")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{id:int}")] HttpRequestData req,
            int id,
            FunctionContext context)
        {
            _logger.LogInformation("Processing GetUserById request...");

            // -----------------------------------------
            // Identify logged-in user (JIT provisioning)
            // -----------------------------------------
            int loggedInUserId = await _authHelper.GetOrCreateUserIdAsync(context);

            // -----------------------------------------
            // Determine logged-in user's role
            // -----------------------------------------
            var principal = context.GetAuthenticatedUser();
            bool isAdmin = principal.IsInRole("Admin");

            // -----------------------------------------
            // IBAC:
            // - Admin can view any user
            // - Mentor/Mentee can only view themselves
            // -----------------------------------------
            if (!isAdmin && loggedInUserId != id)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You can only view your own profile.");
                return forbidden;
            }

            // -----------------------------------------
            // Fetch user from database
            // -----------------------------------------
            using var conn = await _connectionFactory.CreateAsync();
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT UserId, FullName, Email, Role, CreatedAt, IsActive
                FROM Users
                WHERE UserId = @UserId",
                conn);

            cmd.Parameters.AddWithValue("@UserId", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"User with ID {id} not found.");
                return notFound;
            }

            var user = new UserDto
            {
                UserId = reader.GetInt32(0),
                FullName = reader.GetString(1),
                Email = reader.GetString(2),
                Role = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                IsActive = reader.GetBoolean(5)
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(user);
            return response;
        }
    }

    public class UserListDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}