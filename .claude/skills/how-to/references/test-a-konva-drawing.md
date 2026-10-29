# Test a Konva drawing

A spec draws for real: jsdom hands every `<canvas>` to the `canvas` package (node-canvas, a
devDependency), so Konva fills its scene and hit canvases and a pointer event lands on the shape
under it. The spec mounts the real component on the fake laptop of
[frontend-test-with-the-laptop.md](frontend-test-with-the-laptop.md) and acts through the pointer,
never by calling a handler of the component.

## Name what the test acts on

Our code gives every shape it draws a Konva `id` or `name`, and a test finds the shape by it (frontend
rule on tests). The table plan editor sets `id` to `table-<tableId>` or `station-<stationId>`
(`shapeNodeIdOf` in `admin/core/tablePlanSelection.ts`), `name` to `plan-shape` on every shape and
`snap-guide` on every guide line. The selection marquee is `name` `marquee` on its dashed border and
`marquee-fill` on its translucent fill. The waiter's plan (`phone/components/tablePlan/AreaPlan.vue`) sets
`id` to `table-<tableId>` and `name` to `plan-table` on every table, and `name` to `plan-station` on
every station. A child our code does not name, such as the `Rect` and `Text` inside
a shape or the transformer's anchors, is found by the class or name Konva gives it:
`group.findOne('Text')`, `stage.findOne('Transformer')`, the anchor `.bottom-right`.

Wrap the `KonvaStage` in an element of our own with a `data-test`, because vue-konva passes only `id`,
`class` and `style` to the stage's own element. The editor wraps it in
`data-test="table-plan-drawing"`, the waiter's plan in `data-test="area-plan"`.

## The helpers: `frontend/tests/support/konvaDrawing.ts`

- `drawingIn(selector)` returns the `Konva.Stage` drawn inside that element.
- `shapeWithId(stage, id)`, `hasShapeWithId(stage, id)` and `shapesNamed(stage, name)` find shapes.
  Read a shape's values from the node: `x()`, `y()`, `width()`, `height()`, `rotation()`, and
  `findOne<Konva.Text>('Text')?.text()` for its lettering.
- `centreOf(node)` is the centre of a node in stage pixels, `anchorOf(stage, anchor)` the centre of a
  transformer anchor.
- `colourAt(stage, point)` is the colour one pixel of the drawn stage shows, in stage pixels, as
  `[red, green, blue, alpha]`, so a spec can compare it with a colour written out as four numbers.
- `tapAt(stage, point)` presses and releases on the stage, which Konva turns into `click`. Its third
  argument takes `{ ctrlKey, shiftKey }` for a modifier click, as a table multi-selection needs.
- `rightClickAt(stage, point)` sends what a browser sends for a right click: `mousedown`, `contextmenu`
  and `mouseup` with button 2, so Konva also makes its own click from the release. A context menu
  whose selection must survive the right click needs that whole sequence, not only the `contextmenu`
  event.
- `dragBy(stage, node, by)` drags a node from its centre; `dragAnchorBy(stage, anchor, by)` drags a
  transformer anchor, which is a resize. Both first move 10 pixels, past the editor's
  `dragDistance`, then to the target, then release.
- `pressAt`, `movePointerTo` and `releaseAt` are the three steps on their own, for a spec that looks
  at the drawing in the middle of a drag, such as the guide lines. `pressAt` takes the same modifiers
  as `tapAt`. `movePointerTo` and `releaseAt` send to the window, which serves the editor's snapping
  and its marquee; `releaseAt` lets go on the window, so Konva never sees a click; and
  `releaseOnTheDrawingAt(stage, point)` lets go on the drawing itself, for a spec that proves a drag
  does not end in a tap, as the waiter's pan does.
- `layOutTheScreen({ width, height })` gives every element that size, as the next paragraph says.
- Touch: a `Finger` is an `id` and a point. `putFingerDown(stage, finger, alreadyDown)`,
  `moveFingers(stage, fingers)` and `liftFinger(stage, finger, stillDown)` send `touchstart`,
  `touchmove` and `touchend` with the fingers that touch, which Konva turns into `tap`, a pan and,
  with two fingers, the plan's pinch. Use them for anything a finger decides, since Konva handles
  each finger's tap on its own.
- `tapAsAPhoneDoes(stage, finger)` sends what a phone's browser sends for one quick tap: pointer and
  touch events, a pixel of slip, and then, unless the page cancelled the `touchend`, `mousedown`,
  `mouseup` and `click` at that spot, delivered to what lies on top there by then (the scrim of an
  open Vuetify overlay, otherwise the drawing). `clickAsAMouseDoes(stage, point)` sends the laptop's
  mouse click: pointer and mouse events and the native click on the drawing. Use these for anything
  that must survive the click a browser sends behind a tap, such as a sheet the tap opens.

A spec that checks a Vuetify overlay against that click mounts with Vue's real transitions
(`stubs: { transition: false }`, as `mountThePlan` does): Vuetify recognises a click on its scrim
by the scrim's own element, and a stubbed transition puts its stub in that place, so the overlay
would ignore the click. Vuetify closes on such a click in a `setTimeout`, so the spec fakes only
`setTimeout` (`vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })`, leaving
`flushPromises` working) and runs it with `vi.runOnlyPendingTimersAsync()` before it asserts.

Every helper that presses calls `stage.draw()` first, so the hit canvas shows what the last change
drew, and every step ends with `flushPromises()`. Coordinates are stage pixels: when the stage is
scaled they are drawing units times the scale. The admin's editor always draws at its fixed scale
of 1.5, in jsdom too, with its drawing shifted 4 units (6 pixels) right and down for the margin, so
a move of 33 units is a pointer move of 49.5 pixels and a shape at 300 units sits at 456 pixels. In jsdom every element measures 0 pixels; a spec that needs a visible height, or
a window of a certain width, stubs the measurement with `layOutTheScreen({ width, height })`, which
spies on `Element.prototype`'s `clientWidth` and `clientHeight`, and calls `vi.restoreAllMocks()` in
`afterEach`. The waiter's plan falls back to the size of the drawing at scale 1 when it measures 0
pixels. Only the part of the stage inside its height can be hit, so a spec taps and drags shapes
inside the laid-out screen.

## What to assert

- A save: the body the laptop received (`laptop.writtenBodies()`), as a literal, and equal to the
  node's own values, which proves nothing rounded or clamped the value on the way.
- A refusal: the sentence on screen and the node back at the stored values.
- A Konva node made outside the drawing, such as the text that measures the words of a table name:
  spy on its class's `destroy`, unmount the screen, and count the destroyed nodes of that kind
  (`TablePlanEditor.drawing.spec.ts` counts the texts without words).
- Whether a shape's border reaches outside the rectangle it was given: `getClientRect({ skipTransform: true })`
  of its group includes the stroke, so a border that sticks out makes it larger than that rectangle.
  It cannot show a border drawn too far in when something else in the group, such as the name's
  `Text`, already spans the whole rectangle, and it ignores the group's rotation, so it says nothing
  about a rotated shape.
- What a person sees where two shapes touch, which also catches a border drawn too far in or covered
  by a neighbour: `colourAt(stage, point)` at a column or row inside each border, compared with the
  colour as four numbers (`TablePlan.drawing.spec.ts`, "the frame of a table on the plan").
- A colour: the shape's `fill()` or `stroke()` against the literal from `shared/theme.ts`. A spec that
  switches the theme mounts with `createVuetify({ theme: appTheme })` as an extra plugin and calls
  `vuetify.theme.change('dark')`.

Wire data for the plan comes from `tests/support/wireViews.ts` (`aGeometry`, `anAdminTable`,
`anAdminStationShape`, `anAdminArea`).

## Example

`frontend/tests/admin/components/tablePlan/TablePlanEditor.drawing.spec.ts` and
`TablePlanEditor.resizing.spec.ts` with `tablePlanEditorFixture.ts` beside them (`editorLaptop`,
`mountEditor`, `theDrawing`, `selectTheShape`, `shapesWithTheHandles`, `tableRow`, `stationRow`,
`drawingScrollArea`; `editorLaptop({ festival })` sets whether the festival runs), the selection and
group drag in `TablePlanEditor.selection.spec.ts`, the copy, paste and context menu in
`TablePlanEditor.copying.spec.ts`, and the waiter's plan in
`frontend/tests/phone/views/TablePlan.drawing.spec.ts` and `TablePlan.touch.spec.ts` with
`tablePlanFixture.ts` (`planLaptop`, `mountThePlan`, `theDrawing`, `tableNode`, `tapTheTable`,
`aPinchThatSpreadsTwoFingersOnTisch12`). A name a narrow shape draws is read as its lines from the
Konva text node: `findOne<Konva.Text>('Text')?.textArr.map((line) => line.text)`.

Run one file from `frontend/`: `npx vitest run tests/admin/components/tablePlan/TablePlanEditor.drawing.spec.ts`.
