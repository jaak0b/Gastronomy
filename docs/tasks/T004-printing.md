# T004: Printing (renderer, transports, worker)

## 1. Objective

Build the printing subsystem for the vertical slice: the pure ESC/POS slip renderer, the two
`IPrinterTransport` implementations (`MockPrinterTransport` and `NetworkPrinterTransport`), and the
`PrinterWorker` / `PrinterFleet` hosted service that owns queues, connections, retries, and the
per-endpoint circuit breaker. Test first throughout: every class in this task has its failing test
written and run before the production code that makes it pass.

Definition of done: every unit test named in section 5 (`EscPosSlipRenderer`, `RetryPolicy`-shaped
mapping inside the worker, `StationCircuitBreaker`) is green, every table row in spec section 7.6
(and the mock's fault table in spec section 7.8) is exercised against `MockPrinterTransport`, and
the printing-only integration scenarios in section 5 run end to end with `PrinterWorker` +
`MockPrinterTransport` + in-memory SQLite, without any HTTP call. `dotnet build GastronomyApp.slnx`
succeeds with zero warnings and `dotnet test GastronomyApp.slnx` is green.

This task writes into `backend/GastronomyApp.Infrastructure` (renderer, both transports) and
`backend/GastronomyApp.Api` (the hosted service that starts one worker per printer endpoint and
wires worker callbacks toward SignalR). It does not add REST endpoints, a SignalR hub, or any
frontend code.

## 2. Assumed from earlier tasks

T002 (Core types, state machine, outcome rules) and T003 (repositories, counters) are not written
yet when this task is authored; six tasks are being authored in parallel and a later consistency
review reconciles the seams. Everything below that this task depends on from those two tasks is
named explicitly, with the exact shape this task needs, so the review can check it against what
T002 and T003 actually produced rather than guessing.

**Assumed to exist in `GastronomyApp.Core`, produced by T002:**

* The domain records from spec section 2.11 and 2.12, as C# types with the fields listed there:
  `PrintJob`, `PrintAttempt`, `PrinterConfiguration`, `PrinterStatus`, `LocationTicket`,
  `ProductionLocation`, `Order`, `OrderLine`, `NumberCounter`, `EventSession`. Field names and
  nullability follow the tables in spec sections 2.3, 2.4, 2.9 to 2.13 verbatim.
* Enums matching spec section 3.2 (`TicketStatus`: `Queued`, `Blocked`, `Printing`, `Printed`,
  `PrintedOnTestPrinter`, `Unknown`, `Failed`, `HandledOnPaper`), spec section 3.3 (`PrintJobStatus`:
  `Queued`, `PreflightCheck`, `Blocked`, `Sending`, `AwaitingEcho`, `Confirmed`, `Unknown`, `Failed`,
  `ResolvedPrinted`, `ResolvedMissing`), and `PrintJobKind` (`Initial`, `Reprint`, `Test`) from spec
  section 2.11.
* `TicketStateMachine` and `PrintJobStateMachine` classes in Core that expose a method shaped
  `bool CanTransition(TicketStatus from, TicketStatus to)` (and the `PrintJob` equivalent),
  implementing exactly the transitions drawn in the mermaid diagrams of spec sections 3.2 and 3.3,
  refusing every transition not drawn. This task's worker calls these before writing a new status
  rather than re-deriving the graph; if T002 names the method differently, the review updates the
  call sites in this task's production code, not the test intent.
* `OrderStatusCalculator` implementing the table in spec section 3.1, callable as
  `OrderStatus Calculate(IReadOnlyCollection<TicketStatus> ticketStatuses, bool isPractice)`. The
  worker calls this after every ticket write that might change the order's projected status, per
  spec section 7.4 step 10 ("Push. `TicketStatusChanged` and, if it changed, `OrderStatusChanged`").
* `PrintDispatchOutcome`, `PrinterTransportKind`, `IPrinterTransport`, `IPrinterSession`,
  `PrinterEndpoint`, `PrintPayload`, `PrinterStatusSnapshot`, `PrintDispatchResult` exactly as given
  in spec section 7.2. These are Core ports; this task implements `IPrinterTransport` twice
  (`NetworkPrinterTransport`, `MockPrinterTransport`) in Infrastructure and consumes the rest from
  Core. If T002 places these records in a different namespace than
  `GastronomyApp.Core.Printing`, only the `using` directives in this task's files change.
* `FailureReason` as a closed set of strings (or an enum backing them) matching spec section 2.11:
  `PaperEnd`, `CoverOpen`, `Unreachable`, `Timeout`, `SocketDropped`, `PrinterError`,
  `StationDisabled`, `StationFaulty`, `TicketResolvedByHuman`.
* A localization port the renderer can call without knowing resx exists. Assumed shape (Core
  defines the interface, Api/Infrastructure supplies the implementation, per hard rule 6 "extend
  the concept's existing home"):

  ```csharp
  namespace GastronomyApp.Core.Localization;

  public interface ISlipTextProvider
  {
      SlipStrings GetStrings(string languageCode);
  }

  public sealed record SlipStrings(
      string ReprintBanner,
      string ReprintTimePrefix,
      string SlipNumberPrefix,
      string OrderNumberPrefix,
      string TablePrefix,
      string ServerPrefix,
      string NotePrefix,
      string ItemsTotalPrefix,
      string AlsoGoesToPrefix,
      string ChosenStationWasPrefix,
      string TestSlipHeader,
      string StationCardInstructions);
  ```

  This keeps `GastronomyApp.Core` free of any framework or resx dependency (backend hard rule 1,
  "Localization is resx-only", and backend project table: Core has no framework dependencies).
  `GastronomyApp.Api`'s composition root registers the concrete implementation
  (`ResxSlipTextProvider`, in `GastronomyApp.Infrastructure` since it is an adapter, wired at
  startup in `GastronomyApp.Api`) reading `Strings.de.resx` / `Strings.en.resx`, satisfying backend
  hard rule 2 ("Localization is resx-only... This includes the text printed on receipt slips"). If
  T002/T003 already defined a broader `ILocalizer` port used elsewhere in the app, the review
  reconciles `ISlipTextProvider` into that port rather than keeping two.

**Assumed to exist in `GastronomyApp.Infrastructure`, produced by T003:**

* An EF Core `DbContext` (assumed name `GastronomyAppDbContext`) with `DbSet<PrintJob>`,
  `DbSet<PrintAttempt>`, `DbSet<LocationTicket>`, `DbSet<Order>`, `DbSet<OrderLine>`,
  `DbSet<ProductionLocation>`, `DbSet<PrinterConfiguration>`, `DbSet<PrinterStatus>`,
  `DbSet<NumberCounter>`, `DbSet<EventSession>`, configured for SQLite with
  `BEGIN IMMEDIATE`-style write serialization per spec section 4.1 step 1 and a 5 second busy
  timeout.
* A counter allocator satisfying spec section 2.13 and section 7.4 step 6, callable as
  `Task<int> AllocateNextAsync(CounterKind kind, Guid? eventSessionId, Guid? productionLocationId,
  string? printerEndpointKey, CancellationToken ct)`, cycling `PrinterProcessId` at 9999 as spec
  section 2.11 states (`1 to 9999`). This task calls it once per job at step 6 of spec section 7.4
  and nowhere else, and never allocates a `GlobalOrder` or `LocationSequence` number (those belong
  to order acceptance, T003's territory, per spec section 4.1).
* Repository or direct `DbContext` access sufficient for this task's worker to: load a ticket with
  its order and lines in one query (spec 7.4 step 1), claim a ticket transactionally (spec 7.4 step
  2), write `PrintJob` and `PrintAttempt` rows (append-only, per spec 2.11 invariant "Attempts are
  append only"), and write `PrinterStatus` rows (spec 2.12, "Status is written only by that
  printer's worker"). This task assumes direct `DbContext` injection into `PrinterWorker` is
  acceptable rather than a bespoke repository interface, because T003's repository shape is not
  fixed yet; if T003 introduces a narrower repository port, the review retargets the worker's data
  access to it without changing this task's test intent (each test still proves the same worker
  behaviour against the same in-memory SQLite fixture).
* `PrinterEndpointKey` construction exactly as spec section 2.13 defines it:
  `TransportKind|Host|Port|AgentIdentifier`, empty string for unused parts. This task assumes a
  single static-free helper for this (a non-static instance method on a small
  `PrinterEndpointKeyBuilder` class, per backend hard rule 1 forbidding static methods) that both
  `PrinterFleet` (grouping locations into workers) and the counter allocator call, so the string is
  built in exactly one place per root hard rule 6.

**What this task does if an assumption turns out wrong.** Every production class in this task takes
its Core and Infrastructure dependencies through constructor-injected interfaces (never a concrete
`DbContext` type reached for directly outside the two data-access classes named above, never a
static call into T002/T003 code). A signature mismatch discovered in review is therefore a
call-site edit, not a redesign, and the person doing the reconciliation is told exactly that in the
review notes this task's author leaves behind.

## 3. Ordered steps

Work through these in order. TDD is mandatory and test-first, no exceptions (root hard rule 3):
for every class below, write the failing test, run it, quote the red output, then write the
production code, then quote the green output. Do not write production code before its test exists
and has been run red.

### Step 0: Project references

Confirm `backend/GastronomyApp.Infrastructure` already references `backend/GastronomyApp.Core`
(from T001/T003) and add no new package beyond what `Directory.Packages.props` already lists,
except where a step below names one explicitly and justifies it under backend hard rule 5
(official Microsoft or highly regarded community package only, BCL preferred).

`NetworkPrinterTransport`'s TCP work uses `System.Net.Sockets.TcpListener` /
`System.Net.Sockets.TcpClient` from the BCL for both the transport and its in-process fake printer
test double (section 3, Step 4 below); no third-party sockets or ESC/POS package is added.

### Step 1: `EscPosSlipRenderer`, the plain body (red, then green)

File: `backend/GastronomyApp.Infrastructure/Printing/EscPosSlipRenderer.cs`.
Test file: `backend/GastronomyApp.Infrastructure.Tests/Printing/EscPosSlipRendererTest.cs`.

Signature:

```csharp
namespace GastronomyApp.Infrastructure.Printing;

public sealed class EscPosSlipRenderer
{
    public EscPosSlipRenderer(ISlipTextProvider slipTextProvider);

    public RenderedSlip RenderInitialSlip(SlipRenderRequest request);
    public RenderedSlip RenderReprintSlip(SlipRenderRequest request, DateTimeOffset reprintAtUtc, TimeZoneInfo displayTimeZone);
    public RenderedSlip RenderTestSlip(TestSlipRenderRequest request);
}

public sealed record SlipRenderRequest(
    string LocationName,
    string LanguageCode,
    int LocationSequenceNumber,
    int GlobalOrderNumber,
    string TableName,
    string ServerName,
    DateTimeOffset OrderTakenAtUtc,
    TimeZoneInfo DisplayTimeZone,
    IReadOnlyList<SlipLine> Lines,
    string? OrderNote,
    IReadOnlyList<string> AlsoGoesToStationNames,
    string? ChosenStationNameIfDifferent);

public sealed record SlipLine(int Quantity, string ItemName, string? LineNote);

public sealed record TestSlipRenderRequest(
    string LocationName,
    string LanguageCode,
    DateTimeOffset PrintedAtUtc,
    TimeZoneInfo DisplayTimeZone,
    Uri StationCardUrl);

public sealed record RenderedSlip(ReadOnlyMemory<byte> Bytes, string RenderedText);
```

Write the red test first: assert `RenderInitialSlip` on a fixture matching the German example in
spec section 7.7 (`KÜCHE`, `BON 042`, order 137, table 12, server Anna, the four line items
including the wrapped `Hinweis: ohne Ketchup` note, footer `Artikel gesamt: 6`, the
`Diese Bestellung geht auch an: Theke` line) produces `RenderedText` byte-for-byte equal to the
block quoted in spec section 7.7 (allow the CRLF line ending from spec section 7.8's encoding rule
even though the mock, not this class, controls the file's newline; the renderer's own
`RenderedText` uses `\r\n` between slip lines since that is what the printer expects for `\r\n`
processing on Font A, and the mock passes it through unchanged as spec 7.8 requires). Run it,
confirm it fails because `EscPosSlipRenderer` does not exist yet, quote the failure. Then implement
just enough to pass: the init sequence (`ESC @`, `ESC t 19`, `GS a 15`), the double-size location
name and slip number regions (`ESC a 1`, `GS ! 0x11`, `ESC E 1`), the normal-size order header and
body (`ESC a 0`, `GS ! 0x00`), the footer, and `ESC d 4` / `GS V 66 3` at the end, per the command
sequence table in spec section 7.7.

Required test cases, each written red-first as its own `[Test]`:

1. `RenderInitialSlip_GermanFixture_MatchesSpecExample`: full byte comparison against the German
   example block in spec section 7.7, including the exact command bytes (`0x1B 0x40` for `ESC @`,
   `0x1B 0x74 0x13` for `ESC t 19`, `0x1D 0x61 0x0F` for `GS a 15`, `0x1D 0x21 0x11` for
   `GS ! 0x11`, `0x1B 0x45 0x01` for `ESC E 1`, `0x1B 0x61 0x01` / `0x1B 0x61 0x00` for `ESC a 1` /
   `ESC a 0`, `0x1B 0x64 0x04` for `ESC d 4`, `0x1D 0x56 0x42 0x03` for `GS V 66 3`).
2. `RenderInitialSlip_EnglishFixture_MatchesSpecExample`: same fixture, English strings, matches
   the English example block.
3. `RenderInitialSlip_Umlauts_EncodedAsPc858`: an item name containing `ä`, `ö`, `ü`, `Ä`, `Ö`,
   `Ü`, `ß`, and `€` renders each character to its documented PC858 code point (not UTF-8 bytes),
   asserted byte by byte against the PC858 table (`ä` = `0x84`, `ö` = `0x94`, `ü` = `0x81`,
   `Ä` = `0x8E`, `Ö` = `0x99`, `Ü` = `0x9A`, `ß` = `0xE1`, `€` = `0xD5` on PC858; confirm each value
   against the Epson code page 19 reference table before asserting, and quote the source in a code
   comment is forbidden by hard rule 7, so quote it in the test's own name or in this task's
   verification notes instead).
4. `RenderInitialSlip_LongItemName_WrapsIndentedContinuationLine`: an item name longer than 48
   columns (44 after the `N x ` prefix) wraps onto a second line indented by four spaces, with no
   character dropped, per spec section 7.7's truncation rule ("no information on a slip is ever
   dropped to make it fit").
5. `RenderInitialSlip_LongTableOrServerName_WrapsSameWay`: a table label and a server name each
   longer than 48 columns wrap the same way.
6. `RenderInitialSlip_ChosenStationDiffers_FootersChosenStationLine`: when
   `ChosenStationNameIfDifferent` is set, the footer carries the extra
   `Gewählt war: {station}` / `Chosen station was: {station}` line from spec section 7.7.
7. `RenderReprintSlip_German_PrependsReprintBannerWithReprintTime`: matches the reprint header
   example in spec section 7.7 (`NACHDRUCK`, `Nachdruck um 20:31 Uhr` in normal size under the
   banner, both in the location's `DisplayTimeZone`), and the order-header body timestamp still
   shows the original `OrderTakenAtUtc`, not the reprint time (spec 7.7: "The time on the slip is
   the time the order was taken, never the time it was printed").
8. `RenderInitialSlip_FooterCountsUnits_NotLines`: three lines with quantities 2, 1, 3 produce
   `Artikel gesamt: 6`, proving the footer sums `Quantity` rather than counting `Lines.Count`.

### Step 2: `EscPosSlipRenderer`, the station QR card (red, then green)

Same class and test file as Step 1, added as a second red/green pass.

Required test cases:

9. `RenderTestSlip_EmitsGsParenKSequenceInOrder`: asserts the five `GS ( k` functions appear in
   exactly the order given in spec section 7.7's table, with these exact byte sequences:
   * Model select: `1D 28 6B 04 00 31 41 32 00`
   * Module size: `1D 28 6B 03 00 31 43 06`
   * Error correction level M: `1D 28 6B 03 00 31 45 31`
   * Store data: `1D 28 6B pL pH 31 50 30` followed by the URL bytes, where `pL + pH * 256` equals
     `URL.Length + 3`
   * Print stored symbol: `1D 28 6B 03 00 31 51 30`
10. `RenderTestSlip_ShortUrl_PLPHEqualsLengthPlusThree`: a short synthetic URL (well under 69
    bytes) still produces `pL`/`pH` equal to `url.Length + 3`, proving the formula rather than a
    hardcoded length.
11. `RenderTestSlip_69ByteUrl_PLPHEqualsLengthPlusThree`: the realistic 69 byte URL from spec
    section 7.7 (`http://192.168.1.23:5000/station/` plus a 32 character hex key) produces
    `pL = 0x48, pH = 0x00` (69 + 3 = 72 = 0x48).
12. `RenderTestSlip_UrlPrintedAsTextUnderneath`: the same URL appears wrapped over two lines at 48
    columns beneath the symbol commands, matching the German and English test-slip examples in spec
    section 7.7 exactly (including the `TESTBON` / `TEST SLIP` header, no order number, no sequence
    number, per "A test slip carries no order number and no sequence number, because it belongs to
    a printer rather than to an order").
13. `RenderTestSlip_German_MatchesStationCardInstructionsText`: the instructions block below the
    URL matches the German example verbatim.
14. `RenderTestSlip_English_MatchesStationCardInstructionsText`: same for English.

### Step 3: `MockPrinterTransport` (red, then green)

File: `backend/GastronomyApp.Infrastructure/Printing/MockPrinterTransport.cs`, plus
`MockPrinterSession.cs` in the same folder.
Test file: `backend/GastronomyApp.Infrastructure.Tests/Printing/MockPrinterTransportTest.cs`.

Signature:

```csharp
namespace GastronomyApp.Infrastructure.Printing;

public sealed class MockPrinterTransport : IPrinterTransport
{
    public MockPrinterTransport(string dataDirectory, IMockFaultRegistry faultRegistry, TimeProvider timeProvider);

    public PrinterTransportKind Kind { get; }
    public Task<IPrinterSession> ConnectAsync(PrinterEndpoint endpoint, CancellationToken cancellationToken);
}

public interface IMockFaultRegistry
{
    MockFault GetArmedFault(Guid productionLocationId);
    void Arm(Guid productionLocationId, MockFault fault, MockFaultMode mode);
    void ClearIfOnce(Guid productionLocationId);
}

public enum MockFault { None, PaperEnd, CoverOpen, ConnectTimeout, DropSocketEarly, DropSocketMidJob, UnknownOutcome }
public enum MockFaultMode { Once, Sticky }
```

Use `Path.GetTempPath()`-rooted directories in every test per backend hard rule 4 ("No test touches
the developer's database or filesystem"), disposed in `[TearDown]`.

Required test cases, red first, each proving one row of the fault table in spec section 7.8 or one
listed behaviour from spec sections 7.8 and 11.2's "Mock transport" row:

1. `SendJobAsync_NoFault_WritesOneFilePerSlipWithRenderedTextVerbatim`: folder naming
   `{sanitised location name}-{first eight characters of the location id}`, file naming
   `{session start yyyyMMdd-HHmmss}_{folder name}_slip-{sequence, three digits}_print-{n}.txt`,
   UTF-8 no BOM, CRLF line endings, content equal to `PrintPayload.RenderedText` verbatim, outcome
   `Confirmed`, all bytes reported written.
2. `SendJobAsync_PaperEndArmed_ReturnsBlockedWithZeroBytesAndNoFile`.
3. `SendJobAsync_CoverOpenArmed_ReturnsBlockedWithZeroBytesAndNoFile`.
4. `ConnectAsync_ConnectTimeoutArmed_NeverCompletes`: asserts the connect task does not complete
   within a short test timeout while the fault is armed, matching "Never completes `ConnectAsync`".
5. `SendJobAsync_DropSocketEarlyArmed_ReturnsSocketDroppedZeroBytesNoFileWritten`: this is the row
   the task brief calls out by name; assert explicitly that no file exists in the location folder
   afterward, distinguishing it from `DropSocketMidJob`.
6. `SendJobAsync_DropSocketMidJobArmed_WritesPartialFileAndReturnsSocketDroppedWithPartialBytes`:
   the file that was started contains roughly half the rendered text and no more, and the returned
   `BytesWritten` is greater than zero and less than the full payload length.
7. `SendJobAsync_UnknownOutcomeArmed_AcceptsPayloadWritesNoFileNeverEchoesReturnsTimeout`: full
   `BytesWritten` reported (per the fault table: "Bytes written: all"), outcome `Timeout`, and no
   file exists in the folder.
8. `Arm_OnceMode_ClearsAfterOneUseAndSubsequentJobSucceeds`: arm `PaperEnd` as `Once`, send one
   job (gets `Blocked`), send a second job (gets `Confirmed`), matching "PaperEnd armed as Sticky is
   cleared by arming None" contrasted against `Once` clearing itself.
9. `Arm_StickyMode_StaysArmedAcrossMultipleJobs`.
10. `SendJobAsync_ReprintKeepsSequenceNumber_WritesPrintNPlusOneFileBesideOriginal`: first print of
    slip 042 is `print-1`, its first reprint is `print-2`, and the original `print-1` file is not
    overwritten, matching spec section 7.8's `{n}` rule and the 11.2 "Mock transport" row ("a
    reprint written beside its original rather than over it").
11. `SendJobAsync_TwoSessionsSameFolder_DoNotCollide`: two `MockPrinterTransport` instances (or one
    reused across two simulated session starts) with different `session start` timestamps write
    distinct files in the same location folder.
12. `SendJobAsync_TwoLocationsSameName_KeptApartByIdSuffix`: two `ProductionLocation`s both named
    "Theke" produce two distinct folders because of the eight character id suffix.
13. `ConnectAsync_UnwritableDataDirectory_ReturnsPrinterErrorZeroBytesAndReportsPathAndReason`: point
    `dataDirectory` at a path the process cannot write to (a read-only directory created for the
    test), assert the probe-file write/delete fails, `PrinterStatusSnapshot.IsInErrorState` is true,
    `IsOnline` is false, and `Detail` names the full path and the OS-given reason, per spec section
    7.8 ("A folder that cannot be written is a printer fault and is reported as one").
14. `SendJobAsync_TestSlip_FileNameUsesTestPrefixWithProcessId`: file name
    `{session start}_{folder name}_test-{process id}.txt`, matching the test-print row of the file
    naming table.
15. `SendJobAsync_TestSlipWithStationCard_FileContainsBreakGlassUrlAsPlainText`: since a QR code
    cannot exist in a text file, the mock's file for a test slip carries the break-glass URL as
    text, per spec section 7.8.
16. `SendJobAsync_HasNoArtificialDelay`: a job with `MockFault.None` returns without an awaited
    delay (assert wall-clock elapsed time is below a small threshold, for example under 50
    milliseconds), matching "The mock has no artificial delay. It writes the file and returns."
17. `StatusStream_UnknownOutcomeArmed_HoldsJobUntilJobTimeoutSecondsExpires`: a test that needs to
    watch a job while still in flight arms `UnknownOutcome`, which holds the job in `Printing` until
    `JobTimeoutSeconds` expires, per spec section 7.8's closing paragraph on that fault; this is
    exercised through the worker in Step 6, and this transport-level test only proves the session
    reports `IsOnline` throughout without producing a status update that would let the worker move
    on early.
18. `AllSevenFaults_ArmableThroughRegistryInBothOnceAndStickyModes`: a parametrized test over the
    seven `MockFault` values (`None` counts as the eighth "no fault" state and is included to prove
    it clears every other fault), matching 11.2's "all seven faults armable... in both `Once` and
    `Sticky` modes".

### Step 4: In-process fake printer server for `NetworkPrinterTransport` tests

File: `backend/GastronomyApp.Infrastructure.Tests/Printing/FakeEscPosPrinterServer.cs` (test-only,
lives in the test project since production code never depends on it, per backend hard rule 9
keeping transports behind the port interface and nothing above it aware of the implementation).

This is how `NetworkPrinterTransport` is unit-tested without real hardware, as the task brief
requires ("Unit-testable via an in-process fake socket server (the task must specify how; no real
printer exists)"). Write this before Step 5's tests, since they depend on it; it has no red/green
cycle of its own (it is test infrastructure, not a class under test), but write a smoke test proving
the fake server itself accepts a connection and echoes what it is told to, before writing
`NetworkPrinterTransport` tests against it.

Signature:

```csharp
internal sealed class FakeEscPosPrinterServer : IAsyncDisposable
{
    public FakeEscPosPrinterServer(int port);

    public int Port { get; }
    public Task StartAsync(CancellationToken cancellationToken);

    public void ScriptAsbOnConnect(byte paperStatusMask, byte errorStatusMask);
    public void ScriptDleEotResponse(int n, byte statusByte);
    public void ScriptProcessIdEcho(TimeSpan afterDelay);
    public void ScriptNeverEchoProcessId();
    public void ScriptDropConnectionAfterBytes(int byteCount);
    public void ScriptDropConnectionImmediately();

    public IReadOnlyList<byte> ReceivedBytes { get; }

    public ValueTask DisposeAsync();
}
```

Backed by `System.Net.Sockets.TcpListener` bound to `127.0.0.1` on an ephemeral port (pass `0` and
read back the assigned port, so parallel test runs never collide on a fixed port). It parses just
enough of the inbound stream to recognise `DLE EOT n` queries and the `GS ( H` process id request,
and replies according to whichever `Script...` call configured it, otherwise stays silent so a test
can assert a timeout path.

### Step 5: `NetworkPrinterTransport` (red, then green)

File: `backend/GastronomyApp.Infrastructure/Printing/NetworkPrinterTransport.cs`, plus
`NetworkPrinterSession.cs` in the same folder.
Test file: `backend/GastronomyApp.Infrastructure.Tests/Printing/NetworkPrinterTransportTest.cs`.

Signature:

```csharp
namespace GastronomyApp.Infrastructure.Printing;

public sealed class NetworkPrinterTransport : IPrinterTransport
{
    public NetworkPrinterTransport(TimeProvider timeProvider);

    public PrinterTransportKind Kind { get; }
    public Task<IPrinterSession> ConnectAsync(PrinterEndpoint endpoint, CancellationToken cancellationToken);
}
```

`NetworkPrinterSession` implements `IPrinterSession` and holds exactly one `TcpClient` for the
endpoint's lifetime, per root hard rule (hardware constraints: "One printing connection per printer
at a time"). It sends `GS a 15` immediately after `ESC @` on connect (spec section 7.5), parses
every inbound four byte block that is not a process id response as an ASB block (spec section 7.5),
exposes that as `StatusStream`, and answers `QueryStatusAsync` with `DLE EOT n=1`, `n=2`, `n=4`
composited per spec section 7.6's re-query order. `SendJobAsync` writes the payload, then sends
`GS ( H` requesting the process id echo, counts bytes as they are written (spec 7.4 step 7), and
waits for the matching echo up to `endpoint.JobTimeout` (90 seconds per spec section 7.1 and 2.12),
discarding an echo that arrives on a reconnected socket (spec section 2.11 invariant).

Required test cases, red first, against `FakeEscPosPrinterServer`:

1. `ConnectAsync_Success_SendsEscAtThenEscT19ThenGsA15`: asserts the fake server's
   `ReceivedBytes` opens with `1B 40 1B 74 13 1D 61 0F` in that order.
2. `ConnectAsync_Success_HoldsOneConnectionAcrossMultipleJobs`: two consecutive `SendJobAsync`
   calls on the same session reuse one `TcpClient` (assert the fake server sees exactly one
   accepted connection for both jobs).
3. `SendJobAsync_EchoArrivesBeforeTimeout_ReturnsConfirmedWithFullByteCount`.
4. `SendJobAsync_EchoNeverArrives_ReturnsTimeoutAfterJobTimeoutWithBytesWrittenGreaterThanZero`:
   use a short `JobTimeout` (for example 200 milliseconds) in the test's `PrinterEndpoint` so the
   test does not wait 90 real seconds; the 90 second default itself is asserted separately as a
   configuration-level test (`PrinterConfigurationTest` in T003's scope, referenced here only to
   note it, not duplicated).
5. `SendJobAsync_ConnectionDropsBeforeFirstByte_ReturnsSocketDroppedWithZeroBytes`.
6. `SendJobAsync_ConnectionDropsMidWrite_ReturnsSocketDroppedWithPartialBytesGreaterThanZero`.
7. `SendJobAsync_EchoOnReconnectedSocket_IsDiscarded`: script the original socket to drop after
   the job's bytes are sent, reconnect a fresh session, script the fake server to send the stale
   echo down the *original* (now-dead) socket path is not reachable, so instead simulate this at
   the session level: assert that `NetworkPrinterSession` tracks which socket sent which
   `ProcessId` and a session created after a reconnect never treats an echo not tied to its own
   connection as a match, by scripting the new connection to receive an echo carrying a *different*
   process id than the one the new job used and asserting the result is `Timeout`, not `Confirmed`.
8. `QueryStatusAsync_PaperEndMaskSet_ReportsIsPaperEndTrue`: `DLE EOT n=4` bits 5 and 6 both set
   (mask `0x60`) decodes to `IsPaperEnd = true`, per spec section 7.1.
9. `QueryStatusAsync_CoverOpenBitSet_ReportsIsCoverOpenTrue`: `DLE EOT n=2` bit 2 set decodes to
   `IsCoverOpen = true`.
10. `StatusStream_AsbPushedUnprompted_SurfacesWithoutAQuery`: the fake server sends an unsolicited
    four byte ASB block after connect; `StatusStream` yields a matching `PrinterStatusSnapshot`
    without the test having called `QueryStatusAsync`.
11. `BytesWritten_AlwaysCountsBytesHandedToSocket_NotBytesAcknowledged`: asserts the reported count
    equals the payload length written to the socket even when the fake server never acknowledges
    anything at the ESC/POS level (there is no acknowledgement primitive below the process id echo),
    matching spec section 7.2's note that `BytesWritten` "counts bytes handed to the socket rather
    than bytes the printer acknowledged, which nothing can know".
12. `ConnectAsync_HeartbeatEvery10Seconds_KeepsIdleTimeoutAlive`: using a short heartbeat interval
    in the test `PrinterEndpoint`, assert `DLE EOT n=4` is sent at that cadence while no job is in
    flight, matching spec section 7.3 ("kept alive by the `DLE EOT n=4` heartbeat every 10
    seconds").
13. `SendJobAsync_JobTimeoutDefaultsTo90Seconds_MatchesPrinterConfigurationDefault`: a
    configuration-shape assertion that `NetworkPrinterTransport` respects whatever `JobTimeout` the
    caller passes rather than hardcoding a different value, proven by passing two different
    `PrinterEndpoint.JobTimeout` values in two test cases and observing the timeout fire at each.

### Step 6: `PrinterWorker` (red, then green)

File: `backend/GastronomyApp.Api/Printing/PrinterWorker.cs`.
Test file: `backend/GastronomyApp.Api.Tests/Printing/PrinterWorkerTest.cs`.

`PrinterWorker` is not itself an `IHostedService`; `PrinterFleet` (Step 7) is the hosted service
that owns the collection of workers. `PrinterWorker` is a plain class, one instance per distinct
printer endpoint, so it is unit-testable without the ASP.NET Core host.

Signature:

```csharp
namespace GastronomyApp.Api.Printing;

public sealed class PrinterWorker
{
    public PrinterWorker(
        PrinterEndpoint endpoint,
        IPrinterTransport transport,
        IPrinterWorkerDataAccess dataAccess,
        IPrintCallbacks callbacks,
        EscPosSlipRenderer renderer,
        TimeProvider timeProvider);

    public Guid[] ServedProductionLocationIds { get; }

    public Task RunAsync(CancellationToken cancellationToken);
    public void Enqueue(Guid locationTicketId);
    public Task RecoverAtStartupAsync(CancellationToken cancellationToken);
}

public interface IPrintCallbacks
{
    Task OnTicketStatusChangedAsync(Guid orderId, Guid locationTicketId, TicketStatus newStatus, string? failureReason, CancellationToken ct);
    Task OnOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct);
    Task OnPrinterStatusChangedAsync(Guid productionLocationId, PrinterStatusSnapshot snapshot, bool isFaulty, int waitingTicketCount, CancellationToken ct);
}

public interface IPrinterWorkerDataAccess
{
    Task<TicketLoadResult> LoadTicketForPrintingAsync(Guid locationTicketId, CancellationToken ct);
    Task<ClaimResult> TryClaimAsync(Guid locationTicketId, CancellationToken ct);
    Task RecordAttemptAsync(PrintAttempt attempt, CancellationToken ct);
    Task ApplyOutcomeAsync(Guid locationTicketId, PrintDispatchOutcome outcome, int bytesWritten, PrinterStatusSnapshot statusAtEnd, CancellationToken ct);
    Task<IReadOnlyList<Guid>> LoadRecoverableTicketIdsAsync(IReadOnlyCollection<Guid> servedLocationIds, CancellationToken ct);
    Task MarkPrintingTicketsUnknownAsync(IReadOnlyCollection<Guid> servedLocationIds, CancellationToken ct);
    Task<int> CountWaitingTicketsAsync(Guid productionLocationId, CancellationToken ct);
    Task FailAllWaitingAtEndpointAsync(IReadOnlyCollection<Guid> servedLocationIds, string failureReason, CancellationToken ct);
    Task<int> AllocateProcessIdAsync(string printerEndpointKey, CancellationToken ct);
}
```

`IPrinterWorkerDataAccess` is this task's boundary against the T003 uncertainty named in section 2:
its implementation (`EfCorePrinterWorkerDataAccess`, in `GastronomyApp.Infrastructure`) is the only
class in this task that touches `DbContext` directly, so a reconciliation with T003's actual
repository shape touches one file, not the worker's tests.

Write these tests red first, each against `IPrinterWorkerDataAccess` and `IPrintCallbacks` faked
with FakeItEasy (`A.Fake<IPrinterWorkerDataAccess>()`), and `IPrinterTransport` /
`IPrinterSession` faked the same way for the pure-logic tests, reserving `MockPrinterTransport`
itself for the integration tests in Step 8:

1. `RunAsync_ClaimFailsBecauseTicketNoLongerWaiting_EndsFailedWithTicketResolvedByHumanZeroBytes`:
   proves spec section 7.4 step 2's first bullet: the socket is never touched (assert the fake
   `IPrinterTransport.ConnectAsync` / session is never called), the job ends `Failed` with
   `FailureReason.TicketResolvedByHuman`, zero bytes recorded.
2. `RunAsync_ClaimSucceeds_MovesTicketToPrintingBeforeSending`.
3. `RunAsync_PrinterDeclaredFaultySinceEnqueue_LeavesTicketUnclaimedForBreakerToResolve`: spec
   section 7.4 step 2's third bullet.
4. `RunAsync_NoOpenSession_ConnectsWithConfiguredConnectTimeout`.
5. `RunAsync_ConnectFails_MarksPrinterOfflineLeavesJobQueuedSchedulesReconnectWithBackoff`: asserts
   the backoff sequence 1, 2, 5, 10, 30 seconds capped at 30, per spec section 7.4 step 3 (use
   `TimeProvider`'s fake clock to assert scheduled delays without a real wait).
6. `RunAsync_PreflightAsbFresherThanHeartbeatInterval_SkipsQuery`: spec section 7.4 step 4's
   freshness rule.
7. `RunAsync_PreflightStaleAsb_QueriesDleEotN4AndN2`.
8. `RunAsync_PreflightBlocking_MovesToBlockedWithZeroBytesNoSend`.
9. `RunAsync_PreflightClean_ProceedsToRenderAndSend`.
10. `RunAsync_RendersBeforeAllocatingProcessId_ProcessIdNullUntilStep6`: asserts the render call
    happens and completes before `AllocateProcessIdAsync` is invoked, matching "This is the first
    step at which `PrintJob.ProcessId` is anything but null" (spec 7.4 step 6).
11. `RunAsync_AllocatesProcessIdFromEndpointKeyedCounter_NotLocationKeyed`: asserts the
    `printerEndpointKey` argument passed to `AllocateProcessIdAsync` is built from the endpoint
    (`TransportKind|Host|Port|AgentIdentifier`), not from `ProductionLocationId`.
12. `RunAsync_SendsPayloadThenWaitsForEchoUpToJobTimeout`.
13. `RunAsync_RecordsAttemptWithOutcomePhaseBytesAndStatusSnapshot`: asserts
    `RecordAttemptAsync` is called with a `PrintAttempt` carrying all five fields from spec section
    2.11's table.
14. `RunAsync_PushesTicketStatusChangedAlways_AndOrderStatusChangedOnlyWhenItChanged`: spec section
    7.4 step 10; assert `OnOrderStatusChangedAsync` is not called when the recomputed order status
    equals the prior one.
15. `Enqueue_JobsAttemptedInLocationTicketCreatedAtUtcOrder`: enqueue three tickets out of creation
    order, assert the worker's internal queue drains them oldest-`CreatedAtUtc`-first, per spec
    section 7.3.
16. `RunAsync_BlockedJobHoldsStationRatherThanBeingOvertaken`: a `Blocked` job at the head of the
    queue is not skipped in favour of a later `Queued` job, per spec section 7.3 and 4.2.
17. `RecoverAtStartupAsync_EnqueuesQueuedAndBlockedTicketsOfAnyEventSession_OldestFirst`: spec
    section 7.3's startup recovery bullet, asserting the call is not scoped to the current
    `EventSessionId`.
18. `RecoverAtStartupAsync_MarksPrintingTicketsUnknown`: spec section 7.3's crash recovery bullet.
19. `RunAsync_HeartbeatEvery10Seconds_UsingFakeTimeProvider`.
20. `RunAsync_TwoConsecutiveHeartbeatsUnanswered_MarksPrinterOfflineAndForcesReconnect`: spec
    section 7.5.
21. `RunAsync_BlockingConditionClearsFromSetToClear_ReleasesBlockedJobsInCreatedAtUtcOrder`: spec
    section 7.5's status-change exception bullet, covering paper end, cover open, and error state
    each independently clearing.
22. `RunAsync_RetryMapping_ZeroBytesRetried_BytesNeverRetried`: a table-driven test iterating every
    row of spec section 7.6's mapping table (`Confirmed`/`Network`, `Confirmed`/`Mock`, `Blocked`/0,
    `Unreachable`/0, `SocketDropped`/0, `SocketDropped`/>0, `Timeout`/0, `Timeout`/>0,
    `PrinterError`/0, `PrinterError`/>0), asserting the resulting job state, ticket state, and
    whether an automatic retry is scheduled, matches the table exactly. This is the `RetryPolicy`
    coverage named in spec section 11.1, implemented as the worker's own outcome-to-state mapping
    method (a private `MapOutcome` or an extracted `RetryPolicy` class the worker calls; extracting
    it to its own class in `GastronomyApp.Core` is preferred, per root hard rule 6, since the
    mapping is pure and belongs with the state machines T002 owns: if T002 has already placed a
    `RetryPolicy` class in Core, this task's worker calls that class instead of reimplementing the
    table, and the review reconciles the constructor).
23. `StationCircuitBreaker_TwoConsecutiveUnknownOrTimeoutOutcomes_TripsAtEndpoint`: asserts
    `IsFaulty` is set for every `ServedProductionLocationIds` entry and
    `FailAllWaitingAtEndpointAsync` is called once, in one logical transaction (assert both effects
    happen from a single `IPrinterWorkerDataAccess` call rather than two separate calls that could
    partially fail).
24. `StationCircuitBreaker_AConfirmedJobResetsTheConsecutiveCounter`.
25. `StationCircuitBreaker_QueueDepthNeverTripsIt`: fifty waiting tickets at a station out of
    paper (`Blocked`, not `Unknown`/`Timeout`) never trips the breaker, per spec section 7.6 and
    11.1's `StationCircuitBreaker` row.
26. `StationCircuitBreaker_HumanReconnectClearsFaultyAndRestartsWorker`: a reconnect call clears
    `IsFaulty` on every served location and resumes attempting jobs.
27. `GiveUpWindow_MeasuredFromLocationTicketCreatedAtUtc_NotFromFirstAttempt`: spec section 3.2.
28. `GiveUpWindow_SuspendedForFourKnownCauses_ResumesWithoutResettingWhenCauseClears`: the four
    suspending causes: paper end, cover open, station disabled, mock folder unwritable, per spec
    sections 3.0 and 7.8. Accumulate unsuspended time across an alternating sequence of
    suspend/resume per spec section 11.1's `GiveUpWindow` row.
29. `GiveUpWindow_MechanicalErrorBlockedTicket_IsNotSuspendedAndExpiresAtFiveMinutes`.
30. `OuterBound_TwentyMinutes_FiresUnderEveryKnownCause_NeverFiresOnPrinting`: assert a ticket
    that is 20 minutes old while `Printing` (bytes on the wire) is re-evaluated only once the job
    ends, never failed mid-flight.

### Step 7: `PrinterFleet` (Api hosted service)

File: `backend/GastronomyApp.Api/Printing/PrinterFleet.cs`.
Test file: `backend/GastronomyApp.Api.Tests/Printing/PrinterFleetTest.cs`.

Signature:

```csharp
namespace GastronomyApp.Api.Printing;

public sealed class PrinterFleet : IHostedService
{
    public PrinterFleet(
        IPrinterConfigurationSource configurationSource,
        IPrinterTransportFactory transportFactory,
        IPrinterWorkerDataAccess dataAccess,
        IPrintCallbacks callbacks,
        EscPosSlipRenderer renderer,
        TimeProvider timeProvider);

    public Task StartAsync(CancellationToken cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken);
    public Task ReconcileAsync(CancellationToken cancellationToken);
}

public interface IPrinterTransportFactory
{
    IPrinterTransport Create(PrinterTransportKind kind);
}

public interface IPrinterConfigurationSource
{
    Task<IReadOnlyList<(ProductionLocation Location, PrinterConfiguration Configuration)>> LoadEnabledAsync(CancellationToken ct);
}
```

Required tests, red first:

1. `StartAsync_GroupsLocationsByDistinctEndpoint_StartsOneWorkerPerEndpoint`: two locations with
   identical `(TransportKind, Host, Port, AgentIdentifier)` produce one `PrinterWorker`, not two, per
   spec section 7.3.
2. `StartAsync_TwoLocationsDifferentEndpoints_StartsTwoWorkers`.
3. `ReconcileAsync_LocationDisabledOrMovedToNewEndpoint_StopsItsOldWorkerWhenLastLocationLeaves`:
   per spec section 7.3's "stops a worker whose last location was disabled or moved elsewhere".
4. `ReconcileAsync_ConfigurationChangeAddingLocationToExistingEndpoint_JoinsExistingWorkerNotANewOne`.
5. `StartAsync_CallsRecoverAtStartupOnEveryWorker`.
6. `StopAsync_StopsEveryWorkerCleanly`.

### Step 8: Printing-only integration scenarios, no HTTP

File: `backend/GastronomyApp.Api.Tests/Printing/PrinterWorkerIntegrationTest.cs`, using in-memory
SQLite (`Data Source=:memory:`) per backend hard rule 4, a real `MockPrinterTransport` pointed at a
`Path.GetTempPath()` directory, a real `EscPosSlipRenderer`, and a real `PrinterWorker` /
`PrinterFleet` pair. No `WebApplicationFactory`, no HTTP client, no SignalR hub: `IPrintCallbacks`
is a recording fake so assertions read its recorded calls directly, per this task's explicit
out-of-scope boundary (SignalR wiring is the Api task's job, this task only defines the callback
interface and proves the worker calls it correctly).

Required scenarios, each red first against seeded `LocationTicket`/`Order`/`ProductionLocation`
rows and driven purely through `PrinterFleet`/`PrinterWorker` plus `MockPrinterTransport` fault
injection:

1. `PaperOutMidEvening_TicketBlocksThenPrintsItselfWhenClearedNoAutoRetryQuestion`: matches spec
   section 11.3's "Paper out before sending" and "Paper loaded afterwards" scenarios combined into
   one worker-level test: arm `PaperEnd`, submit a ticket, assert `Blocked` with zero bytes and
   `OnPrinterStatusChangedAsync` fired; arm `None`, assert the parked ticket reaches
   `PrintedOnTestPrinter` without any external re-send call.
2. `DropSocketEarly_AutoRetriesWithNoQuestionAskedAndSlipEventuallyPrints`: arm `DropSocketEarly`
   as `Once`, assert the worker retries with backoff automatically and no `Unknown`-shaped callback
   ever fires, then the retry (fault now cleared) succeeds.
3. `BytesWrittenGreaterThanZero_DropsToUnknown_NeverAutoRetried`: arm `DropSocketMidJob`, assert
   the ticket lands on `Unknown` and stays there across repeated worker ticks with no further
   automatic send attempt.
4. `TwoConsecutiveUnknownOutcomes_TripsBreakerFailsEveryWaitingTicketAtEndpointThenClearsOnReconnect`
  : arm `UnknownOutcome` `Sticky`, enqueue two tickets at the same endpoint (spanning two
   locations sharing the endpoint), assert both fail with `StationFaulty` in one data-access call,
   then simulate the reconnect action and assert `IsFaulty` clears on both and a subsequent good job
   succeeds.
5. `SharedEndpointInterleaving_PreservesPerLocationSequenceOrder`: two locations on one endpoint,
   tickets submitted interleaved across both, assert each location's own tickets are attempted in
   that location's `LocationSequenceNumber` order even though the shared queue interleaves the two
   locations' jobs on the wire (matching spec section 4.2's "each station's own run of numbers stays
   unbroken").
6. `Reprint_ReusesNumbersIncrementsReprintCountPrintsBanner`: a `Reprint` kind `PrintJob` against
   an already-`Printed` ticket keeps `GlobalOrderNumber` and `LocationSequenceNumber` unchanged,
   increments `ReprintCount`, and the mock's written file contains the reprint banner and reprint
   time from Step 1's renderer output, matching spec sections 3.4, 4.2, and 7.8's `print-{n}` rule.

## 4. Constraints

Carried forward from the root, backend, and this task's own scope, binding everything written here:

- No code comments anywhere in production or test code beyond the one narrow exception in root hard
  rule 7 (a documented hardware constraint this task's ESC/POS byte sequences already state as
  spec-cited literals, not as comments; prefer naming the constant instead of commenting it, for
  example `private const byte GsInitCommand = 0x1D;` rather than a comment explaining the byte).
- No static methods or properties anywhere in this task's code, including the endpoint-key builder
  (assumed non-static in section 2) and any helper class. Framework metadata registration is the
  only exception, and nothing in this task's scope needs it.
- No empty catch blocks. A caught exception in the renderer, either transport, or the worker either
  rethrows, surfaces through `PrintDispatchResult`/`PrinterStatusSnapshot`, or is otherwise turned
  into a value a caller acts on.
- No positional tuple access anywhere; every multi-value return in this task (for example
  `PrinterConfigurationSource`'s load result) is a named record or record struct.
- Never use the em-dash character, and never a hyphen as its substitute, anywhere in this document,
  in code identifiers, in test names, or in commit-adjacent text this task produces.
- Localization stays resx-only, wired through `ISlipTextProvider`/`ResxSlipTextProvider` per
  section 2. Both `Strings.de.resx` and `Strings.en.resx` (or the domain-specific pair this task
  adds, for example `SlipStrings.de.resx` / `SlipStrings.en.resx`) must contain every key this task
  introduces, in both languages, in the same change that introduces the key, per root hard rule 8
  and backend hard rule 2.
- `MockPrinterTransport` stays exactly as simple as backend hard rule 10 and the root CLAUDE.md
  "Current phase" section describe: one file per slip, the seven fault controls, nothing else. Do
  not add a rendered station screen, a pile visualisation, or styling to it under any pretext.
- Print jobs are serialized per printer, structurally, per backend hard rule 11: no code path in
  this task ever opens a second socket or a second mock session against the same endpoint while one
  is already open.
- No test in this task touches the developer's real database or filesystem: in-memory SQLite for
  anything using EF Core, `Path.GetTempPath()` directories disposed in teardown for anything
  touching the mock's files.
- No niche or unmaintained NuGet package. This task adds none beyond what
  `Directory.Packages.props` already lists; if a genuine gap appears (for example a QR-encoding
  library), this task renders the `GS ( k` bytes itself per the exact sequence in spec section 7.7
  rather than reaching for a third-party QR library, because the printer does the QR encoding
  internally from the stored data command, not the renderer.
- Every fixture is `<ClassUnderTest>Test`, one file per fixture, method names
  `MethodName_State_Expected`, per backend CLAUDE.md testing conventions.
- No trademarked words in file names or identifiers introduced by this task.

## 5. Verification

Run all commands from the repository root. Quote the real command output for each; do not claim a
step is done without having run it and pasted the result, per root hard rule 5.

1. For every red step in section 3, before writing the corresponding production code:
   ```powershell
   dotnet test GastronomyApp.slnx --filter "FullyQualifiedName~<TheNewTestFixture>"
   ```
   Quote the failing output (the fixture or method not found, or the assertion failure) before
   proceeding to the production code for that step.

2. After each production step:
   ```powershell
   dotnet test GastronomyApp.slnx --filter "FullyQualifiedName~<TheNewTestFixture>"
   ```
   Quote the passing output.

3. Full backend build and test pass at the end of this task:
   ```powershell
   dotnet build GastronomyApp.slnx
   dotnet test GastronomyApp.slnx
   ```
   Success looks like `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`, and every fixture
   named in section 3 reporting `Passed:` with zero `Failed:`.

4. Targeted re-run of just this task's fixtures, useful once T002/T003 land for real and this
   task's assumed signatures are reconciled:
   ```powershell
   dotnet test GastronomyApp.slnx --filter "FullyQualifiedName~Printing"
   ```

## 6. Out of scope

Explicitly not part of this task; do not attempt any of it here even if it seems like a natural next
step:

- Any REST endpoint (`POST /api/orders/{orderId}/tickets/{ticketId}/reprint`,
  `POST /api/admin/printers/{locationId}/reconnect`, `POST /api/admin/mock/{locationId}/fault`,
  `GET /api/printers/status`, or any other route named in spec section 5). This task defines and
  proves the worker's behaviour; another task wires HTTP handlers that call into
  `IPrinterWorkerDataAccess`-shaped operations or the callback interface's inverse.
- The SignalR hub itself, and any `HubContext` usage. This task defines `IPrintCallbacks` precisely
  (section 3, Step 6) so the Api task that owns SignalR can implement it and register it in the
  composition root; this task's own tests use a recording fake for that interface and never touch
  SignalR types.
- `AgentPrinterTransport` (the Pi agent transport). Deferred per spec section 7.2's table and the
  root CLAUDE.md; not started, not stubbed beyond what already exists in `PrinterTransportKind`.
- Real-hardware verification against a physical TM-T20IV. Open questions 1 and 11 in spec section
  12 stay open. This task's `NetworkPrinterTransport` and the `GS ( H` / `GS ( k` sequences compile
  and are unit-tested against the in-process fake server from Step 4, which is a documented
  assumption about firmware behaviour, not a fact verified against real hardware. The `GS ( H`
  path is the primary path implemented; the fallback described in open question 1 (treating a clean
  write plus a clean `DLE EOT n=4` read as a weaker confirmation) is not implemented in this task,
  since it is only exercised if the primary path is later found not to work on real firmware.
- The composition root wiring that registers `PrinterFleet` as a hosted service in
  `GastronomyApp.Api`'s `Program`/startup configuration, and the registration of
  `ResxSlipTextProvider` and `EfCorePrinterWorkerDataAccess` in dependency injection. This task
  writes the classes and their tests; wiring them into the actual ASP.NET Core host belongs to
  whichever task builds the composition root (assumed to be T003 or a later Api-wiring task), since
  this task's classes are constructor-injected and testable without a running host.
- The admin printer screen, the break-glass page, and any frontend code whatsoever.
- Device authentication, order acceptance, routing, or numbering allocation logic beyond the single
  `AllocateProcessIdAsync` call this task's worker makes at spec section 7.4 step 6. Global order
  numbers and location sequence numbers are allocated during order acceptance, which is T003's
  scope per spec section 4.1.
- Any `git` command. The owner reviews and commits this work separately, per root hard rule 12.

## 7. Ambiguities and chosen readings

- **Whether `RetryPolicy` and the state machines live in `GastronomyApp.Core` or inside this
  task's worker.** The spec names `RetryPolicy`, `TicketStateMachine`, and `PrintJobStateMachine`
  as unit-tested classes in section 11.1 without saying which project owns them. Root hard rule 6
  ("extend the concept's existing home") and the backend project table (Core holds "domain models,
  ports, use cases... no framework dependencies") both point at Core, since the outcome-to-state
  mapping is pure domain logic with no printer, socket, or EF Core dependency. This task assumes
  T002 owns them and calls them from the worker; if T002 does not produce them, this task's
  fallback (an extracted `RetryPolicy` class placed in Core by this task itself) is named in Step 6
  so the worker's tests do not silently depend on inline logic nobody can find later.
- **Whether the mock's `ISlipTextProvider` implementation lives in Infrastructure or Api.** The
  backend project table puts "printer transports" in Infrastructure and "composition root" in Api.
  This task places `ResxSlipTextProvider` in Infrastructure, next to the renderer that consumes it,
  and assumes the Api composition root only registers it, per section 6's out-of-scope note.
- **The exact wire format the fake ESC/POS server needs to parse.** Spec section 7 documents
  `ESC @`, `ESC t 19`, `GS a 15`, `DLE EOT n`, and `GS ( H`/`GS ( k` precisely enough to script
  fixed byte responses, but does not specify a general ESC/POS parser. This task's
  `FakeEscPosPrinterServer` (Step 4) is scripted per test rather than a general parser, which keeps
  it honest about only supporting what the tests actually exercise rather than silently emulating
  more of the protocol than has been verified against any real datasheet passage.
- **Whether `PrinterWorker.RunAsync` is a `while` loop meant to run for the process lifetime or a
  single drain-the-queue-once method.** Section 3.2 states there is a single consumer per worker
  and the worker owns a queue; this task assumes `RunAsync` is the long-running loop `PrinterFleet`
  starts once per worker and keeps running until the supplied `CancellationToken` is cancelled at
  shutdown, with `Enqueue` adding work from outside (called by whatever the acceptance and
  reconnect/reprint call sites turn out to be in later tasks) and the loop waking on a new item or
  its own heartbeat timer, whichever comes first.
