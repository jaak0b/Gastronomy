# Add a refusal

A refusal is how the laptop says no. What a refusal carries, how `ResultEnvelope` answers it and what a
handler may do with it is in the Refusals section of `backend/CLAUDE.md`; this page is the order of
work.

## Decide the kind first

Root rule 2 gives two kinds.

- **The person can act on it.** A menu that changed under a waiter, a name that is taken. It gets a key
  of its own, parameters, and a sentence in both languages that names the item or the tap that clears
  it. Example: `Refusal.Ingredients.NameBelongsToDeactivatedIngredient`.
- **A guard against a broken program.** The screen already stops the person from doing it, so the call
  did not come from that screen. The server keeps the check and writes the exact reason in
  `Description`; the person gets the shared sentence and no new text exists. The `Code` is
  `errors.admin.actionFailed` on an admin screen, `RefusalMessageKeys.OrderCannotBeProcessed` for an
  order and `SettlementCannotBeProcessed` for a settlement. Examples:
  `Refusal.Ingredients.UsedByActiveItems`, `Refusal.Order.StationRequired`.

A refusal that only means "not there" is a bare status: `NotFound` or `Unauthorized` without a
`ProblemCode`, as `Refusal.Ingredients.IngredientNotFound` is. Nothing else may be answered bare.

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
   `errors.admin.actionFailed`.
5. **The caller.** A service returns `ErrorOr<T>` with the factory call. A list of refusals answers
   with its first entry and logs all of them.

## Tests that must change

- Service test in `GastronomyApp.Core.Tests/Services`: assert `RefusalMessageKey()`,
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
