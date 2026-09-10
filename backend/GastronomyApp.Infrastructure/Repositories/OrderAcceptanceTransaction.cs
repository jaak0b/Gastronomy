using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class OrderAcceptanceTransaction
{
  private const int AttemptsBeforeGivingUp = 5;

  private readonly OrderAcceptanceService _acceptanceService;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly ILogger<OrderAcceptanceTransaction> _logger;
  private readonly IOrderRepository _orderRepository;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public OrderAcceptanceTransaction(GastronomyAppDbContext dbContext,
                                    IOrderRepository orderRepository,
                                    OrderAcceptanceService acceptanceService,
                                    ILogger<OrderAcceptanceTransaction> logger)
  {
    _dbContext = dbContext;
    _orderRepository = orderRepository;
    _acceptanceService = acceptanceService;
    _logger = logger;
  }

  public async Task<Result<OrderAcceptanceResult, OrderValidationFailure>> AcceptAsync(OrderAcceptanceRequest request,
                                                                                       CancellationToken cancellationToken)
  {
    for (var attempt = 1; attempt <= AttemptsBeforeGivingUp; attempt++)
    {
      try
      {
        return await AttemptAsync(request, cancellationToken);
      }
      catch (DbUpdateConcurrencyException) when (attempt < AttemptsBeforeGivingUp)
      {
        _logger.LogWarning("Attempt {Attempt} of {AttemptsBeforeGivingUp} to allocate the numbers for the order {ClientOrderId} lost the counter to another submission, so it is being tried again.",
                           attempt,
                           AttemptsBeforeGivingUp,
                           request.ClientOrderId);

        _dbContext.ChangeTracker.Clear();
      }
      catch (DbUpdateConcurrencyException)
      {
        _dbContext.ChangeTracker.Clear();

        return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new()
                                                                            {
                                                                              Reason = OrderValidationFailureReason.OrderNumberCouldNotBeAllocated
                                                                            });
      }
    }

    return new Never().OfType<Result<OrderAcceptanceResult, OrderValidationFailure>>(AttemptsBeforeGivingUp);
  }

  private Task<Result<OrderAcceptanceResult, OrderValidationFailure>> AttemptAsync(OrderAcceptanceRequest request,
                                                                                   CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(_dbContext,
                                       async transactionCancellationToken =>
                                       {
                                         var existingOrder = await _orderRepository.FindByClientOrderIdAsync(request.ClientOrderId,
                                                                                                             transactionCancellationToken);

                                         if (existingOrder is not null)
                                         {
                                           return new()
                                                  {
                                                    Value = Result<OrderAcceptanceResult, OrderValidationFailure>.Success(new()
                                                                                                                          {
                                                                                                                            Order = existingOrder,
                                                                                                                            WasAlreadyAccepted = true
                                                                                                                          }),
                                                    ShouldCommit = true
                                                  };
                                         }

                                         Result<OrderAcceptanceResult, OrderValidationFailure> acceptance =
                                           await _acceptanceService.AcceptAsync(request, transactionCancellationToken);

                                         return new TransactionOutcome<Result<OrderAcceptanceResult, OrderValidationFailure>>
                                                {
                                                  Value = acceptance,
                                                  ShouldCommit = acceptance.IsSuccess
                                                };
                                       },
                                       cancellationToken);
  }
}
