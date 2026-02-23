using Cleansia.Web.Mobile.Configuration;
using Cleansia.Web.Mobile.Extensions;
using Cleansia.Web.Mobile.Middleware;

namespace Cleansia.Web.Mobile;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    public IConfiguration Configuration { get; } = configuration;
    public IWebHostEnvironment Environment { get; } = environment;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddServices(Configuration, Environment);
        services.AddHangfireServices(Configuration);

        services.AddCors(options =>
        {
            options.AddPolicy("CleansiaMobile", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
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
                c.SwaggerEndpoint("swagger/v1/swagger.json", "Cleansia.Mobile.API v1");
                c.RoutePrefix = string.Empty;
            });
        }

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
        app.UseCors("CleansiaMobile");
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseHangfireConfiguration();
        HangfireConfiguration.ConfigureRecurringJobs();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
