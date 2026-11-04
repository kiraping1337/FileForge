namespace FileForge.IntegrationTests.Infrastructure;

/// <summary>
/// All test classes marked [Collection(ApiCollection.Name)] share one ApiFactory (one PostgreSQL
/// container for the whole run) and run one after another, never in parallel: they share the database.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "API";
}
