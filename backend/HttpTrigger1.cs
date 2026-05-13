using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MentorshipPlatform.Common;

namespace Company.Function;

public class HttpTrigger1
{
    private readonly ILogger<HttpTrigger1> _logger;
    private readonly SqlConnectionFactory _connectionFactory;

    public HttpTrigger1(ILogger<HttpTrigger1> logger, SqlConnectionFactory connectionFactory)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    [Function("HttpTrigger1")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Backend API received a request.");

        using var connection = await _connectionFactory.CreateAsync();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP 1 'Backend is working'";

        var result = await command.ExecuteScalarAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(result?.ToString() ?? "Backend is working");

        return response;
    }
}