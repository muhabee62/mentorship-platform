using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Core;

namespace MentorshipPlatform.Common
{
    public class SqlConnectionFactory
    {
        private readonly IConfiguration _config;

        public SqlConnectionFactory(IConfiguration config)
        {
            _config = config;
        }

        public Task<SqlConnection> CreateAsync()
        {
            var connectionString = _config["SqlConnectionString"];
            var connection = new SqlConnection(connectionString);
            return Task.FromResult(connection);
        }
    }
}