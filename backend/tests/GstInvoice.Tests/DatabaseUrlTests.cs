using GstInvoice.Api.Data;

namespace GstInvoice.Tests;

public class DatabaseUrlTests
{
    [Fact]
    public void Neon_style_url_becomes_npgsql_connection_string()
    {
        var cs = DatabaseUrl.ToNpgsql("postgresql://app_user:p%40ss@ep-cool-name.ap-southeast-1.aws.neon.tech/gst?sslmode=require");
        Assert.Contains("Host=ep-cool-name.ap-southeast-1.aws.neon.tech", cs);
        Assert.Contains("Database=gst", cs);
        Assert.Contains("Username=app_user", cs);
        Assert.Contains("Password=p@ss", cs);
        Assert.Contains("SSL Mode=Require", cs);
    }

    [Fact]
    public void Missing_url_returns_null() => Assert.Null(DatabaseUrl.ToNpgsql(null));

    [Fact]
    public void Key_value_connection_string_passes_through() =>
        Assert.Equal("Host=db;Database=x", DatabaseUrl.ToNpgsql("Host=db;Database=x"));
}
