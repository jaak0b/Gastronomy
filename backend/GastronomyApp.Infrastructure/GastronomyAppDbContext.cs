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

    public DbSet<TableSuggestion> TableSuggestions => Set<TableSuggestion>();

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<EnrolmentInvitation> EnrolmentInvitations => Set<EnrolmentInvitation>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<LocationTicket> LocationTickets => Set<LocationTicket>();

    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    public DbSet<PrintAttempt> PrintAttempts => Set<PrintAttempt>();

    public DbSet<PrinterConfiguration> PrinterConfigurations => Set<PrinterConfiguration>();

    public DbSet<PrinterStatus> PrinterStatuses => Set<PrinterStatus>();

    public DbSet<NumberCounter> NumberCounters => Set<NumberCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GastronomyAppDbContext).Assembly);
    }
}
