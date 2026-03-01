using Testcontainers.PostgreSql;

namespace Cleansia.IntegrationTests;

public class PostgresContainerFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;

    public PostgresContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithPortBinding(5432, 5432)
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