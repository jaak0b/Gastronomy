# Change what the API sends or receives

The wire shapes have one source: the records in `GastronomyApp.Contracts`. The OpenAPI document
the build writes carries them, and the frontend's types and zod schemas are generated from that
document, so a changed record reaches the frontend as a changed type and schema once the steps below
are followed in order.

## Steps

1. **The record.** Edit or add it under `backend/GastronomyApp.Contracts/<Area>`. A response is a
   positional `sealed record`; a request is a `sealed record` with `required` init properties and
   DataAnnotations attributes whose `ErrorMessage` is a constant from
   `Validation/RefusalMessageKeys.cs`. Request shape is checked there and nowhere else; domain rules
   stay in `GastronomyApp.Core`.
2. **The service.** The record is produced and consumed by the area's service in `GastronomyApp.Core`, not
   by a mapping file in `Api`. A read projects into the record with Mapster
   `ProjectToType<View>()`; a view member that Mapster cannot fill from the entity is configured once
   for that record, and a copy by hand is the exception. A write takes the request record.
   The steps are on the `add-a-read-to-a-service` and `add-a-write-to-a-service` pages.
3. **The route.** Map it in `backend/GastronomyApp.Api/Endpoints` with `group.MapGet`, `MapPost`, `MapPut` or `MapDelete`, calling the area's service directly;
   `frontend/tests/shared/apiRouteVocabulary.spec.ts` reads those calls to prove that every `/api/...`
   address the frontend uses exists, both the literal addresses and the routes the admin list layers
   build from their list's `path` through `ROUTES_OF_EACH_ADMIN_LIST_LAYER` (see
   `admin-list-screen.md`), each with the method it is called with. The endpoint's return type (`ApiAnswer<T>`, `CreatedAnswer<T>`,
   `NoContentAnswer`) is the status and the response type in the document. The device a request came
   from is an endpoint parameter, never something an endpoint looks up: `StaffDeviceCaller` and
   `StationDeviceCaller` bind themselves through `BindAsync`. Hub event payloads are records in
   `Contracts/Events`.
4. **Regenerate the document.** Build the Api test project: `dotnet build backend/GastronomyApp.Api.Tests`.
   It references `GastronomyApp.Api.OpenApiHost`, whose build rewrites
   `backend/GastronomyApp.Api.OpenApiHost/openapi/GastronomyApp.Api.OpenApiHost.json`. That file is
   committed, so it belongs in the same change. The host project exists only to give
   `Microsoft.Extensions.ApiDescription.Server` an entry point (`DocumentGenerationEntryPoint.cs`)
   that builds the web application; only the Api tests reference it, for build order. The two
   transformers in `GastronomyApp.Api/DocumentTransformers` add the hub event payloads as component
   schemas (`HubEventSchemaTransformer`) and describe numbers as numbers
   (`StringEncodedNumberSchemaTransformer`).
5. **Regenerate the frontend.** `npm run generate:api` in `frontend/` runs `typed-openapi` over that
   document and rewrites `src/shared/api/generatedSchemas.ts` and its `generatedSchemas.types.d.ts`
   sidecar: every request, response and SignalR event shape as a zod schema with the TypeScript type
   beside it, under the backend's own contract names. `npm run build` runs it before `vue-tsc`. Never
   edit them by hand; when the output is wrong, fix the record, the document transformer or the
   attribute that produced it.
6. **Fix what the new schema rejects.** The object schemas are `z.strictObject`, so an answer with a
   field the schema does not name, or without one it does, becomes `unreadableAnswer` and fails the
   screen. Update the factories in `frontend/tests/support/wireViews.ts` first, then any spec or
   fixture that spells the shape out; `npx vue-tsc -b` lists them, because `tsconfig.tests.json`
   includes `tests/**/*.ts`. Then change the store and the components.
7. **Texts.** A new message key needs German and English wording in the same change; see `add-a-refusal`.

## Tests

Backend: the endpoint test in `GastronomyApp.Api.Tests/Endpoints` for the route; the test of the use
case for the projection and the rules;
`OpenApi/GeneratedOpenApiDocumentTest` only when a new kind of schema needs a rule, since it already
pins numbers, enums and event schemas. Frontend: the store spec for the new call and the specs of
the screens that show it. Run single files, for example
`dotnet test backend/GastronomyApp.Api.Tests --filter "FullyQualifiedName~AdminIngredientEndpointsTest"`
and `npx vitest run tests/admin/stores/ingredients.spec.ts`.

## When the work is split

A change shared by several agents, or by the backend and the frontend, follows root rule 17. The
backend is built first, and the generated types carry every request and response shape, every field
name and type and every enum value. The written part covers only what the generated document does
not carry: the route paths, the refusal message keys with their German and English texts, the hub
event names, and the few rules both sides rely on. It is copied into every prompt, nobody renames
anything in it, and an agent that finds it cannot work stops and reports.
