using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TheFloor;

public sealed class LocalHostAccess
{
    private const string Cookie = "thefloor-host";
    private readonly string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public IResult SignIn(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        var host = context.Request.Host.Host.Trim('[', ']');
        var localHost = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out var parsed) && IPAddress.IsLoopback(parsed);
        if (address is null || !IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address) ||
            !localHost || context.Request.Headers["Sec-Fetch-Site"] == "cross-site")
            return Results.Json(new { error = "Open the host interface on the computer running the server." }, statusCode: 403);

        context.Response.Cookies.Append(Cookie, token, new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = context.Request.IsHttps,
            Path = "/", IsEssential = true,
        });
        context.Response.Headers.CacheControl = "no-store";
        return Results.NoContent();
    }

    public bool IsAuthorized(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Cookie, out var cookie) && cookie.Length == token.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(cookie), Encoding.ASCII.GetBytes(token));
}
