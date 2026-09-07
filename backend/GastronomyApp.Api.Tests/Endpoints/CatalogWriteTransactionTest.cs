using FakeItEasy;
using GastronomyApp.Api.Endpoints;
using GastronomyApp.Api.Hub;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Tests.Endpoints;

[TestFixture]
public sealed class CatalogWriteTransactionTest
{
  [SetUp]
  public async Task SetUp()
  {
    _context = await new OrderTestContext.Builder().StartAsync();
    _scope = _context.Factory.Services.CreateScope();
    _dbContext = _scope.ServiceProvider.GetRequiredService<GastronomyAppDbContext>();
    _proxy = A.Fake<IClientProxy>();
    _logger = A.Fake<ILogger<SavedChangeAnnouncement>>();
    _programIsQuitting = new();
  }

  [TearDown]
  public async Task TearDown()
  {
    _programIsQuitting.Dispose();
    _scope.Dispose();
    await _context.DisposeAsync();
  }

  private readonly IResult _savedChange = Results.Ok(new { saved = true });
  private OrderTestContext _context = null!;
  private GastronomyAppDbContext _dbContext = null!;
  private ILogger<SavedChangeAnnouncement> _logger = null!;
  private CancellationTokenSource _programIsQuitting = null!;
  private IClientProxy _proxy = null!;
  private IServiceScope _scope = null!;

  [Test]
  public async Task RunAsync_TheAdminsBrowserGoesAwayWhileTheDevicesAreBeingTold_TellsTheDevicesWithATokenOfItsOwn()
  {
    using CancellationTokenSource adminRequest = new();
    CancellationToken tokenUsedForThePush = default;
    A.CallTo(() => _proxy.SendCoreAsync(A<string>._, A<object?[]>._, A<CancellationToken>._))
     .Invokes(call =>
              {
                tokenUsedForThePush = call.GetArgument<CancellationToken>(2);
                adminRequest.Cancel();
              });

    await TransactionTalkingTo(_proxy).RunAsync(_dbContext, RaiseTheBratwurstPriceAsync, adminRequest.Token);

    A.CallTo(() => _proxy.SendCoreAsync(A<string>._, A<object?[]>._, A<CancellationToken>._)).MustHaveHappened();
    Assert.That(tokenUsedForThePush.IsCancellationRequested, Is.False);
  }

  [Test]
  public async Task RunAsync_TheDevicesCannotBeTold_StillAnswersTheAdminWithTheSavedChange()
  {
    A.CallTo(() => _proxy.SendCoreAsync(A<string>._, A<object?[]>._, A<CancellationToken>._))
     .Throws(new InvalidOperationException("The connection to the station tablets broke."));

    IResult response = await TransactionTalkingTo(_proxy)
      .RunAsync(_dbContext, RaiseTheBratwurstPriceAsync, TestContext.CurrentContext.CancellationToken);

    Assert.That(response, Is.SameAs(_savedChange));
  }

  [Test]
  public async Task RunAsync_TheDevicesCannotBeTold_WritesTheReasonToTheLogAsAnError()
  {
    A.CallTo(() => _proxy.SendCoreAsync(A<string>._, A<object?[]>._, A<CancellationToken>._))
     .Throws(new InvalidOperationException("The connection to the station tablets broke."));

    await TransactionTalkingTo(_proxy)
      .RunAsync(_dbContext, RaiseTheBratwurstPriceAsync, TestContext.CurrentContext.CancellationToken);

    A.CallTo(_logger)
     .Where(call => call.Method.Name == nameof(ILogger.Log)
                    && call.GetArgument<LogLevel>(0) == LogLevel.Error
                    && call.GetArgument<Exception?>(3) != null
                    && Rendered(call.GetArgument<object>(2)).Contains("station tablets", StringComparison.Ordinal))
     .MustHaveHappened();
  }

  [Test]
  public async Task RunAsync_TheProgramQuitsWhileTheDevicesAreBeingTold_LeavesNoErrorInTheLog()
  {
    await QuitTheProgramWhileTheDevicesAreBeingToldAsync();

    A.CallTo(_logger)
     .Where(call => call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == LogLevel.Error)
     .MustNotHaveHappened();
  }

  [Test]
  public async Task RunAsync_TheProgramQuitsWhileTheDevicesAreBeingTold_StillAnswersTheAdminWithTheSavedChange()
  {
    IResult response = await QuitTheProgramWhileTheDevicesAreBeingToldAsync();

    Assert.That(response, Is.SameAs(_savedChange));
  }

  [Test]
  public async Task RunAsync_TheProgramQuitsWhileTheDevicesAreBeingTold_WritesTheReasonToTheLogAsInformation()
  {
    await QuitTheProgramWhileTheDevicesAreBeingToldAsync();

    A.CallTo(_logger)
     .Where(call => call.Method.Name == nameof(ILogger.Log)
                    && call.GetArgument<LogLevel>(0) == LogLevel.Information
                    && Rendered(call.GetArgument<object>(2)).Contains("was quitting", StringComparison.Ordinal))
     .MustHaveHappenedOnceExactly();
  }

  private string Rendered(object? state)
  {
    return state?.ToString() ?? string.Empty;
  }

  private async Task<CatalogWrite> RaiseTheBratwurstPriceAsync(CancellationToken cancellationToken)
  {
    var bratwurst = await _dbContext.CatalogItems
                                    .FirstAsync(item => item.Id == _context.World.BratwurstItemId, cancellationToken);
    bratwurst.PriceCents += 50;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(_savedChange, true);
  }

  private async Task<IResult> QuitTheProgramWhileTheDevicesAreBeingToldAsync()
  {
    return await TransactionTalkingTo(_proxy)
      .RunAsync(_dbContext,
                RaisingTheBratwurstPriceAndThen(_programIsQuitting.CancelAsync),
                TestContext.CurrentContext.CancellationToken);
  }

  private Func<CancellationToken, Task<CatalogWrite>> RaisingTheBratwurstPriceAndThen(Func<Task> whatHappensNext)
  {
    return async cancellationToken =>
           {
             CatalogWrite written = await RaiseTheBratwurstPriceAsync(cancellationToken);
             await whatHappensNext();

             return written;
           };
  }

  private CatalogWriteTransaction TransactionTalkingTo(IClientProxy proxy)
  {
    var clients = A.Fake<IHubClients>();
    A.CallTo(() => clients.Group(A<string>._)).Returns(proxy);

    var hubContext = A.Fake<IHubContext<GastronomyHub>>();
    A.CallTo(() => hubContext.Clients).Returns(clients);

    CatalogChangeAnnouncer announcer =
      new(new(hubContext, _scope.ServiceProvider.GetRequiredService<IDbContextFactory<GastronomyAppDbContext>>()));

    var lifetime = A.Fake<IHostApplicationLifetime>();
    A.CallTo(() => lifetime.ApplicationStopping).Returns(_programIsQuitting.Token);

    return new(announcer, new(lifetime, _logger));
  }
}
