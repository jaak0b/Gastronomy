using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure;

public sealed class GastronomyAppDbContext : DbContext
{
    public GastronomyAppDbContext(DbContextOptions<GastronomyAppDbContext> options)
        : base(options)
    {
    }


    public DbSet<Station> Stations => Set<Station>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<ItemStationAssignment> ItemStationAssignments => Set<ItemStationAssignment>();

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<EnrolmentInvitation> EnrolmentInvitations => Set<EnrolmentInvitation>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<StationOrder> StationOrders => Set<StationOrder>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    public DbSet<Printer> Printers => Set<Printer>();

    public DbSet<PrinterStatus> PrinterStatuses => Set<PrinterStatus>();

    public DbSet<SequenceCounters> SequenceCounters => Set<SequenceCounters>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GastronomyAppDbContext).Assembly);
    }
}
