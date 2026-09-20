using GastronomyApp.Core.Ports;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class CatalogWriteTransaction
{
  private readonly SavedChangeAnnouncement _announcement;
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly ITransactionRunner _transactionRunner;

  public CatalogWriteTransaction(CatalogChangeAnnouncer announcer,
                                 SavedChangeAnnouncement announcement,
                                 ITransactionRunner transactionRunner)
  {
    _announcer = announcer;
    _announcement = announcement;
    _transactionRunner = transactionRunner;
  }

  public async Task<IResult> RunAsync(Func<CancellationToken, Task<CatalogWrite>> write,
                                      CancellationToken cancellationToken)
  {
    return await RunTransactionAsync(write, announceTheCatalogChange: true, cancellationToken);
  }

  public async Task<IResult> RunWithoutCatalogAnnouncementAsync(Func<CancellationToken, Task<CatalogWrite>> write,
                                                                CancellationToken cancellationToken)
  {
    return await RunTransactionAsync(write, announceTheCatalogChange: false, cancellationToken);
  }

  private async Task<IResult> RunTransactionAsync(Func<CancellationToken, Task<CatalogWrite>> write,
                                                  bool announceTheCatalogChange,
                                                  CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(write);

    CatalogWrite outcome =
      await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                        {
                                          CatalogWrite written = await write(transactionCancellationToken);

                                          return new TransactionOutcome<CatalogWrite>
                                                 {
                                                   Value = written,
                                                   ShouldCommit = written.SomethingChanged
                                                 };
                                        },
                                        cancellationToken);

    if (outcome.SomethingChanged && announceTheCatalogChange)
    {
      await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);
    }

    return outcome.Response;
  }
}
