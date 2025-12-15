using Cleansia.Web.Configuration;
using Cleansia.Web.Extensions;

namespace Cleansia.Web;

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
            options.AddPolicy("Cleansia", policy =>
            {
                // TODO
                //policy.WithOrigins("http://localhost:4200", "http://localhost:4201")
                //      .AllowAnyMethod()
                //      .AllowAnyHeader()
                //      .WithExposedHeaders("Content-Disposition");

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
                c.SwaggerEndpoint("swagger/v1/swagger.json", "Cleansia.API v1");
                c.RoutePrefix = string.Empty;
            });
        }

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An unexpected error occurred.");
            });
        });
        app.UseRouting();
        app.UseCors("Cleansia");
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