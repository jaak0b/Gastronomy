# How screens learn that something changed

## Principle

A write that nobody is told about is a bug, so no service tells anyone about a configuration or
order change: the commit does. A writer that has to remember to announce will forget.

## Events

Five SignalR hub events exist, and no others are added without a page here saying why.

| Event | Payload | Groups | Raised when |
|---|---|---|---|
| `ConfigurationChanged` | none | `Devices`, `Admin`, `Stations` | any entity an admin edits was added, changed or deleted: catalog category, catalog item, festival, festival catalog item, item station assignment, station, festival station, staff member, unit, ingredient, recipe line, festival stock row, area, table, festival station shape |
| `OrdersChanged` | none | `Devices`, `Admin`, `Stations` | an order, a station order, an order item or an order item status was added, changed or deleted, which covers placing, handing out, putting back, hiding and settling |
| `EnrolmentCompleted` | staff member id or station id, owner name, device id | `Admin` | a device redeemed an invitation |
| `DeviceRevoked` | device id | `device:{id}`, `Admin` | a device was signed out |
| `GroupsJoined` | none | the connecting connection only | the laptop has put a new connection into all its groups |

Two events are broad on purpose. A reload of a catalog or a queue is a few kilobytes on a festival
WiFi that carries nothing else, while a fine-grained event is exactly the thing a writer forgets.

## Laptop

The decision sits on the entity. `HubEvent` and `RaisesAttribute` live in `Core/Announcements`, and
every entity class carries `[Raises(...)]` with the events a write to it raises: the configuration
entities `ConfigurationChanged`, `Order`, `StationOrder`, `OrderItem` and `OrderItemStatus` `OrdersChanged`, `Festival`
and `FestivalStation` both because an order moves their counters, and `Device` and
`EnrolmentInvitation` an empty list. A save interceptor (`ChangeAnnouncementInterceptor` in
`Infrastructure/Persistence`) on the one database context unions those lists over every added,
modified or deleted entry before each save, in `HubEventsRaisedBy`. An entity type without the
attribute makes the save throw, so a new entity forces the decision instead of allowing a silent gap.
No station ids are recorded: every station tablet is in the `Stations` group and reloads its own
queue.

There is no collector. The interceptor hands each event to the request's `IAfterCommitActions`
through `SendOnceWhenCommittedAsync`, which treats hub events as a set: a request may save several
times, and the queue still sends each event once per commit, after the transaction committed, and
drops everything when it rolls back. A save that raises an event while no request queue is collecting
throws, because there is no commit to send it after. Placing an order therefore sends one
`OrdersChanged` and one `ConfigurationChanged`.

Synchronous saves throw on that context, so no write can slip past the asynchronous hook.
`HubNotificationDispatcher` in the Api implements `ICommittedChangeAnnouncer` and maps each
`HubEvent` to its name in `Names.HubEvents` and its groups. Every send is wrapped in
`IAnnouncementGuard`, which lives once in `AnnouncementGuard`: a hub that cannot be reached is logged
and never fails the change that was already saved.

The two device commands, `EnrolmentCompleted` and `DeviceRevoked`, are sent by `EnrolmentService` and
`DeviceOwnerRetirement` through `IEnrolmentCompletionAnnouncer` and `IDeviceRevocationAnnouncer`,
handed to the same after-commit queue, because they carry a payload and address one device; they are
not derived from the tracker.

SignalR finishes the client's start before `GastronomyHub.OnConnectedAsync` has put the connection
into its groups, and for a station tablet the hub first reads the device from the database. A
reload on start can therefore miss a change committed in between, and nobody tells that device
about it. The hub sends `GroupsJoined` to the caller as the last step of `OnConnectedAsync`, after
every group join and the connection registry entry, and the phone reloads then. The send has no
catch and does not go through `IAnnouncementGuard`, because it announces no saved change: if it
fails, SignalR logs the error and closes the connection, and the phone falls back to polling.

The mechanism only sees writes that go through the tracker. Every write therefore loads its
entities and saves them. `ExecuteDeleteAsync`, `ExecuteUpdateAsync`, `FromSqlRaw`,
`ExecuteSqlRawAsync` and any other statement that reaches the database without tracked entities are
banned. If one is ever unavoidable, the same method enqueues the event it caused on the
same after-commit queue, in the same request, with a test proving the event arrives, and a reviewer treats the
bare statement without that record as a defect.

## Phones and tablets

A store subscribes to the event whose name says what it holds and reloads from the laptop. A store
never patches its own copy after an action instead of reloading, and a screen never hides a stale
value by filtering it against another list: a value the laptop no longer has is shown as the problem
it is, or refetched, never quietly dropped and later sent back. Every store also registers with the
connection store's refetch, which runs when `GroupsJoined` arrives, on the first connection and after
every reconnect, so a reconnect reloads everything. Where a reload and an action share the
state a screen shows, the reload goes through the latest request gate, so a slow answer never
overwrites a newer one.

## Tests

The mechanism is per entity type, so the hub tests prove it per type through one real route each:
`ConfigurationChangedAnnouncementTest` for a category, an article, a festival, a station, a waiter,
a station removed from a festival with its assignments, an area, a table, a station shape on the
plan, a new, renamed, activated and deactivated unit, and a phone replaced over several saves;
`OrdersChangedAnnouncementTest` for placing (two saves, one event), handing out (with the status rows it writes), putting back, hiding and settling. Each
asserts the phone, the admin connection and the station tablet hear the event exactly once. A new
entity type gets a hub test through one of its routes in the same change. The tests listen through
`HubEventListener`, built on the test factory. The interceptor's unit tests cover the union of
events, once per request over several saves, nothing after a rollback, nothing outside a request,
and the throw for a type without the attribute.

`GastronomyHubTest` proves for a phone, a station tablet and the laptop that `GroupsJoined` arrives
once and that a broadcast sent right after it reaches the connection. `HubEventListener.StartAsync`
returns only once its connection heard `GroupsJoined`, so a hub test never sends an event before the
listener is in its groups.
