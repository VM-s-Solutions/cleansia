using Testcontainers.PostgreSql;

namespace Cleansia.IntegrationTests;

public class PostgresContainerFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;

    public PostgresContainerFixture()
    {
        // No fixed host-port binding: Testcontainers assigns a random free host port and
        // GetConnectionString() reports the mapped port the consumers actually use. Pinning
        // host 5432 caused "address already in use" on CI (and locally) whenever 5432 was
        // taken — by another test assembly's container, a local Postgres, or the runner's own.
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();
        InitializeAsync().GetAwaiter().GetResult();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public string GetConnectionString()
    {
        return _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}