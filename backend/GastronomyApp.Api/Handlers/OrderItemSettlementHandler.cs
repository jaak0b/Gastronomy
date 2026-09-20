using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.Auth.Callers;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Requests;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderItemSettlementHandler
{
  private readonly SavedChangeAnnouncer _announcement;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly ILogger<OrderItemSettlementHandler> _logger;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly OrderItemSettlementService _settlementService;

  public OrderItemSettlementHandler(OrderItemSettlementService settlementService, SavedChangeAnnouncer announcement, HubNotificationDispatcher dispatcher, ResultEnvelope resultEnvelope, ILogger<OrderItemSettlementHandler> logger, IMapper mapper)
  {
    _settlementService = settlementService;
    _announcement = announcement;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _logger = logger;
    _mapper = mapper;
  }

  public async Task<IResult> SettleAsync(SettleItemsRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<SettlementResult, SettlementFailure> settlement = await _settlementService.SettleAsync(BuildSettlementRequest(request, caller), cancellationToken);

    if (!settlement.IsSuccess)
    {
      WarnAboutASettlementTheScreenCannotProduce(settlement.Failure, caller.StaffMemberId);

      return _resultEnvelope.ToResult(_resultEnvelope.BuildProblemDescription(settlement.Failure));
    }

    List<Guid> settledIds = settlement.Value.NewlySettled.Select(item => item.Id).ToList();
    List<Guid> reappliedIds = settlement.Value.Reapplied.Select(item => item.Id).ToList();
    List<Guid> alreadySettledByOthersIds = settlement.Value.AlreadySettledByOthers.Select(item => item.Id).ToList();

    if (settledIds.Count > 0)
      _logger.LogInformation("{SettledItemCount} order items were settled and saved. Order item ids: {SettledOrderItemIds}.", settledIds.Count, settledIds);

    var otherPhonesWereTold = settledIds.Count == 0 || await TellTheOtherPhonesAsync(settledIds, settlement.Value.SettledTableNames);

    return Results.Ok(new SettlementView(settledIds, reappliedIds, alreadySettledByOthersIds, otherPhonesWereTold));
  }

  private SettlementRequest BuildSettlementRequest(SettleItemsRequest request, StaffDeviceCaller caller)
  {
    return new()
           {
             Lines = _mapper.Map<IReadOnlyList<SettlementLine>>(request.Lines ?? []),
             SettledByStaffMemberId = caller.StaffMemberId
           };
  }

  private Task<bool> TellTheOtherPhonesAsync(IReadOnlyList<Guid> settledIds, IReadOnlyList<string> tableNames)
  {
    return _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(cancellationToken => _dispatcher.PushOrderItemsSettledAsync(new(settledIds, tableNames), cancellationToken));
  }

  private void WarnAboutASettlementTheScreenCannotProduce(SettlementFailure failure, Guid staffMemberId)
  {
    if (failure.Reason is SettlementFailureReason.NoRunningFestival)
    {
      _logger.LogWarning("A settlement from staff member {StaffMemberId} was refused because no festival is running, so nothing was settled.", staffMemberId);
      return;
    }

    if (failure.Reason is SettlementFailureReason.AmountPaidMissing or SettlementFailureReason.AmountPaidNegative or SettlementFailureReason.DuplicateOrderItemId or SettlementFailureReason.NoItemsSelected or SettlementFailureReason.UnknownOrderItemId or SettlementFailureReason.PaymentNoticeMissing)
    {
      _logger.LogWarning("A settlement from staff member {StaffMemberId} was refused because {Reason}. The order item it names is {OrderItemId}, and the open items screen cannot produce that, so nothing was settled.", staffMemberId, failure.Reason, failure.OffendingOrderItemId);
      return;
    }

    if (failure.Reason is SettlementFailureReason.SelectionSpansSeveralTables)
    {
      _logger.LogWarning("A settlement from staff member {StaffMemberId} was refused because {Reason}. The items the phone sent belong to the tables {TableNames}, and the open items screen holds every other table back once one of them has something ticked, so nothing was settled.",
                         staffMemberId,
                         failure.Reason,
                         failure.TableNamesInTheSelection);
    }
  }
}
