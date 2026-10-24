# Write a frontend spec against a fake laptop

A spec mounts the real component with the real Pinia stores and the real locale files, and replaces
only `fetch`. The rules for what to test and how to name it are in the `writing-unittests` skill;
this page is the set of tools and the shape of a spec.

## The fake laptop: `frontend/tests/support/laptop.ts`

- `stubLaptop()` replaces the global `fetch` and returns a `StubbedLaptop`. Register answers with
  `answers(method, match, reply)` and a default with `answersEverythingElse(reply)`. `match` is a
  path, a `RegExp` or a function of the call. A path also matches everything below it
  (`/api/admin/items` answers `/api/admin/items/x/ingredients/y`), and the latest registration wins.
  A call with no answer still lands in `calls`, but the stubbed `fetch` rejects it with a `TypeError`.
  The two places in the app that call `fetch` (`shared/api/client.ts` and
  `admin/api/invitationQrRequests.ts`) turn every rejection into `unreachable`, so the screen shows a
  lost connection and the spec does not fail by itself. A forgotten answer is therefore found only by
  a spec that checks for it: register an answer for every request the scenario makes, give the rest a
  defined reply with `answersEverythingElse`, and assert on `calls` or `urls()` that the expected
  requests were sent.
- It records every request in `calls` and offers `writes()` (every call that is not a GET),
  `writtenBodies()`, `callsTo(method, match)`, `urls()` and `forgetCalls()`. Assert on these, for
  example `expect(laptop.writes()).toEqual([{ url, method, body }])`.
- Replies: `answer(body, status = 200)`, `emptyAnswer(status)`,
  `refusal(messageKey, { status = 409, code, parameters })` for a refusal with the laptop's error body,
  `noConnection()`, `neverAnswers()`, `inTurn(first, second)` for a laptop that answers differently the
  second time, and `heldUntil(promise, reply)` with `aHold()` (`released`, `release()`) to answer when
  the spec says so. `stubLaptopAt(repliesByUrl)` and `stubLaptopAnswering(payloadFor)` cover the two
  common table shapes.
- Call `vi.unstubAllGlobals()` in `afterEach`.

## Wire data: `tests/support/wireViews.ts`

Factories take overrides and return a full, schema valid view: `anAdminItem`, `anAdminItemIngredient`
(one recipe line of an article), `anAdminIngredient`,
`anAdminFestivalIngredient` (one stock row of the festival page, with its usage and forecasts),
`aFestival`, `anAdminStation`, `anOpenItem`, `anOpenItemsTable`, `aQueuedItem`, `aStationOrder`. The
generated schemas are strict, so an answer must hold every field; build it with the factory and
override only what the spec is about, `anAdminIngredient({ name: 'Senf', unit: 'gram' })`. A shape that
changes on the wire is fixed in the factory once.

## Mounting and finding elements

- `tests/setup.ts` installs Vuetify for every spec; `testPlugins('de' | 'en')` gives the locale. A
  component spec calls `setActivePinia(createPinia())` and empties `document.body` in `beforeEach`,
  and mounts with `attachTo: document.body` because Vuetify dialogs render outside the component.
  A spec that mounts a screen calls `enableAutoUnmount(afterEach)` at the top of the file, because a
  screen left mounted keeps its timers running into the next test, and a store action such a timer
  starts makes the earlier test's Pinia active again, so `useXStore()` returns the old store.
- `tests/support/dom.ts` finds and drives the page: `onScreen`, `allOnScreen`, `isOnScreen`,
  `textOnScreen`, `inputOf`, `typeIn`, `clickOn`, `waitUntil`, and `testIdSelector(testId, id)`.
  `formDialogs.ts` fills and saves the admin form dialogs, and `hubConnection.ts` fakes SignalR
  (`fireHubEvent`) for a spec that proves a store reloads on an event.
- The connection store reloads every store only when the laptop sends `GroupsJoined`, never when
  `connect()` resolves. The fake sends `GroupsJoined` to the connection that started, right after
  its `start` resolves, so a store registered before `connect()` begins its first load then; a spec
  that reads what that load returned waits for it with `vi.waitFor` or `waitUntil`. The connection
  store keeps its own `GroupsJoined` handler on the hub, so a spec that checks which handlers are
  left reads `hubEventsOtherThanGroupsJoined()` instead of `hubEventsRegistered`.
- `holdBackGroupsJoined()` stops that automatic delivery for the current test, and
  `theLaptopSendsGroupsJoined()` sends it when the spec says so. `theWifiComesBack()` and
  `theConnectionCloses()` run the `onreconnected` and `onclose` callbacks of the connection that
  started last, and `forgetHubEvents()` resets all of these.
- Select by `data-test`, and by `data-test-id` when a list holds several rows:
  `[data-test="ingredient-row"][data-test-id="ingredient-senf"]`. Add the attribute to the template when
  it is missing. Never select by position or by a class.

## Shape of a spec set

Put the laptop scenario and the mount helper in a fixture module beside the specs
(`tests/admin/components/ingredients/ingredientsListFixture.ts` gives `ingredientsListLaptop()` and
`mountList()`), and split specs by scenario once a file nears 400 lines. A store spec needs no
component: call the store's actions against `stubLaptop()`.

## Running

From `frontend/`, one file at a time: `npx vitest run tests/admin/components/ingredients/IngredientsList.editing.spec.ts`.
Never the whole suite and never a folder; name the files you ran and why they cover the change.
