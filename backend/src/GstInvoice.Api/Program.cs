using Anthropic;
using GstInvoice.Api.Data;
using GstInvoice.Api.Services;
using GstInvoice.Core.Extraction;
using GstInvoice.Core.Validation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<ExtractionOptions>(builder.Configuration.GetSection(ExtractionOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InvoiceValidator>();
builder.Services.AddScoped<InvoiceService>();

// Pick the AI provider from config. "Fake" lets anyone run the app without an API key.
var provider = builder.Configuration[$"{ExtractionOptions.Section}:Provider"] ?? "Fake";
if (provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
{
    // AnthropicClient reads ANTHROPIC_API_KEY from the environment. Never put the key in appsettings.
    builder.Services.AddSingleton(_ => new AnthropicClient());
    builder.Services.AddScoped<IInvoiceExtractor, ClaudeInvoiceExtractor>();
}
else
{
    builder.Services.AddSingleton<IInvoiceExtractor, FakeInvoiceExtractor>();
}

const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.MapControllers();
app.MapHealthChecks("/health");

// Apply EF Core migrations on startup. Fine for a single-instance demo; in a real
// deployment you'd run migrations as a separate pipeline step.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.Logger.LogInformation("Extraction provider: {Provider}", provider);
app.Run();

public partial class Program;
