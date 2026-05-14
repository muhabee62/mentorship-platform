using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using MentorshipPlatform.Common;
using MentorshipPlatform.Api.Auth;
using MentorshipPlatform.Services;

// Enable Azure CLI credential for DefaultAzureCredential when running locally
Environment.SetEnvironmentVariable("AZURE_IDENTITY_ENABLE_CLI_CREDENTIAL", "true");

var builder = FunctionsApplication.CreateBuilder(args);

// ---------------------------------------------------------
// 1. Load CIAM Role Settings from configuration
// ---------------------------------------------------------
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    return new CiamRoleSettings
    {
        TenantId     = config["Ciam:TenantId"]     ?? config["Ciam_TenantId"],
        ClientId     = config["Ciam:ClientId"]     ?? config["Ciam_ClientId"],
        ClientSecret = config["Ciam:ClientSecret"] ?? config["Ciam_ClientSecret"],
        ApiAppId     = config["Ciam:ApiAppId"]     ?? config["Ciam_ApiAppId"],
        AdminRoleId  = config["Ciam:AdminRoleId"]  ?? config["Ciam_AdminRoleId"],
        MentorRoleId = config["Ciam:MentorRoleId"] ?? config["Ciam_MentorRoleId"],
        MenteeRoleId = config["Ciam:MenteeRoleId"] ?? config["Ciam_MenteeRoleId"]
    };
});

// ---------------------------------------------------------
// 2. Register core services
// ---------------------------------------------------------
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<AuthSettings>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<CiamRoleService>();   // ⭐ NEW
builder.Services.AddSingleton<AuthHelper>();

// ---------------------------------------------------------
// 3. Register middleware
// ---------------------------------------------------------
builder.Services.AddSingleton<JwtValidationMiddleware>();
builder.Services.AddSingleton<AuthFilter>();

builder.UseMiddleware<JwtValidationMiddleware>();
builder.UseMiddleware<AuthFilter>();

// ---------------------------------------------------------
// 4. Application Insights
// ---------------------------------------------------------
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
