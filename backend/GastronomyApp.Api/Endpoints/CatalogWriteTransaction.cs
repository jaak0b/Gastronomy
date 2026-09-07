using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed record CatalogWrite(IResult Response, bool SomethingChanged);

public sealed class CatalogWriteTransaction
{
  private readonly SavedChangeAnnouncement _announcement;
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public CatalogWriteTransaction(CatalogChangeAnnouncer announcer, SavedChangeAnnouncement announcement)
  {
    _announcer = announcer;
    _announcement = announcement;
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
      await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);
    }

    return outcome.Response;
  }
}
