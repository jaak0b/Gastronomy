using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class OrderQueryHandler
{
    private const int DefaultLimit = 50;

    private readonly OrderLineCollapser lineCollapser = new();

    private readonly GastronomyAppDbContext dbContext;
    private readonly OrderReader orderReader;
    private readonly StationPrinterStatusLookup statusLookup;

    public OrderQueryHandler(
        GastronomyAppDbContext dbContext,
        OrderReader orderReader,
        StationPrinterStatusLookup statusLookup)
    {
        this.dbContext = dbContext;
        this.orderReader = orderReader;
        this.statusLookup = statusLookup;
    }

    public async Task<IResult> ListForStaffMemberAsync(Guid staffMemberId, CancellationToken cancellationToken)
    {
        List<Order> orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.StaffMemberId == staffMemberId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(DefaultLimit)
            .ToListAsync(cancellationToken);

        List<OrderListEntryView> entries = [];

        foreach (Order order in orders)
        {
            LoadedOrder loaded = (await orderReader.LoadAsync(dbContext, order.Id, cancellationToken))!;
            entries.Add(await DescribeListEntryAsync(loaded, cancellationToken));
        }

        return Results.Ok(new OrderListView(entries));
    }

    public async Task<IResult> DetailAsync(Guid orderId, Guid staffMemberId, CancellationToken cancellationToken)
    {
        LoadedOrder? loaded = await orderReader.LoadAsync(dbContext, orderId, cancellationToken);

        if (loaded is null || loaded.Order.StaffMemberId != staffMemberId)
        {
            return Results.NotFound();
        }

        return Results.Ok(await DescribeDetailAsync(loaded, cancellationToken));
    }

    public async Task<OrderDetailView> DescribeDetailAsync(LoadedOrder loaded, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Guid> stationOrderStations = loaded.StationOrders
            .ToDictionary(stationOrder => stationOrder.Id, stationOrder => stationOrder.StationId);

        await Task.CompletedTask;

        List<OrderDetailItemView> items =
        [
            .. loaded.StationOrders.SelectMany(stationOrder => lineCollapser
                .Collapse(
                    [.. loaded.Items.Where(item => item.StationOrderId == stationOrder.Id)],
                    item => item.ItemName,
                    item => item.Note)
                .Select(collapsed => new OrderDetailItemView(
                    collapsed.Line.Id,
                    collapsed.Line.CatalogItemId,
                    collapsed.Line.ItemName,
                    collapsed.Quantity,
                    collapsed.Line.UnitPriceCents,
                    collapsed.Line.Note,
                    StationNameFor(loaded, stationOrderStations, collapsed.Line.StationOrderId)))),
        ];

        return new OrderDetailView(
            loaded.Order.Id,
            loaded.Order.GlobalOrderNumber,
            loaded.Order.TableName,
            loaded.Order.Note,
            orderReader.TotalCentsOf(loaded),
            orderReader.StatusOf(loaded).ToString(),
            loaded.Order.CreatedAtUtc,
            items,
            orderReader.DescribeStationOrders(loaded));
    }

    public async Task<OrderListEntryView> DescribeListEntryAsync(
        LoadedOrder loaded,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> stationIds = [.. loaded.StationOrders.Select(stationOrder => stationOrder.StationId)];

        Dictionary<Guid, PrinterStatus> printerStatuses =
            await statusLookup.ByStationAsync(dbContext, stationIds, cancellationToken);

        List<OrderListStationOrderView> stationOrders =
        [
            .. loaded.StationOrders.Select(stationOrder => new OrderListStationOrderView(
                stationOrder.Id,
                loaded.StationNames.TryGetValue(stationOrder.StationId, out string? name) ? name : string.Empty,
                stationOrder.StationOrderNumber,
                orderReader.StatusOfStationOrder(loaded, stationOrder.Id).ToString(),
                loaded.LatestPrintJobByStationOrderId.TryGetValue(stationOrder.Id, out PrintJob? job)
                    ? job.FailureReason?.ToString()
                    : null,
                !printerStatuses.TryGetValue(stationOrder.StationId, out PrinterStatus? status)
                    || !status.IsPaperEnd)),
        ];

        return new OrderListEntryView(
            loaded.Order.Id,
            loaded.Order.GlobalOrderNumber,
            loaded.Order.TableName,
            orderReader.TotalCentsOf(loaded),
            orderReader.StatusOf(loaded).ToString(),
            loaded.Order.CreatedAtUtc,
            stationOrders);
    }

    private string StationNameFor(
        LoadedOrder loaded,
        IReadOnlyDictionary<Guid, Guid> stationOrderStations,
        Guid stationOrderId)
    {
        if (!stationOrderStations.TryGetValue(stationOrderId, out Guid stationId))
        {
            return string.Empty;
        }

        return loaded.StationNames.TryGetValue(stationId, out string? name) ? name : string.Empty;
    }
}
