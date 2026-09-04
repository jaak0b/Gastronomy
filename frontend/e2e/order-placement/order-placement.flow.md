# Flow: a server enrols a phone, builds an order, and sends it

This is the one required webtest. It is written before the test code and the test code is a
mechanical transcription of it. It drives the real UI only: it never imports a store, never seeds
`localStorage`, and never calls the API directly.

## Which backend task must land before the skip is lifted

The spec is skipped while no backend is reachable. Lifting the skip needs all of T002 (core domain),
T003 (infrastructure), T004 (printing) and T005 (API), because the journey below touches enrolment,
catalog, order submission, ticket creation and the printer status banner. T005 is the last of the
four to land, so **T005 is the task that lifts this skip**, by setting `GASTRONOMY_E2E_BASE_URL` to a
running backend serving the built frontend.

## Preconditions the run needs

1. The backend is running and serving the built frontend from `wwwroot`.
2. A practice event session is running, per spec 11.3, so the run leaves no orders in the takings.
3. One production location named `Küche`, on the test printer transport.
4. One catalog item named `Bratwurst` at `3,50 €`, assigned to `Küche` only, available.
5. An outstanding enrolment invitation whose six digit code is `GASTRONOMY_E2E_ENROL_CODE`.
6. The phone's language is German, which is the default for a new device.

## The journey, step by step, with the literal values expected

| # | The server does | The screen shows, literally |
|---|---|---|
| 1 | Opens the app root with no token | `Einrichten mit dem sechsstelligen Code` as the heading |
| 2 | Reads the code off the laptop and types it, then types the name `Anna` | The continue button `Weiter` becomes enabled |
| 3 | Taps `Weiter` | The catalog, heading `Bestellung aufnehmen` |
| 4 | Reads the basket bar before touching anything | `Noch nichts ausgewählt` |
| 5 | Taps `Bratwurst` once | The item button carries the count `1` |
| 6 | Taps `Bratwurst` a second time | The item button carries the count `2` |
| 7 | Reads the basket bar | `2 Artikel, 7,00 €` |
| 8 | Taps `Weiter zur Übersicht` | The review screen, heading `Bestellung prüfen` |
| 9 | Reads the station grouping | `Geht an Küche`, because Bratwurst has exactly one candidate station and was never asked about |
| 10 | Reads the total | `7,00 €` |
| 11 | Reads under the send button before typing a table | `Tragen Sie einen Tisch ein, bevor Sie senden.` and the send button is disabled |
| 12 | Types `Tisch 12` into the table field | The send button `Bestellung senden` becomes enabled |
| 13 | Taps `Bestellung senden` | `Bestellung {number} ist angekommen.` where `{number}` matches `^\d+$`, no `#` and no `Nr.` |
| 14 | Returns to the catalog and reads the basket bar | `Noch nichts ausgewählt`, because an accepted order clears the draft |

## What this test is not

It is one full journey, not a set of micro assertions on individual buttons. It does not test the
failure paths: those are unit tested in `tests/core/sendFailure.spec.ts` and component tested in
`tests/components/review/SendFailurePanel.spec.ts`, because reproducing a lost response against a
live backend is not something a webtest can do honestly.
