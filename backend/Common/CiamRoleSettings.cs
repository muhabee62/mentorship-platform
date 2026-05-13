namespace MentorshipPlatform.Common
{
    public class CiamRoleSettings
    {
        public string TenantId { get; set; } = default!;
        public string ClientId { get; set; } = default!;
        public string ClientSecret { get; set; } = default!;
        public string ApiAppId { get; set; } = default!;

        public string AdminRoleId { get; set; } = default!;
        public string MentorRoleId { get; set; } = default!;
        public string MenteeRoleId { get; set; } = default!;
    }
}
