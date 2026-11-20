using Npgsql;

namespace GstInvoice.Api.Data;

public static class DatabaseUrl
{
    /// <summary>
    /// Converts postgres://user:pass@host:port/db?sslmode=require into an Npgsql connection string.
    /// Returns null when no URL is given, so the appsettings connection string is used.
    /// </summary>
    public static string? ToNpgsql(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.StartsWith("postgres://") && !url.StartsWith("postgresql://")) return url;

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            // Hosted databases require TLS; local Docker Postgres doesn't, so only demand it off localhost.
            SslMode = uri.Host is "localhost" or "127.0.0.1" or "db" ? SslMode.Prefer : SslMode.Require,
        };
        return builder.ConnectionString;
    }
}
