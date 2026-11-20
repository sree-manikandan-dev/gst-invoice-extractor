using System.Net;
using System.Text;
using GstInvoice.Core.Extraction;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GstInvoice.Tests;

public class GeminiInvoiceExtractorTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static GeminiInvoiceExtractor Create(StubHandler handler, string? key = "test-key") =>
        new(new HttpClient(handler),
            Options.Create(new ExtractionOptions { GeminiApiKey = key, GeminiModel = "gemini-flash-latest" }),
            NullLogger<GeminiInvoiceExtractor>.Instance);

    [Fact]
    public async Task Parses_json_from_the_first_candidate_and_normalizes_gstin()
    {
        const string body = """
            {"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"SupplierGstin\":\"32aapfu0939f1z4\",\"Total\":1180.5}"}]}}],
             "usageMetadata":{"promptTokenCount":1200,"candidatesTokenCount":80}}
            """;
        var handler = new StubHandler(HttpStatusCode.OK, body);

        var result = await Create(handler).ExtractAsync([1, 2, 3], "image/png");

        Assert.Equal("32AAPFU0939F1Z4", result.Invoice.SupplierGstin);
        Assert.Equal(1180.5m, result.Invoice.Total);
        Assert.Equal(1200, result.InputTokens);
        Assert.Equal("test-key", handler.Request!.Headers.GetValues("x-goog-api-key").Single());
        Assert.Contains("gemini-flash-latest:generateContent", handler.Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Rate_limit_becomes_a_friendly_error()
    {
        var ex = await Assert.ThrowsAsync<ExtractionException>(() =>
            Create(new StubHandler(HttpStatusCode.TooManyRequests, "{}")).ExtractAsync([1], "image/png"));
        Assert.Contains("rate limit", ex.Message);
    }

    [Fact]
    public async Task Missing_api_key_fails_fast() =>
        await Assert.ThrowsAsync<ExtractionException>(() =>
            Create(new StubHandler(HttpStatusCode.OK, "{}"), key: null).ExtractAsync([1], "image/png"));
}
