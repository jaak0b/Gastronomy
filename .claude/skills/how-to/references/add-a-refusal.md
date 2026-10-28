# Add a refusal

A refusal is how the laptop says no. This page says what a refusal carries, how it is answered, and
the order of work for a new one.

## What a refusal carries

A refusal is an ErrorOr `Error` built by a factory in `Core/Refusals`, one partial file per area of
the one static class `Refusal` (`Refusal.Order`, `Refusal.StationQueue`, `Refusal.Festival` and so
on). A factory carries:

- **`Code`**: the frontend's message key. A refusal the API answers with a bare status carries a
  plain name instead, because no device ever reads it.
- **`Description`**: one sentence naming the exact reason, written for the developer reading the log.
- **`Metadata`**: the parameters the message needs, under the fixed keys in `Refusal.MetadataKeys`,
  plus `ProblemCode`, the `ApiError.Code` value the frontend receives.
- **`NumericType`**: the HTTP status the refusal answers, declared once in `RefusalType`. Every
  factory goes through a private helper on `Refusal` named after that status; the built-in
  `Error.Validation`, `Error.NotFound` and their siblings are not used.

## How it is answered

`ResultEnvelope` in the Api answers a refusal: it logs the recording of the refused lines at Warning
with the request path, takes the status from `NumericType`, and writes an `ApiError` built from the
`ProblemCode` metadata, the `Code` and the remaining metadata as parameters. A refusal without a
`ProblemCode` is answered with the bare status and no body. Where a command validates a list, every
bad line is collected into `List<Error>`; the envelope answers with the first line of that list, in the
order the caller handed it over, and the log holds them all.

An endpoint's body is one expression: the service call, `Then`, `ThenDo` and their async siblings for
what follows a success, and nothing else. An endpoint holds no `ErrorOr` in a local, switches on no
refusal, writes no status code and never touches `ResultEnvelope`.

**The endpoint's return type is the status it answers with.** The three answer types in `Api/Answers`
each take the service's `ErrorOr<TView>` through an implicit conversion, so the body stays that one
expression: `ApiAnswer<TView>` answers 200, `CreatedAnswer<TView>` answers 201 and `NoContentAnswer`
answers 204. Each is an `IResult` that writes the view as JSON on success and hands a refusal to
`ResultEnvelope`, which it resolves from the request's services; the shared part of that lives once,
in `ViewOrRefusal<TView>`. Each is also an `IEndpointMetadataProvider` and describes its own response
in `PopulateMetadata`, so a route that answers through one declares no `Produces` of its own and the
OpenAPI document still carries one response per route. `ApiAnswer<TView>` also converts from a bare
`TView`, which is what a query that cannot refuse returns. The invitation QR route answers `IResult`
itself, because it writes an SVG image rather than a view, and declares its response with
`Produces`.

**A refused request shape** (see the `request-validation` page of `architecture`) answers 400 with the
same `ApiError` every other refusal uses. Every DataAnnotations attribute carries the frontend's
message key as its `ErrorMessage`, a constant in `Contracts/Validation/RefusalMessageKeys.cs`.
`RequestShapeRefusalWriter` is the `IProblemDetailsService` the validation filter writes through, and
it maps every refused member to `Refusal.RequestShape.MemberRefused` in the order the request record
declares its properties, then hands that list to `ResultEnvelope` like every other refusal. A refused
shape that names no member is a broken program and throws.

## Decide the kind first

Root rule 2 gives two kinds.

- **The person can act on it.** A menu that changed under a waiter, a name that is taken. It gets a key
  of its own, parameters, and a sentence in both languages that names the item or the tap that clears
  it. Example: `Refusal.Ingredients.NameBelongsToDeactivatedIngredient`.
- **A guard against a broken program.** The screen already stops the person from doing it, so the call
  did not come from that screen. The server keeps the check and writes the exact reason in
  `Description`; the person gets the shared sentence and no new text exists. The `Code` is
  `common.errors.actionFailed` (`RefusalMessageKeys.ActionFailed`) on an admin screen,
  `RefusalMessageKeys.OrderCannotBeProcessed` for an order and `SettlementCannotBeProcessed` for a
  settlement. Examples:
  `Refusal.Ingredients.UsedByActiveItems`, `Refusal.Order.StationRequired`.

A refusal that only means "not there" is a bare status: `NotFound` or `Unauthorized` without a
`ProblemCode`, as `Refusal.Ingredients.IngredientNotFound` is. Nothing else may be answered bare.

An action that fails in the browser itself, with no answer from the laptop and nothing more specific
to say, shows the shared `common.errors.actionFailed` ("Die Aktion ist fehlgeschlagen.", "The action
failed."), as the copy button of `InvitationPanel.vue` does when the browser offers no clipboard.
Never add a key of its own for such a failure.

## Steps

1. **The factory.** Add a `public static Error` to the partial file of its area in
   `backend/GastronomyApp.Core/Refusals` (`Refusal.<Area>.cs`, a nested static class per area; a new
   area is a new partial file). Build it through the private status helper in `Refusal.Builders.cs`
   (`BadRequest`, `Unauthorized`, `NotFound`, `Conflict`, `Gone`, `UnprocessableEntity`,
   `ServiceUnavailable`), which sets the HTTP status from `RefusalType`.
2. **The key.** A key a contract attribute names, or that two places send, is a constant in
   `backend/GastronomyApp.Contracts/Validation/RefusalMessageKeys.cs`. A key one factory alone sends
   stays a literal in the factory, as `Refusal.CatalogItem.NameTaken` does.
3. **The metadata.** Put `Refusal.MetadataKeys.ProblemCode` in (one of `Refusal.ProblemCodes`, or a
   plain name such as `"IngredientUsedByActiveItems"` for a conflict). Every other entry reaches the
   frontend as a sentence parameter, under a name from `MetadataKeys`; add a constant there when none
   fits. `MetadataKeys.Count` selects the plural form of the sentence.
4. **The sentence.** For the first kind, add `errors.<area>.<name>` to
   `frontend/src/shared/i18n/de.json` and `en.json` with the same `{placeholders}` as the metadata
   names. Read `no-ai-slop` first. An admin screen shows a key the German file lacks as
   `common.errors.actionFailed`.
5. **The caller.** A service method returns `ErrorOr<T>` with the factory call. A list of refusals answers
   with its first entry and logs all of them.

## Tests that must change

- Service test in `GastronomyApp.Core.Tests`: assert `RefusalMessageKey()`,
  `FirstError.NumericType` and `RefusalMetadata(...)` (`TestSupport/RefusalReading.cs`).
- Endpoint test in `GastronomyApp.Api.Tests/Endpoints`: status, `code`, `messageKey` and `parameters`
  of the body, as `AdminIngredientEndpointsTest` does.
- `GastronomyApp.Api.Tests/ErrorHandling/RefusalTest` finds every factory by reflection. A factory
  whose parameter type has no stand-in value throws there, so add a branch to `DummyFor`. It also
  fails when a refusal with a `ProblemCode` names a key `de.json` lacks, when one without it is not a
  404 or 401, and when `Description` is empty.
- `frontend/tests/shared/i18nCoverage.spec.ts` fails when an `"errors.…"` literal in the backend has no
  wording in either language, or the two files differ in keys or placeholders.
- A screen that shows the sentence gets a spec that answers with `refusal(key, { status, parameters })`
  from `tests/support/laptop.ts`.

Narrow runs: `dotnet test backend/GastronomyApp.Api.Tests --filter "FullyQualifiedName~RefusalTest"`
and `npx vitest run tests/shared/i18nCoverage.spec.ts` in `frontend/`.
