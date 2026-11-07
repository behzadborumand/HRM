namespace HRMS.Core.Services;

public static class OrganizationLinkRules
{
    public static bool IsAllowedUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && !url.Any(char.IsControl)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
}
