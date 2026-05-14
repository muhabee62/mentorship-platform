using Microsoft.Data.SqlClient;

namespace MentorshipPlatform.Common
{
    public class UserRepository
    {
        private readonly SqlConnectionFactory _factory;

        public UserRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<int?> GetUserIdByEmailAsync(string email)
        {
            using var conn = await _factory.CreateAsync();
            await conn.OpenAsync();

            var cmd = new SqlCommand(
                "SELECT UserId FROM Users WHERE Email = @Email AND IsActive = 1",
                conn);

            cmd.Parameters.AddWithValue("@Email", email);

            var result = await cmd.ExecuteScalarAsync();
            return result == null ? null : (int?)result;
        }

        public async Task<string?> GetUserEmailByIdAsync(int userId)
        {
            using var conn = await _factory.CreateAsync();
            await conn.OpenAsync();

            var cmd = new SqlCommand(
                "SELECT Email FROM Users WHERE UserId = @UserId",
                conn);

            cmd.Parameters.AddWithValue("@UserId", userId);

            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        // JIT provisioning with role support
        public async Task<int> GetOrCreateUserAsync(string email, string role)
        {
            using var conn = await _factory.CreateAsync();
            await conn.OpenAsync();

            // 1. Check if user already exists
            var checkCmd = new SqlCommand(
                "SELECT UserId FROM Users WHERE Email = @Email AND IsActive = 1",
                conn);

            checkCmd.Parameters.AddWithValue("@Email", email);

            var existing = await checkCmd.ExecuteScalarAsync();
            if (existing != null)
                return (int)existing;

            // 2. Create new user with role
            var insertCmd = new SqlCommand(@"
                INSERT INTO Users (Email, Role, IsActive, CreatedAt)
                OUTPUT INSERTED.UserId
                VALUES (@Email, @Role, 1, @CreatedAt);",
                conn);

            insertCmd.Parameters.AddWithValue("@Email", email);
            insertCmd.Parameters.AddWithValue("@Role", role);
            insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            var result = await insertCmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("Failed to insert user");
            var newUserId = (int)result;

            // 3. Insert into correct role table
            await InsertIntoRoleTableAsync(conn, newUserId, role);

            return newUserId;
        }

        public async Task UpdateUserRoleAsync(int userId, string newRole)
        {
            using var conn = await _factory.CreateAsync();
            await conn.OpenAsync();

            // 1. Get current role
            var getCmd = new SqlCommand(
                "SELECT Role FROM Users WHERE UserId = @UserId",
                conn);

            getCmd.Parameters.AddWithValue("@UserId", userId);
            var currentRole = (string?)await getCmd.ExecuteScalarAsync();

            // 2. Update Users.Role
            var updateCmd = new SqlCommand(
                "UPDATE Users SET Role = @Role WHERE UserId = @UserId",
                conn);

            updateCmd.Parameters.AddWithValue("@Role", newRole);
            updateCmd.Parameters.AddWithValue("@UserId", userId);
            await updateCmd.ExecuteNonQueryAsync();

            // 3. Remove from old role table
            if (!string.IsNullOrEmpty(currentRole))
            {
                var oldTable = currentRole switch
                {
                    "Admin" => "Admins",
                    "Mentor" => "Mentors",
                    _ => "Mentees"
                };

                var deleteCmd = new SqlCommand(
                    $"DELETE FROM {oldTable} WHERE UserId = @UserId",
                    conn);

                deleteCmd.Parameters.AddWithValue("@UserId", userId);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            // 4. Insert into new role table
            await InsertIntoRoleTableAsync(conn, userId, newRole);
        }

        private async Task InsertIntoRoleTableAsync(SqlConnection conn, int userId, string role)
        {
            string table = role switch
            {
                "Admin" => "Admins",
                "Mentor" => "Mentors",
                _ => "Mentees"
            };

            var cmd = new SqlCommand(
                $"INSERT INTO {table} (UserId) VALUES (@UserId)",
                conn);

            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
