using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public sealed record CatalogWrite(IResult Response, bool SomethingChanged);

public sealed class CatalogWriteTransaction
{
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly IHostApplicationLifetime _applicationLifetime;
  private readonly ILogger<CatalogWriteTransaction> _logger;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public CatalogWriteTransaction(CatalogChangeAnnouncer announcer,
                                IHostApplicationLifetime applicationLifetime,
                                ILogger<CatalogWriteTransaction> logger)
  {
    _announcer = announcer;
    _applicationLifetime = applicationLifetime;
    _logger = logger;
  }

  public async Task<IResult> RunAsync(GastronomyAppDbContext dbContext,
                                      Func<CancellationToken, Task<CatalogWrite>> write,
                                      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);
    ArgumentNullException.ThrowIfNull(write);

    CatalogWrite outcome =
      await _transactionRunner.RunAsync(dbContext,
                                        async transactionCancellationToken =>
                                        {
                                          CatalogWrite written = await write(transactionCancellationToken);

                                          return new TransactionOutcome<CatalogWrite>
                                                 {
                                                   Value = written,
                                                   ShouldCommit = written.SomethingChanged
                                                 };
                                        },
                                        cancellationToken);

    if (outcome.SomethingChanged)
    {
      await TellTheDevicesWithoutFailingTheSavedChangeAsync();
    }

    return outcome.Response;
  }

  private async Task TellTheDevicesWithoutFailingTheSavedChangeAsync()
  {
    CancellationToken tokenOutlivingTheAdminsRequest = _applicationLifetime.ApplicationStopping;

    try
    {
      await _announcer.AnnounceAsync(tokenOutlivingTheAdminsRequest);
    }
    catch (OperationCanceledException) when (tokenOutlivingTheAdminsRequest.IsCancellationRequested)
    {
      _logger.LogInformation("The change to the menu was saved, but the program was quitting, so the phones and station tablets were not told about it and will load the new menu the next time they connect.");
    }
    catch (Exception exception)
    {
      _logger.LogError(exception,
                       "The change to the menu was saved, but the phones and station tablets could not be told about it, so they keep showing the previous menu until they load it again.");
    }
  }
}
