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
2. **The mapping.** An entity becomes a view through Mapster in `backend/GastronomyApp.Api/Mapping`
   (`IMappingRegistration`, `config.NewConfig<Entity, View>()`). `MappingConfiguration` compiles with
   `RequireDestinationMemberSource = true`, so a view member without a source fails
   `MappingConfigurationTest`. A new mapping class is also registered in `ApiServiceRegistration`
   (`MappingConfigurationTest` checks that too).
3. **The route.** Map it in `backend/GastronomyApp.Api/Endpoints` with `group.MapGet`, `MapPost`, `MapPut` or `MapDelete`;
   `frontend/tests/shared/apiRouteVocabulary.spec.ts` reads those calls to prove that every `/api/...`
   address the frontend uses exists. The handler's return type (`ApiAnswer<T>`, `CreatedAnswer<T>`,
   `NoContentAnswer`) is the status and the response type in the document. Hub event payloads are
   records in `Contracts/Events`; `HubEventSchemaTransformer` adds each to the document.
4. **Regenerate the document.** Build the Api test project: `dotnet build backend/GastronomyApp.Api.Tests`.
   It references `GastronomyApp.Api.OpenApiHost`, whose build rewrites
   `backend/GastronomyApp.Api.OpenApiHost/openapi/GastronomyApp.Api.OpenApiHost.json`. That file is
   committed, so it belongs in the same change.
5. **Regenerate the frontend.** `npm run generate:api` in `frontend/` rewrites
   `src/shared/api/generatedSchemas.ts` and `generatedSchemas.types.d.ts`. Never edit them by hand;
   when the output is wrong, fix the record, the document transformer or the attribute that produced it.
6. **Fix what the new schema rejects.** The object schemas are `z.strictObject`, so an answer with a
   field the schema does not name, or without one it does, becomes `unreadableAnswer` and fails the
   screen. Update the factories in `frontend/tests/support/wireViews.ts` first, then any spec or
   fixture that spells the shape out; `npx vue-tsc -b` lists them, because `tsconfig.tests.json`
   includes `tests/**/*.ts`. Then change the store and the components.
7. **Texts.** A new message key needs German and English wording in the same change; see `add-a-refusal`.

## Tests

Backend: the endpoint test in `GastronomyApp.Api.Tests/Endpoints` for the route; the mapping test when
a view member needs a source of its own (`GastronomyApp.Api.Tests/Mapping`);
`OpenApi/GeneratedOpenApiDocumentTest` only when a new kind of schema needs a rule, since it already
pins numbers, enums and event schemas. Frontend: the store spec for the new call and the specs of
the screens that show it. Run single files, for example
`dotnet test backend/GastronomyApp.Api.Tests --filter "FullyQualifiedName~AdminIngredientEndpointsTest"`
and `npx vitest run tests/admin/stores/ingredients.spec.ts`.

## When the work is split

A change shared by several agents, or by the backend and the frontend, starts from the written
contract of root rule 17: every route with its request and response, field names and types, status
codes, message keys with their German and English text, and event names. It is copied into every
prompt, nobody renames anything in it, and an agent that finds it cannot work stops and reports.
