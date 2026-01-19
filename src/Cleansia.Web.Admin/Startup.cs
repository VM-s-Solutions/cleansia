using Cleansia.Web.Admin.Configuration;
using Cleansia.Web.Admin.Extensions;
using Cleansia.Web.Admin.Middleware;

namespace Cleansia.Web.Admin;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    public IConfiguration Configuration { get; } = configuration;
    public IWebHostEnvironment Environment { get; } = environment;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddServices(Configuration, Environment);

        // Add Hangfire for background jobs
        services.AddHangfireServices(Configuration);

        services.AddCors(options =>
        {
            options.AddPolicy("CleansiaAdmin", policy =>
            {
                // Admin app will run on port 4202
                policy.WithOrigins("http://localhost:4200")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("Content-Disposition");
            });
        });

        services.AddSwaggerGen();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        app.MigrateDatabase(env);

        app.Use((context, next) =>
        {
            context.Request.EnableBuffering();
            return next();
        });

        if (!env.IsProduction())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("swagger/v1/swagger.json", "Cleansia.Admin.API v1");
                c.RoutePrefix = string.Empty;
            });
        }

        // Add request logging middleware
        app.UseMiddleware<RequestLoggingMiddleware>();

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An unexpected error occurred.");
            });
        });
        app.UseRouting();
        app.UseCors("CleansiaAdmin");
        app.UseAuthentication();
        app.UseAuthorization();

        // Add Hangfire Dashboard
        app.UseHangfireConfiguration();

        // Configure recurring jobs
        HangfireConfiguration.ConfigureRecurringJobs();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
