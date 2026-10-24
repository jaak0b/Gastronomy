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
`snap-guide` on every guide line. A child our code does not name, such as the `Rect` and `Text` inside
a shape or the transformer's anchors, is found by the class or name Konva gives it:
`group.findOne('Text')`, `stage.findOne('Transformer')`, the anchor `.bottom-right`.

Wrap the `KonvaStage` in an element of our own with a `data-test`, because vue-konva passes only `id`,
`class` and `style` to the stage's own element. The editor wraps it in
`data-test="table-plan-drawing"`.

## The helpers: `frontend/tests/support/konvaDrawing.ts`

- `drawingIn(selector)` returns the `Konva.Stage` drawn inside that element.
- `shapeWithId(stage, id)`, `hasShapeWithId(stage, id)` and `shapesNamed(stage, name)` find shapes.
  Read a shape's values from the node: `x()`, `y()`, `width()`, `height()`, `rotation()`, and
  `findOne<Konva.Text>('Text')?.text()` for its lettering.
- `centreOf(node)` is the centre of a node in stage pixels, `anchorOf(stage, anchor)` the centre of a
  transformer anchor.
- `tapAt(stage, point)` presses and releases on the stage, which Konva turns into `click`.
- `dragBy(stage, node, by)` drags a node from its centre; `dragAnchorBy(stage, anchor, by)` drags a
  transformer anchor, which is a resize. Both first move 10 pixels, past the editor's
  `dragDistance`, then to the target, then release.
- `pressAt`, `movePointerTo` and `releaseAt` are the three steps on their own, for a spec that looks
  at the drawing in the middle of a drag, such as the guide lines.

Every helper that presses calls `stage.draw()` first, so the hit canvas shows what the last change
drew, and every step ends with `flushPromises()`. Coordinates are stage pixels: when the stage is
scaled they are drawing units times the scale. In jsdom every element measures 0 pixels, so the
editor draws at scale 1 and a pixel is a drawing unit. A spec that needs another scale or a visible
height stubs the measurement, as `layOutTheScreen({ width, height })` in the editor fixture does with
`vi.spyOn(Element.prototype, 'clientWidth', 'get')`, and calls `vi.restoreAllMocks()` in
`afterEach`.

## What to assert

- A save: the body the laptop received (`laptop.writtenBodies()`), as a literal, and equal to the
  node's own values, which proves nothing rounded or clamped the value on the way.
- A refusal: the sentence on screen and the node back at the stored values.
- A colour: the shape's `fill()` or `stroke()` against the literal from `shared/theme.ts`. A spec that
  switches the theme mounts with `createVuetify({ theme: appTheme })` as an extra plugin and calls
  `vuetify.theme.change('dark')`.

Wire data for the plan comes from `tests/support/wireViews.ts` (`aGeometry`, `anAdminTable`,
`anAdminStationShape`, `anAdminArea`).

## Example

`frontend/tests/admin/components/tablePlan/TablePlanEditor.drawing.spec.ts` and
`TablePlanEditor.resizing.spec.ts` with `tablePlanEditorFixture.ts` beside them (`editorLaptop`,
`mountEditor`, `theDrawing`, `selectTheShape`, `layOutTheScreen`, `drawingScrollArea`).

Run one file from `frontend/`: `npx vitest run tests/admin/components/tablePlan/TablePlanEditor.drawing.spec.ts`.
