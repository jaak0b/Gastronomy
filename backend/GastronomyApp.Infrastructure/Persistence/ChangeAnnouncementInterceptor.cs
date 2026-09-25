using System.Reflection;
using GastronomyApp.Core.Announcements;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Persistence;

public sealed class ChangeAnnouncementInterceptor : SaveChangesInterceptor
{
  private readonly IAfterCommitActions _afterCommitActions;

  public ChangeAnnouncementInterceptor(IAfterCommitActions afterCommitActions)
  {
    _afterCommitActions = afterCommitActions;
  }

  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    throw new InvalidOperationException("Changes are saved asynchronously only, because the hub events a save raises are enqueued asynchronously.");
  }

  public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(eventData);

    foreach (var hubEvent in HubEventsRaisedBy(eventData.Context!.ChangeTracker.Entries()))
      await _afterCommitActions.SendOnceWhenCommittedAsync(hubEvent, cancellationToken);

    return await base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private IReadOnlySet<HubEvent> HubEventsRaisedBy(IEnumerable<EntityEntry> entries)
  {
    return entries.Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                  .SelectMany(entry => entry.Metadata.ClrType.GetCustomAttribute<RaisesAttribute>(false)?.Events ?? throw new InvalidOperationException($"The entity type {entry.Metadata.ClrType.FullName} was saved without a Raises attribute, so no screen would learn about the change."))
                  .ToHashSet();
  }
}
