using ErrorOr;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderItemSettlementHandler
{
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly ILogger<OrderItemSettlementHandler> _logger;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly SavedChangeAnnouncer _savedChangeAnnouncer;
  private readonly OrderItemSettlementService _settlementService;

  public OrderItemSettlementHandler(OrderItemSettlementService settlementService, SavedChangeAnnouncer savedChangeAnnouncer, HubNotificationDispatcher dispatcher, ResultEnvelope resultEnvelope, ILogger<OrderItemSettlementHandler> logger)
  {
    _settlementService = settlementService;
    _savedChangeAnnouncer = savedChangeAnnouncer;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _logger = logger;
  }

  public async Task<IResult> SettleAsync(SettleItemsRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await _settlementService.SettleAsync(request.Lines ?? [], caller.StaffMemberId, cancellationToken)
                                   .MatchAsync(BuildSettlementViewAsync, refusedLines => Task.FromResult(_resultEnvelope.Refuse(refusedLines)));
  }

  private async Task<IResult> BuildSettlementViewAsync(SettlementResult settlement)
  {
    List<Guid> settledIds = settlement.NewlySettled.Select(item => item.Id).ToList();
    List<Guid> reappliedIds = settlement.Reapplied.Select(item => item.Id).ToList();
    List<Guid> alreadySettledByOthersIds = settlement.AlreadySettledByOthers.Select(item => item.Id).ToList();

    if (settledIds.Count > 0)
      _logger.LogInformation("{SettledItemCount} order items were settled and saved. Order item ids: {SettledOrderItemIds}.", settledIds.Count, settledIds);

    var otherPhonesWereTold = settledIds.Count == 0 || await TellTheOtherPhonesAsync(settledIds, settlement.SettledTableNames);

    return Results.Ok(new SettlementView(settledIds, reappliedIds, alreadySettledByOthersIds, otherPhonesWereTold));
  }

  private Task<bool> TellTheOtherPhonesAsync(IReadOnlyList<Guid> settledIds, IReadOnlyList<string> tableNames)
  {
    return _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(cancellationToken => _dispatcher.PushOrderItemsSettledAsync(new(settledIds, tableNames), cancellationToken));
  }

}
