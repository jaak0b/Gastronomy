# The generated types are the boundary

## Decision

The records in `GastronomyApp.Contracts` are the one source of every request and response shape,
field name and type and enum value on the wire. The build writes them into an OpenAPI document, and
the frontend generates its TypeScript types and zod schemas from that document, under the backend's
own contract names. The frontend uses those generated types for every request body and every
answer, never a hand-written copy, so a name the two sides disagree on fails the frontend build
instead of the running app. This is why a change that spans both trees builds the backend first
(root rule 17).

## Laptop

`dotnet build` writes
`backend/GastronomyApp.Api.OpenApiHost/openapi/GastronomyApp.Api.OpenApiHost.json`. The document is
committed to the repository and lives in the host project because that project writes it. It is a
build artifact, not something the laptop serves: the hosted app maps no `/openapi` route.
`GastronomyApp.Api` stays a class library, and `GastronomyApp.Api.OpenApiHost` exists only to give
the document generator an entry point; it is never published.

## Phones and tablets

`frontend/src/shared/api/generatedSchemas.ts` and its `generatedSchemas.types.d.ts` sidecar hold
every request, response and SignalR event shape. Nothing edits them by hand. The steps of a change
are on the `change-a-wire-contract` page of `how-to`.

## What the document does not carry

Route paths, hub event names and message keys are plain strings on both sides. Tests read the
backend's source and assert the frontend declares the same set, so the check holds without anyone
remembering it.

## Tests

`GastronomyApp.Api.Tests/OpenApi/GeneratedOpenApiDocumentTest`;
`frontend/tests/shared/apiRouteVocabulary.spec.ts`, `hubEventVocabulary.spec.ts` and
`i18nCoverage.spec.ts`.
