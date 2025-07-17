namespace Cleansia.IntegrationTests;

[CollectionDefinition("PostgresCollection")]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;