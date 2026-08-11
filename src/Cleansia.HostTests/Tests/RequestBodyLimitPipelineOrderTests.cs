using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Why the oversize body on the real host is answered 413 and not 500, reduced to the one variable:
/// whether the read that trips Kestrel's limit happens upstream or downstream of
/// <c>UseExceptionHandler</c>.
///
/// <para>Two hosts, identical but for the order of those two hops. Upstream, the exception never reaches
/// the handler and Kestrel maps it to the status it carries. Downstream, the handler catches it like any
/// other fault and answers a flat 500 — the same body, refused for the same reason, reported as a server
/// error. So the legible refusal a host-wide ceiling depends on is a property of the ORDERING, not of the
/// limit; today's pipeline reads the body a hop before the handler is installed, and moving that read
/// after it would silently turn every rejected body into a 500.</para>
///
/// <para>Deliberately a bare pipeline rather than an API host: the claim under test is about two
/// framework hops, and a host would add reasons for a 500 that have nothing to do with the question.</para>
/// </summary>
public sealed class RequestBodyLimitPipelineOrderTests
{
    private const long BodyLimitBytes = 4 * 1024;

    [Fact]
    public async Task Read_upstream_of_the_exception_handler_answers_413()
    {
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, await StatusForOversizeBody(readBeforeHandler: true));
    }

    [Fact]
    public async Task Read_downstream_of_the_exception_handler_answers_500()
    {
        Assert.Equal(HttpStatusCode.InternalServerError, await StatusForOversizeBody(readBeforeHandler: false));
    }

    private static async Task<HttpStatusCode> StatusForOversizeBody(bool readBeforeHandler)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = BodyLimitBytes);

        var app = builder.Build();

        if (readBeforeHandler)
        {
            app.Use(ReadWholeBody);
            UseExceptionHandler(app);
        }
        else
        {
            UseExceptionHandler(app);
            app.Use(ReadWholeBody);
        }

        app.Run(context => context.Response.WriteAsync("reached the endpoint"));

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(BoundAddress(app)) };
            var payload = new StringContent(new string('x', (int)BodyLimitBytes * 4), Encoding.UTF8, "application/json");

            return (await client.PostAsync("/", payload)).StatusCode;
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static async Task ReadWholeBody(HttpContext context, RequestDelegate next)
    {
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        await reader.ReadToEndAsync();
        await next(context);
    }

    private static void UseExceptionHandler(IApplicationBuilder app) =>
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("An unexpected error occurred.");
        }));

    private static string BoundAddress(IHost host) =>
        host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
}
