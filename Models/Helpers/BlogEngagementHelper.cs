using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Blogger_website.Models.Helpers;

public static class BlogEngagementHelper
{
    private const string VisitorCookieName = "bloghub_visitor";

    public static string GetLikeKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
                return userId;
        }

        if (!context.Request.Cookies.TryGetValue(VisitorCookieName, out var visitorId) || string.IsNullOrEmpty(visitorId))
        {
            visitorId = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append(VisitorCookieName, visitorId, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(2),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
        }

        return "v:" + visitorId;
    }

    public static string BuildShareUrl(HttpContext context, string slug)
    {
        var baseUrl = GetPublicBaseUrl(context);
        return $"{baseUrl}/Blog/Details/{slug}";
    }

    public static string BuildShareText(string title, string shareUrl)
        => $"{title.Trim()}\n\nRead here: {shareUrl}";

    private static string GetPublicBaseUrl(HttpContext context)
    {
        var config = context.RequestServices.GetService<IConfiguration>();
        var configured = config?["Site:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
            return configured;

        var request = context.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
    }
}
