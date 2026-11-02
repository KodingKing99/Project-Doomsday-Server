namespace ProjectDoomsdayServer.E2ETests.Infrastructure;

[CollectionDefinition(CollectionName)]
public sealed class E2ETestCollectionDefinition : ICollectionFixture<E2EInfrastructureFixture>
{
    public const string CollectionName = "E2E Infrastructure";
}
