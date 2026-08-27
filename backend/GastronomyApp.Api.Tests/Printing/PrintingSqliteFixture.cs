using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Printing;

public sealed class PrintingSqliteFixture : IDisposable
{
    private readonly SqliteConnection connection;

    public PrintingSqliteFixture()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using GastronomyAppDbContext creator = CreateContext();
        creator.Database.Migrate();
    }

    public GastronomyAppDbContext CreateContext()
    {
        DbContextOptions<GastronomyAppDbContext> options = new DbContextOptionsBuilder<GastronomyAppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new GastronomyAppDbContext(options);
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}

public sealed record SeededTicket(Guid OrderId, Guid TicketId, int SequenceNumber);

public sealed class PrintingSeeder
{
    private readonly DateTime baseline = new(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc);

    public Guid EventSessionId { get; } = Guid.NewGuid();

    public Guid StaffMemberId { get; } = Guid.NewGuid();

    public Guid DeviceId { get; } = Guid.NewGuid();

    public async Task SeedSessionAsync(GastronomyAppDbContext context, bool isPractice, CancellationToken cancellationToken)
    {

        context.StaffMembers.Add(new StaffMember
        {
            Id = StaffMemberId,
            Name = "Anna",
            IsActive = true,
            CreatedAtUtc = baseline,
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedStationAsync(
        GastronomyAppDbContext context,
        Guid stationId,
        string name,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        context.Stations.Add(new Station
        {
            Id = stationId,
            Name = name,
            SortOrder = 1,
            IsActive = true,
        });

        context.PrinterConfigurations.Add(new PrinterConfiguration
        {
            StationId = stationId,
            TransportKind = TransportKind.Mock,
            Host = host,
            Port = port,
            AgentIdentifier = null,
            CharactersPerLine = 48,
            CodePageName = "PC858",
            ConnectTimeoutSeconds = 3,
            JobTimeoutSeconds = 90,
            HeartbeatSeconds = 10,
            IsEnabled = true,
        });

        context.PrinterStatuses.Add(new PrinterStatus
        {
            StationId = stationId,
            IsOnline = true,
            IsPaperEnd = false,
            IsPaperNearEnd = false,
            IsCoverOpen = false,
            IsInErrorState = false,
            IsFaulty = false,
            LastDetail = "seeded",
            LastChangedAtUtc = baseline,
            LastHeardFromAtUtc = baseline,
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SeededTicket> SeedOrderAsync(
        GastronomyAppDbContext context,
        Guid stationId,
        int globalOrderNumber,
        int sequenceNumber,
        int minutesAfterBaseline,
        LocationTicketStatus status,
        CancellationToken cancellationToken)
    {
        Guid orderId = Guid.NewGuid();
        Guid ticketId = Guid.NewGuid();
        DateTime createdAtUtc = baseline.AddMinutes(minutesAfterBaseline);

        context.Orders.Add(new Order
        {
            Id = orderId,
            ClientOrderId = Guid.NewGuid(),
            GlobalOrderNumber = globalOrderNumber,
            StaffMemberId = StaffMemberId,
            DeviceId = DeviceId,
            TableLabel = "12",
            Note = null,
            TotalCents = 950,
            Status = OrderStatus.Accepted,
            CreatedAtUtc = createdAtUtc,
        });

        context.LocationTickets.Add(new LocationTicket
        {
            Id = ticketId,
            OrderId = orderId,
            StationId = stationId,
            StationSequenceNumber = sequenceNumber,
            Status = status,
            ReprintCount = 0,
            CreatedAtUtc = createdAtUtc,
        });

        context.OrderLines.Add(new OrderLine
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            LocationTicketId = ticketId,
            CatalogItemId = Guid.NewGuid(),
            ChosenStationId = null,
            ItemNameSnapshot = "Bratwurst mit Brot",
            UnitPriceCentsSnapshot = 350,
            Quantity = 2,
            Note = null,
        });

        await context.SaveChangesAsync(cancellationToken);
        return new SeededTicket(orderId, ticketId, sequenceNumber);
    }
}
