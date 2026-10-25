using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class CatalogCategoryRepositoryTest
{
  [Test]
  public async Task FindByIdAsync_ACategoryThatIsNotThere_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogCategoryRepository repository = new(fixture.DbContext);

    Assert.That(await repository.FindByIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.Null);
  }
}
