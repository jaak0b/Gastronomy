using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class StaffMemberRepositoryTest
{
  [Test]
  public async Task FindByIdAsync_SomebodyWhoIsNotOnTheList_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StaffMemberRepository repository = new(fixture.DbContext);

    Assert.That(await repository.FindByIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.Null);
  }

  [Test]
  public async Task FindAllAsync_NamesInMixedCase_ListsThemWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    foreach (var name in new[]
                         {
                           "Zora",
                           "Änne",
                           "berta",
                           "Carl"
                         })
      fixture.DbContext.StaffMembers.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           Name = name,
                                           IsActive = true,
                                           CreatedAtUtc = DateTime.UtcNow
                                         });

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var readContext = fixture.CreateContext();

    var staffMembers = await new StaffMemberRepository(readContext).FindAllAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(staffMembers.Select(staffMember => staffMember.Name),
                Is.EqualTo(new[]
                           {
                             "Anna",
                             "berta",
                             "Carl",
                             "Zora",
                             "Änne"
                           }));
  }

  [Test]
  public async Task SaveChangesAsync_ANameThatWasJustChanged_StoresIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StaffMemberRepository repository = new(fixture.DbContext);

    var anna = (await repository.FindByIdAsync(seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken))!;
    anna.Name = "Anne Marie";
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That((await readContext.StaffMembers.FirstAsync(staffMember => staffMember.Id == seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken)).Name, Is.EqualTo("Anne Marie"));
  }
}
