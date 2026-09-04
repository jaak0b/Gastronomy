using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.Printing;

public sealed class PrintingSqliteFixture : IDisposable
{
  private readonly SqliteConnection _connection;

  public PrintingSqliteFixture()
  {
    _connection = new("Data Source=:memory:");
    _connection.Open();
    using var creator = CreateContext();
    creator.Database.Migrate();
  }

  public void Dispose()
  {
    _connection.Dispose();
  }

  public GastronomyAppDbContext CreateContext()
  {
    DbContextOptions<GastronomyAppDbContext> options = new DbContextOptionsBuilder<GastronomyAppDbContext>()
                                                      .UseSqlite(_connection)
                                                      .Options;

    return new(options);
  }
}

public sealed record SeededStationOrder(Guid OrderId, Guid StationOrderId, Guid PrintJobId, int StationOrderNumber);

public sealed class PrintingSeeder
{
  private readonly DateTime _baseline = new(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc);

  public Guid EventSessionId { get; } = Guid.NewGuid();

  public Guid StaffMemberId { get; } = Guid.NewGuid();

  public Guid DeviceId { get; } = Guid.NewGuid();

  public async Task SeedSessionAsync(GastronomyAppDbContext context, bool isPractice, CancellationToken cancellationToken)
  {
    context.StaffMembers.Add(new()
                             {
                               Id = StaffMemberId,
                               Name = "Anna",
                               IsActive = true,
                               CreatedAtUtc = _baseline
                             });

    await context.SaveChangesAsync(cancellationToken);
  }

  public async Task SeedStationAsync(GastronomyAppDbContext context,
                                     Guid stationId,
                                     string name,
                                     Guid printerId,
                                     string host,
                                     int port,
                                     CancellationToken cancellationToken)
  {
    Station station = new()
                      {
                        Id = stationId,
                        Name = name,
                        SortOrder = 1,
                        IsActive = true,
                        NextStationOrderNumber = 1
                      };
    context.Stations.Add(station);

    station.PrinterId = printerId;

    if (!await context.Printers.AnyAsync(printer => printer.Id == printerId, cancellationToken))
    {
      context.Printers.Add(new EpsonTmT20ivNetworkPrinter
                           {
                             Id = printerId,
                             Name = "Drucker " + name,
                             Host = host,
                             Port = port
                           });

      context.PrinterStatuses.Add(new()
                                  {
                                    PrinterId = printerId,
                                    IsOnline = true,
                                    IsPaperEnd = false,
                                    IsPaperNearEnd = false,
                                    IsCoverOpen = false,
                                    IsInErrorState = false,
                                    IsFaulty = false,
                                    LastDetail = "seeded",
                                    LastChangedAtUtc = _baseline,
                                    LastHeardFromAtUtc = _baseline
                                  });
    }

    await context.SaveChangesAsync(cancellationToken);
  }

  public async Task<SeededStationOrder> SeedOrderAsync(GastronomyAppDbContext context,
                                                       Guid stationId,
                                                       int globalOrderNumber,
                                                       int sequenceNumber,
                                                       int minutesAfterBaseline,
                                                       PrintJobStatus status,
                                                       CancellationToken cancellationToken)
  {
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();
    var printJobId = Guid.NewGuid();
    var createdAtUtc = _baseline.AddMinutes(minutesAfterBaseline);

    context.Orders.Add(new()
                       {
                         Id = orderId,
                         ClientOrderId = Guid.NewGuid(),
                         GlobalOrderNumber = globalOrderNumber,
                         StaffMemberId = StaffMemberId,
                         TableName = "12",
                         Note = null,
                         CreatedAtUtc = createdAtUtc
                       });

    context.StationOrders.Add(new()
                              {
                                Id = stationOrderId,
                                OrderId = orderId,
                                StationId = stationId,
                                StationOrderNumber = sequenceNumber
                              });

    context.PrintJobs.Add(new()
                          {
                            Id = printJobId,
                            StationOrderId = stationOrderId,
                            CopyNumber = 0,
                            Status = status,
                            CreatedAtUtc = createdAtUtc
                          });

    context.OrderItems.Add(new()
                           {
                             Id = Guid.NewGuid(),
                             StationOrderId = stationOrderId,
                             CatalogItemId = Guid.NewGuid(),
                             ItemName = "Bratwurst mit Brot",
                             UnitPriceCents = 350,
                             Note = null
                           });

    await context.SaveChangesAsync(cancellationToken);
    return new(orderId, stationOrderId, printJobId, sequenceNumber);
  }
}
