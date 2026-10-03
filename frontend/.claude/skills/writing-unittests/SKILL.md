---
name: writing-unittests
description: Use when adding or changing a Vitest unit test under tests/**, when reviewing unit tests, or when a code change needs a unit test that would actually catch a real bug, before committing the change.
---

# Writing unit tests

The frontend has one test tier. Vitest specs under `tests/` are the correctness net for whoever
touches the code next: the phone's order flow, the station board, the admin screens and the plain
TypeScript in every `core/` folder. This skill says how such a test is written. It follows Yoni
Goldberg's JavaScript testing best practices where they apply to a Vue app that talks to a stubbed
laptop, and it keeps the rules this project already learned the hard way.

## The golden rule

A test is not production code. It is short, flat and boring, and a reader understands it in one
pass without opening another file. Read top to bottom, it tells a small story: a waiter has a
table with two open items, taps the amount button, types 2,00 and a reason, and the laptop is sent
exactly that.

That is why a test holds no logic. There is no `if`, no loop over outcomes, no `try`, and no
expected value worked out at run time. When a test needs a branch, it is two tests. A table of
cases with `it.each` is fine, because each row is still a plain statement.

## Expected values are literals

An expected value is never calculated inside the test: no formula, no conversion, and no call to
a production helper to build the expectation. Write the literal you worked out by hand, such as
`'3,50 €'` or `{ paidPriceCents: 200 }`. A test that recomputes its expectation with the code it
checks shares every bug of that code and can never catch one.

## Naming a test

A test name has three parts: the unit under test, the scenario, and the outcome. In this project
the unit lives in the `describe` text and the scenario and outcome in the `it` text, written as
behaviour a person can see, not as a method name. `describe('an order the laptop did not
confirm')` with `it('offers no way to take the sold-out line off, because the laptop may hold the
order as it was')` reads as one sentence and tells a reviewer what broke when it goes red.

Name a `describe` block after a situation, never after a function. "sending the order from the
review screen" and "a refusal the admin has walked away from" are scenarios; "send()" and
"handleRefusal" are not. Nest a second `describe` only when a scenario really splits in two.

## Arrange, act, assert

Every test has three visible blocks separated by a blank line: setting the scene, the one thing
that happens, and what is checked afterwards.

```ts
it('sends the smaller amount together with the typed reason', async () => {
  const laptop = openItemsLaptop()
  const screen = await mountScreen()
  await openTheAmountDialog(screen)

  await typeIn('[data-test="amount-field"]', '2,00')
  await typeIn('[data-test="reason-field"]', 'Stammgast')
  await pressConfirm()

  expect(laptop.writtenBodies()[0]).toEqual({ ... })
})
```

The act block is short. If it needs ten steps, most of them belong to the arrange block or to a
named scenario helper such as `reviewAfterAFailedSend(order)`.

## Test from the outside

Test what a person does and sees, and what the laptop is asked and answers. A component spec
mounts the component, taps and types through the DOM, and checks the rendered text and the
requests the stubbed laptop recorded. A store spec calls the store's public actions and reads its
public state. A `core/` spec calls the exported function.

Never reach into a component through `wrapper.vm`, never spy on a private function, and never
assert that one internal method called another. Do not drive a child component with
`findComponent(X).vm.$emit(...)`: open the dialog, fill it and press its button, which the
helpers in `tests/support/formDialogs.ts` do. Wait for a re-render with `nextTick()` from `vue`
or `flushPromises()`, not with `wrapper.vm.$nextTick()`.

The test should survive a refactor that keeps the behaviour. If renaming a private function or
splitting a component turns it red, it was a change detector, and a change detector trains people
to update expectations on sight.

Mock only the real boundary: the laptop (through the stubbed `fetch`), the SignalR hub (through
`tests/support/hubConnection.ts`) and the clock. Everything else runs for real, Pinia included.

## Data

Every test builds its own data. Use the factories in `tests/support/wireViews.ts` and override
only what the test is about: `anAdminItem({ name: 'Bier', atTheFestival: null })` says more than
twelve copied lines in which one field differs. Use realistic values from the festival, such as
Bratwurst, Tisch 4 and 3,50 €, not `foo` and `test1`, because a wrong German plural or a wrong
price format shows up only with real words and real numbers.

Never share mutable state between tests. A module-level array that tests push into, a `let`
reassigned halfway through a test to change what the laptop answers next, or a fixture one test
edits and the next one reads, all make the result depend on the order the tests run in. When the
laptop has to answer differently the second time, say so with `inTurn(firstReply, secondReply)`.

Constants that never change, like `const KITCHEN_ID = 'station-kueche'`, are fine at module level.

## One concern per test

A test checks one behaviour, usually with one to three assertions about the same outcome. When
one test checks the dialog closing, the request body and the notice text, split it; the name of
each part then says what failed. Several `expect` lines that all describe one result, such as the
request's url and its body, are still one concern.

## Errors are expected, not caught

A test that expects a failure says so with `await expect(promise).rejects.toThrow(...)` or
`expect(() => run()).toThrow(...)`. It never wraps the call in `try` and asserts in the `catch`,
because a call that suddenly stops failing then passes silently. A refusal that reaches the screen
is checked as the text the person reads.

## Waiting

Never sleep. There is no `setTimeout` in a test and no fixed wait. Await the observable outcome
with `waitUntil(() => expect(...))` from `tests/support/dom.ts` or `vi.waitFor`, or settle pending
promises with `flushPromises()`. Where the code itself waits, such as the ten second send timeout
or a debounced lookup, use fake timers and `vi.advanceTimersByTimeAsync` with the constant the
code exports.

## Finding elements

Find an element by its `data-test` attribute or by its visible text, nothing else. A template
carries `data-test="festival-item-row"`, and when a list holds several rows the row also carries
`data-test-id` with its id, so a test reads
`[data-test="festival-item-row"][data-test-id="item-bier"]`. Add the attribute to the template
when it is missing; that is the one change in `src/` a test may bring with it.

Never select by position (`[0]`, `.at(-1)`, `:nth-child`) or by a styling class. A class is there
for the look and changes with it. A few cases are fine and should be recognisable as such:
counting how many elements match, asserting the display order of all matches, indexing the
test's own data arrays, Vuetify internals that have nothing of ours to tag, and a state class read
as the assertion itself, such as `expect(row.classes()).toContain('tinted-row')`.

Never select by an accessibility attribute; the frontend has none.

## The support module

`tests/support/` is the one home for what more than one spec needs. A spec never declares its own
copy of anything listed here.

- `laptop.ts` stubs `fetch` with a readable laptop. `stubLaptop()` returns a `StubbedLaptop` that
  records every call (`calls`, `writes()`, `writtenBodies()`, `urls()`, `callsTo(method, path)`)
  and answers routes registered with `answers(method, path, reply)`, the latest registration
  winning, plus `answersEverythingElse(reply)`. Replies are built with `answer(body, status)`,
  `emptyAnswer(status)`, `refusal(messageKey, { status, code, parameters })`, `noConnection()`,
  `neverAnswers()`, `inTurn(...)` and `heldUntil(promise, reply)`. `stubLaptopAt(repliesByUrl)` and
  `stubLaptopAnswering(payloadFor)` cover the two common table shapes.
  `aHold()` gives a reply gate for `heldUntil`: pass its `released` promise and call `release()` when
  the test wants the laptop to answer.
- `dom.ts` finds and drives elements: `onScreen`, `allOnScreen`, `isOnScreen`, `textOnScreen`,
  `inputOf`, `typeInto`, `typeIn`, `leave`, `clickOn`, `waitUntil`, and the confirmation dialog helpers
  `openDialog`, `waitForDialog`, `dialogText` and `pressInDialog`.
- `formDialogs.ts` fills and saves or cancels the admin form dialogs.
- `wireViews.ts` holds the factories for the wire views used most: `anAdminItem`, `aFestival`,
  `anAdminStation`, `anOpenItem`, `anOpenItemsTable`, `aQueuedItem` and `aStationOrder`.
- `station.ts` holds the station tablet scenario: `KITCHEN`, `item`, `stationOrder`, `aQueue` and
  `enrolledStationTablet()`.
- `plugins.ts` gives `testPlugins(locale)` for mounting, `mountApp.ts` mounts the whole app,
  `hubConnection.ts` fakes the SignalR hub, and `sourceFiles.ts` lists source files for the specs
  that scan the source tree.

A spec may still have helpers of its own when they name a scenario of that spec, such as
`festivalLaptop(scenario)` or `reviewAfterAFailedSend(order)`. They are built on the support
module, never beside it. When a second spec needs the same helper, it moves into `tests/support/`.

## Size of a spec file

A spec file stays readable at one sitting. Once it grows past about 400 lines, split it by
scenario into files named for the scenario, such as `order.sending.spec.ts`, `order.retry.spec.ts`
and `FestivalPage.stock.spec.ts`. What the parts share (constants, the laptop scenario, the mount
helper) goes into a fixture module beside them, such as `festivalPageFixture.ts`, which is not a
spec and holds no tests. A test that breaks only because it moved to another file was depending
on another test, and that dependency is fixed in the test setup.

## Proving a test works

A bug fix starts with a test that goes red on the assertion it was written for, as the root
`CLAUDE.md` rule on test-first fixes describes. A test never seen red is unverified. When in doubt
about a test's strength, break the code on purpose, watch the test fail for the intended reason,
and undo the break.

Mutation testing is deliberately not adopted. Do not add Stryker, do not quote a mutation score,
and do not treat its absence as a gap.

## Running tests

Run only the spec files that cover the change, by path, with `npx vitest run <paths>`. Never the
whole suite and never a whole folder for comfort; the owner's machine is slow. Name the files you
ran and why they cover the change.

## Review checklist

Read each new or changed test with the implementation closed and check:

1. The name says unit, scenario and outcome in behaviour wording, and the `describe` names a
   situation.
2. Arrange, act and assert are visible blocks, and the act block is short.
3. The test holds no `if`, loop, `try` or calculated expectation.
4. It observes the screen, the store's public state or the laptop's recorded requests, never
   `wrapper.vm`, a spy on an internal function or a child's `$emit`.
5. Its data is built in the test or by a factory, and nothing it touches is shared with another
   test.
6. Elements are found by `data-test` or visible text only, apart from the listed exceptions.
7. It waits on an observable outcome, never a sleep.
8. It uses `tests/support/` instead of a private copy of a stub or a DOM helper.
9. Its file stays under about 400 lines, or the file is split by scenario.
10. For a bug fix, the red run was seen and quoted.
