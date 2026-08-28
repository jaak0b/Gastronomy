using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class OrderAcceptanceTransaction
{
  private readonly OrderAcceptanceService _acceptanceService;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly IOrderRepository _orderRepository;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public OrderAcceptanceTransaction(GastronomyAppDbContext dbContext,
                                    IOrderRepository orderRepository,
                                    OrderAcceptanceService acceptanceService)
  {
    _dbContext = dbContext;
    _orderRepository = orderRepository;
    _acceptanceService = acceptanceService;
  }

  public Task<Result<OrderAcceptanceResult, OrderValidationFailure>> AcceptAsync(OrderAcceptanceRequest request,
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
