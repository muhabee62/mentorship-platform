using Microsoft.Extensions.Configuration;

namespace MentorshipPlatform.Common
{
    public class AuthSettings
    {
        public string Authority { get; }
        public string Audience { get; }

        public AuthSettings(IConfiguration config)
        {
            Authority = config["Auth:Authority"];
            Audience = config["Auth:Audience"];
        }
    }
}

