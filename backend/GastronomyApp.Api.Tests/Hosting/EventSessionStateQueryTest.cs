using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.Hosting;

[TestFixture]
public sealed class EventSessionStateQueryTest
{
    private ApiTestFactory factory = null!;

    [SetUp]
    public async Task SetUp()
    {
        factory = await new ApiTestFactory.Builder().StartAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await factory.DisposeAsync();
    }

    [Test]
    public async Task IsSessionActiveAsync_NoSessionYet_AnswersFalse()
    {
        Assert.That(await QueryAsync(), Is.False);
    }

    [Test]
    public async Task IsSessionActiveAsync_ActiveSessionSeeded_AnswersTrue()
    {
        await using (GastronomyAppDbContext context = factory.CreateContext())
        {
            await new ApiSeeder().SeedAsync(context, CancellationToken.None);
        }

        Assert.That(await QueryAsync(), Is.True);
    }

    [Test]
    public async Task IsSessionActiveAsync_SessionEnded_AnswersFalse()
    {
        await using (GastronomyAppDbContext context = factory.CreateContext())
        {
            await new ApiSeeder().SeedAsync(context, CancellationToken.None);
            EventSession session = await context.EventSessions.FirstAsync();
            session.IsActive = false;
            await context.SaveChangesAsync();
        }

        Assert.That(await QueryAsync(), Is.False);
    }

    private async Task<bool> QueryAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISessionStateQuery>()
            .IsSessionActiveAsync(CancellationToken.None);
    }
}
