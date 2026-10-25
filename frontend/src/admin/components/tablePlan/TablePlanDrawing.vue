<script setup lang="ts">
import Konva from 'konva'
import type { KonvaEventObject } from 'konva/lib/Node'
import type { Box } from 'konva/lib/shapes/Transformer'
import type { Vector2d } from 'konva/lib/types'
import { computed, nextTick, onMounted, onUnmounted, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  Group as KonvaGroup,
  Layer as KonvaLayer,
  Line as KonvaLine,
  Rect as KonvaRect,
  Stage as KonvaStage,
  Text as KonvaText,
  Transformer as KonvaTransformer,
  type VueKonvaRef,
} from 'vue-konva'
import type { AdminAreaView, AreaShapeGeometry } from '../../../shared/api/generatedSchemas'
import { useShapeNameWordWidth } from '../../../shared/composables/useShapeNameWordWidth'
import { useThemedValue } from '../../../shared/composables/useThemedValue'
import { assertNever } from '../../../shared/core/assertNever'
import { DARK_TABLE_PLAN_COLOURS, LIGHT_TABLE_PLAN_COLOURS } from '../../../shared/core/tablePlanColours'
import { LARGEST_NAME_FONT_SIZE, nameFontSizeKeepingWordsWhole } from '../../../shared/core/shapeNameFit'
import { TABLE_PLAN_DRAWING_WIDTH, type PlanPoint } from '../../../shared/core/tablePlanDrawing'
import type { AdminActionResult } from '../../core/adminActionResult'
import { drawnTablesOf, editorDrawingLength, fitsTheDrawing } from '../../core/tablePlanPlacement'
import {
  isTableSelected,
  nodeIdOfStation,
  nodeIdOfTable,
  selectionOfTables,
  selectedTableIds,
  shapeNodeIdOf,
  tableIdsInMarquee,
  withTableToggled,
  type PlanShape,
  type TablePlanSelection,
} from '../../core/tablePlanSelection'
import {
  snapDraggedBox,
  snapResizeCorner,
  snapStopsBeside,
  type PlanBox,
  type SnapGuide,
  type SnapStops,
} from '../../core/tablePlanSnapping'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import { useAdminTablePlanStore } from '../../stores/tablePlan'

const DRAWING_SCALE = 1.5

const MARGIN_AROUND_THE_DRAWING = 4

const PLAN_SHAPE_NAME = 'plan-shape'

const SNAP_GUIDE_NAME = 'snap-guide'

const NAME_PADDING = 4

const PIXELS_BEFORE_A_DRAG_STARTS = 3

const SCROLL_MARGIN_PIXELS = 24

const MARQUEE_FILL_NAME = 'marquee-fill'

const MARQUEE_NAME = 'marquee'

interface ShapeOnThePlan {
  shape: PlanShape
  geometry: AreaShapeGeometry
  saveGeometry: (geometry: AreaShapeGeometry) => Promise<AdminActionResult<null>>
}

interface DrawnShape {
  key: string
  shape: PlanShape
  geometry: AreaShapeGeometry
  group: Konva.GroupConfig
  rect: Konva.RectConfig
  text: Konva.TextConfig
}

interface GroupDrag {
  draggedTableId: string
  tableIds: readonly string[]
  startGeometry: ReadonlyMap<string, AreaShapeGeometry>
}

interface ContextMenuState {
  at: [number, number]
  drawingPoint: PlanPoint
  on: 'table' | 'empty'
}

interface MarqueeStart {
  from: PlanPoint
  adds: boolean
}

const props = defineProps<{ festivalId: string; area: AdminAreaView }>()
const selection = defineModel<TablePlanSelection | null>('selection', { required: true })

const { t } = useI18n()
const tablePlan = useAdminTablePlanStore()
const colours = useThemedValue(LIGHT_TABLE_PLAN_COLOURS, DARK_TABLE_PLAN_COLOURS)
const { refusal, refusalText, showRefusalOf } = useRefusalDisplay()
const stage = useTemplateRef<VueKonvaRef<Konva.Stage>>('stage')
const scrollArea = useTemplateRef<HTMLElement>('scrollArea')
const layer = useTemplateRef<VueKonvaRef<Konva.Layer>>('layer')
const transformer = useTemplateRef<VueKonvaRef<Konva.Transformer>>('transformer')
const visibleHeight = ref(0)
const guides = ref<SnapGuide[]>([])
const marqueeBox = ref<PlanBox | null>(null)
const contextMenu = ref<ContextMenuState | null>(null)
const contextMenuOpen = ref(false)
let groupDrag: GroupDrag | null = null
let marqueeStart: MarqueeStart | null = null
let resizeObserver: ResizeObserver | null = null

const widthOfANameWord = useShapeNameWordWidth()

const layerConfig = { x: MARGIN_AROUND_THE_DRAWING, y: MARGIN_AROUND_THE_DRAWING }

function storedGeometryOfTable(tableId: string): AreaShapeGeometry | undefined {
  return drawnTablesOf(props.area).find((candidate) => candidate.tableId === tableId)?.geometry
}
const drawnShapes = computed<DrawnShape[]>(() => {
  const palette = colours.value
  const nameText = {
    fontSize: LARGEST_NAME_FONT_SIZE,
    align: 'center',
    verticalAlign: 'middle',
    padding: NAME_PADDING,
    wrap: 'word',
    ellipsis: true,
    listening: false,
  }
  return [
    ...drawnTablesOf(props.area).map((table): DrawnShape => {
      const shape: ShapeOnThePlan = {
        shape: { kind: 'table', tableId: table.tableId },
        geometry: table.geometry,
        saveGeometry: (geometry) =>
          tablePlan.moveTables(props.festivalId, [{ tableId: table.tableId, geometry }]),
      }
      const selected = isTableSelected(selection.value, table.tableId)
      return {
        key: shapeNodeIdOf(shape.shape),
        shape: shape.shape,
        geometry: table.geometry,
        group: {
          id: shapeNodeIdOf(shape.shape),
          name: PLAN_SHAPE_NAME,
          ...table.geometry,
          draggable: true,
          dragDistance: PIXELS_BEFORE_A_DRAG_STARTS,
          ...listenersOf(shape),
        },
        rect: {
          width: table.geometry.width,
          height: table.geometry.height,
          fill: palette.table,
          stroke: selected ? palette.station : palette.ink,
          strokeWidth: selected ? 2 : 1,
          cornerRadius: 4,
        },
        text: {
          ...nameText,
          fontSize: nameFontSizeKeepingWordsWhole(
            table.name,
            table.geometry.width - 2 * NAME_PADDING,
            widthOfANameWord,
          ),
          width: table.geometry.width,
          height: table.geometry.height,
          text: table.name,
          fill: palette.ink,
        },
      }
    }),
    ...props.area.stationShapes.map((stationShape): DrawnShape => {
      const shape: ShapeOnThePlan = {
        shape: { kind: 'station', stationId: stationShape.stationId },
        geometry: stationShape.geometry,
        saveGeometry: (geometry) =>
          tablePlan.placeStation(props.festivalId, stationShape.stationId, {
            areaId: props.area.areaId,
            geometry,
          }),
      }
      return {
        key: shapeNodeIdOf(shape.shape),
        shape: shape.shape,
        geometry: stationShape.geometry,
        group: {
          id: shapeNodeIdOf(shape.shape),
          name: PLAN_SHAPE_NAME,
          ...stationShape.geometry,
          draggable: true,
          dragDistance: PIXELS_BEFORE_A_DRAG_STARTS,
          ...listenersOf(shape),
        },
        rect: {
          width: stationShape.geometry.width,
          height: stationShape.geometry.height,
          fill: palette.station,
          stroke: palette.station,
          strokeWidth: 1,
        },
        text: {
          ...nameText,
          width: stationShape.geometry.width,
          height: stationShape.geometry.height,
          text: stationShape.stationName,
          fill: palette.stationInk,
        },
      }
    }),
  ]
})

const drawingLength = computed(() =>
  editorDrawingLength(
    drawnShapes.value.map((shape) => shape.geometry),
    visibleHeight.value / DRAWING_SCALE,
  ),
)

const stageConfig = computed(() => ({
  width: (TABLE_PLAN_DRAWING_WIDTH + 2 * MARGIN_AROUND_THE_DRAWING) * DRAWING_SCALE,
  height: (drawingLength.value + 2 * MARGIN_AROUND_THE_DRAWING) * DRAWING_SCALE,
  scaleX: DRAWING_SCALE,
  scaleY: DRAWING_SCALE,
}))

const marqueeFillConfig = computed(() => ({
  name: MARQUEE_FILL_NAME,
  ...(marqueeBox.value ?? { x: 0, y: 0, width: 0, height: 0 }),
  fill: colours.value.station,
  opacity: 0.12,
  listening: false,
}))

const marqueeStrokeConfig = computed(() => ({
  name: MARQUEE_NAME,
  ...(marqueeBox.value ?? { x: 0, y: 0, width: 0, height: 0 }),
  stroke: colours.value.station,
  strokeWidth: 1,
  strokeScaleEnabled: false,
  dash: [4, 4],
  listening: false,
}))

const menuTarget = computed<[number, number]>(() =>
  contextMenu.value === null ? [0, 0] : contextMenu.value.at,
)

function guidePointsOf(guide: SnapGuide): number[] {
  switch (guide.orientation) {
    case 'vertical':
      return [guide.position, 0, guide.position, drawingLength.value]
    case 'horizontal':
      return [0, guide.position, TABLE_PLAN_DRAWING_WIDTH, guide.position]
    default:
      return assertNever(guide.orientation)
  }
}

const guideLines = computed(() =>
  guides.value.map((guide) => ({
    key: guide.orientation,
    config: {
      name: SNAP_GUIDE_NAME,
      points: guidePointsOf(guide),
      stroke: colours.value.ink,
      strokeWidth: 1,
      strokeScaleEnabled: false,
      dash: [4, 4],
      listening: false,
    },
  })),
)

function boxOf(node: Konva.Node): PlanBox {
  return node.getClientRect({ relativeTo: node.getLayer() ?? undefined, skipStroke: true })
}

function stopsBeside(node: Konva.Node, excluded: readonly Konva.Node[] = []): SnapStops {
  const otherShapes = layer.value?.getNode().find(`.${PLAN_SHAPE_NAME}`) ?? []
  return snapStopsBeside(
    otherShapes
      .filter((other) => other !== node && !excluded.includes(other))
      .map((other) => boxOf(other)),
  )
}

function selectedTableNodes(): Konva.Node[] {
  const layerNode = layer.value?.getNode()
  return selectedTableIds(selection.value)
    .map((tableId) => layerNode?.findOne(`#${nodeIdOfTable(tableId)}`))
    .filter((node): node is Konva.Node => node !== undefined)
}

function drawGeometryOn(group: Konva.Group, geometry: AreaShapeGeometry): void {
  group.setAttrs({ ...geometry, scaleX: 1, scaleY: 1 })
  for (const part of group.getChildren()) {
    part.setAttrs({ width: geometry.width, height: geometry.height })
  }
}

function geometryOf(node: Konva.Node): AreaShapeGeometry {
  return {
    x: node.x(),
    y: node.y(),
    width: node.width(),
    height: node.height(),
    rotation: node.rotation(),
  }
}

function boxThatFitsTheDrawing(oldBox: Box, newBox: Box): Box {
  const fits = fitsTheDrawing({
    x: newBox.x / DRAWING_SCALE - MARGIN_AROUND_THE_DRAWING,
    y: newBox.y / DRAWING_SCALE - MARGIN_AROUND_THE_DRAWING,
    width: newBox.width / DRAWING_SCALE,
    height: newBox.height / DRAWING_SCALE,
    rotation: (newBox.rotation * 180) / Math.PI,
  })
  return fits ? newBox : oldBox
}

function snapTheResizedCorner(previous: Vector2d, wanted: Vector2d): Vector2d {
  const resized = transformer.value?.getNode().nodes()[0]
  if (resized === undefined) {
    return previous
  }
  const snapped = snapResizeCorner(
    {
      x: wanted.x / DRAWING_SCALE - MARGIN_AROUND_THE_DRAWING,
      y: wanted.y / DRAWING_SCALE - MARGIN_AROUND_THE_DRAWING,
    },
    stopsBeside(resized),
  )
  guides.value = snapped.guides
  return {
    x: (snapped.x + MARGIN_AROUND_THE_DRAWING) * DRAWING_SCALE,
    y: (snapped.y + MARGIN_AROUND_THE_DRAWING) * DRAWING_SCALE,
  }
}

const transformerConfig = computed(() => ({
  rotateEnabled: false,
  flipEnabled: false,
  keepRatio: false,
  ignoreStroke: true,
  centeredScaling: false,
  anchorSize: 10,
  anchorStroke: colours.value.ink,
  anchorFill: colours.value.drawing,
  borderStroke: colours.value.ink,
  boundBoxFunc: boxThatFitsTheDrawing,
  anchorDragBoundFunc: snapTheResizedCorner,
}))

function chooseTheShape(shape: PlanShape, addsToSelection: boolean): void {
  if (shape.kind === 'station') {
    selection.value = { kind: 'station', stationId: shape.stationId }
    return
  }
  selection.value = addsToSelection
    ? withTableToggled(selection.value, shape.tableId)
    : selectionOfTables([shape.tableId])
}

function startDragging(shape: ShapeOnThePlan): void {
  refusal.value = null
  const current = selection.value
  if (
    shape.shape.kind === 'table'
    && current?.kind === 'tables'
    && current.tableIds.length > 1
    && current.tableIds.includes(shape.shape.tableId)
  ) {
    const startGeometry = new Map<string, AreaShapeGeometry>()
    for (const tableId of current.tableIds) {
      const geometry = storedGeometryOfTable(tableId)
      if (geometry !== undefined) {
        startGeometry.set(tableId, { ...geometry })
      }
    }
    groupDrag = {
      draggedTableId: shape.shape.tableId,
      tableIds: [...current.tableIds],
      startGeometry,
    }
    return
  }
  groupDrag = null
  const isAlreadySelected =
    shape.shape.kind === 'table'
      ? current?.kind === 'tables' && current.tableIds.length === 1 && current.tableIds[0] === shape.shape.tableId
      : current?.kind === 'station' && current.stationId === shape.shape.stationId
  if (!isAlreadySelected) {
    chooseTheShape(shape.shape, false)
  }
}

function startTransforming(shape: ShapeOnThePlan): void {
  refusal.value = null
  groupDrag = null
  chooseTheShape(shape.shape, false)
}

function snapWhileDragging(event: KonvaEventObject<DragEvent, Konva.Group>): void {
  const group = event.currentTarget
  const box = boxOf(group)
  const snapped = snapDraggedBox(box, stopsBeside(group))
  group.position({ x: group.x() - box.x + snapped.x, y: group.y() - box.y + snapped.y })
  guides.value = snapped.guides
}

function snapTheGroupWhileDragging(event: KonvaEventObject<DragEvent, Konva.Group>): void {
  const state = groupDrag
  if (state === null) {
    return
  }
  const group = event.currentTarget
  const draggedStart = state.startGeometry.get(state.draggedTableId)
  if (draggedStart === undefined) {
    return
  }
  const box = boxOf(group)
  const snapped = snapDraggedBox(box, stopsBeside(group, selectedTableNodes()))
  group.position({ x: group.x() - box.x + snapped.x, y: group.y() - box.y + snapped.y })
  const offsetX = group.x() - draggedStart.x
  const offsetY = group.y() - draggedStart.y
  for (const tableId of state.tableIds) {
    if (tableId === state.draggedTableId) {
      continue
    }
    const start = state.startGeometry.get(tableId)
    const node = layer.value?.getNode().findOne(`#${nodeIdOfTable(tableId)}`)
    if (start !== undefined && node !== undefined) {
      node.position({ x: start.x + offsetX, y: start.y + offsetY })
    }
  }
  guides.value = snapped.guides
}

function keepTheNameUnstretched(event: KonvaEventObject<Event, Konva.Group>): void {
  const group = event.currentTarget
  drawGeometryOn(group, {
    x: group.x(),
    y: group.y(),
    width: group.width() * group.scaleX(),
    height: group.height() * group.scaleY(),
    rotation: group.rotation(),
  })
}

async function saveTheNewGeometry(
  shape: ShapeOnThePlan,
  event: KonvaEventObject<Event, Konva.Group>,
): Promise<void> {
  guides.value = []
  const group = event.currentTarget
  const saved = await shape.saveGeometry(geometryOf(group))
  if (showRefusalOf(saved)) {
    drawGeometryOn(group, shape.geometry)
    transformer.value?.getNode().forceUpdate()
  }
}

function putTheTablesBack(startGeometry: ReadonlyMap<string, AreaShapeGeometry>): void {
  const layerNode = layer.value?.getNode()
  for (const [tableId, geometry] of startGeometry) {
    const node = layerNode?.findOne<Konva.Group>(`#${nodeIdOfTable(tableId)}`)
    if (node !== undefined) {
      drawGeometryOn(node, geometry)
    }
  }
  transformer.value?.getNode().forceUpdate()
}

async function saveTheMovedGroup(): Promise<void> {
  guides.value = []
  const state = groupDrag
  groupDrag = null
  if (state === null) {
    return
  }
  const layerNode = layer.value?.getNode()
  const moves = state.tableIds.flatMap((tableId) => {
    const node = layerNode?.findOne<Konva.Group>(`#${nodeIdOfTable(tableId)}`)
    return node === undefined ? [] : [{ tableId, geometry: geometryOf(node) }]
  })
  if (moves.length === 0) {
    return
  }
  const saved = await tablePlan.moveTables(props.festivalId, moves)
  if (showRefusalOf(saved)) {
    putTheTablesBack(state.startGeometry)
  }
}

function deselectOnAnEmptySpot(event: KonvaEventObject<MouseEvent>): void {
  if (event.target !== event.target.getStage()) {
    return
  }
  selection.value = null
}

function drawingPointOfClient(at: { clientX: number; clientY: number }): PlanPoint {
  const container = stage.value?.getNode().container()
  const frame = container?.getBoundingClientRect()
  return {
    x: (at.clientX - (frame?.left ?? 0)) / DRAWING_SCALE - MARGIN_AROUND_THE_DRAWING,
    y: (at.clientY - (frame?.top ?? 0)) / DRAWING_SCALE - MARGIN_AROUND_THE_DRAWING,
  }
}

function beginPressingTheDrawing(event: KonvaEventObject<MouseEvent>): void {
  if (event.evt.button !== 0 || event.target !== event.target.getStage()) {
    return
  }
  marqueeStart = {
    from: drawingPointOfClient(event.evt),
    adds: event.evt.ctrlKey || event.evt.shiftKey,
  }
  window.addEventListener('mousemove', stretchTheMarquee)
  window.addEventListener('mouseup', finishTheMarquee)
}

function stretchTheMarquee(event: MouseEvent): void {
  const start = marqueeStart
  if (start === null) {
    return
  }
  const pointer = drawingPointOfClient(event)
  const moved = Math.hypot(pointer.x - start.from.x, pointer.y - start.from.y) * DRAWING_SCALE
  if (moved < PIXELS_BEFORE_A_DRAG_STARTS && marqueeBox.value === null) {
    return
  }
  const left = Math.max(0, Math.min(start.from.x, pointer.x))
  const top = Math.max(0, Math.min(start.from.y, pointer.y))
  const right = Math.min(TABLE_PLAN_DRAWING_WIDTH, Math.max(start.from.x, pointer.x))
  const bottom = Math.max(0, Math.max(start.from.y, pointer.y))
  marqueeBox.value = {
    x: left,
    y: top,
    width: Math.max(0, right - left),
    height: Math.max(0, bottom - top),
  }
}

function finishTheMarquee(): void {
  window.removeEventListener('mousemove', stretchTheMarquee)
  window.removeEventListener('mouseup', finishTheMarquee)
  const start = marqueeStart
  const box = marqueeBox.value
  marqueeStart = null
  marqueeBox.value = null
  if (start === null || box === null) {
    return
  }
  const selectedInside = tableIdsInMarquee(props.area, box)
  const current = selection.value
  if (start.adds && current?.kind === 'tables') {
    selection.value = selectionOfTables([...current.tableIds, ...selectedInside])
    return
  }
  selection.value = selectionOfTables(selectedInside)
}

function stopWatchingTheMarquee(): void {
  window.removeEventListener('mousemove', stretchTheMarquee)
  window.removeEventListener('mouseup', finishTheMarquee)
  marqueeStart = null
  marqueeBox.value = null
}

function copyTheSelection(): void {
  const ids = selectedTableIds(selection.value)
  const tables = drawnTablesOf(props.area).filter((table) => ids.includes(table.tableId))
  tablePlan.copyTables(props.festivalId, props.area.areaId, tables)
}

async function pasteTables(at?: PlanPoint): Promise<void> {
  refusal.value = null
  const pasted = await tablePlan.pasteInto(props.festivalId, props.area.areaId, at)
  switch (pasted.kind) {
    case 'failed':
      showRefusalOf(pasted)
      return
    case 'ok':
      selection.value = selectionOfTables(pasted.value.tableIds)
      await nextTick()
      scrollToTheSelectedShape()
      return
    default:
      assertNever(pasted)
  }
}

function openTheTableMenu(shape: PlanShape, event: KonvaEventObject<MouseEvent, Konva.Group>): void {
  event.evt.preventDefault()
  event.cancelBubble = true
  if (shape.kind !== 'table') {
    return
  }
  if (!isTableSelected(selection.value, shape.tableId)) {
    selection.value = selectionOfTables([shape.tableId])
  }
  contextMenu.value = {
    at: [event.evt.clientX, event.evt.clientY],
    drawingPoint: drawingPointOfClient(event.evt),
    on: 'table',
  }
  contextMenuOpen.value = true
}

function openTheEmptySpotMenu(event: KonvaEventObject<MouseEvent>): void {
  if (!tablePlan.hasCopiedTables) {
    contextMenu.value = null
    contextMenuOpen.value = false
    return
  }
  contextMenu.value = {
    at: [event.evt.clientX, event.evt.clientY],
    drawingPoint: drawingPointOfClient(event.evt),
    on: 'empty',
  }
  contextMenuOpen.value = true
}

function handleTheDrawingMenu(event: KonvaEventObject<MouseEvent>): void {
  event.evt.preventDefault()
  if (event.target !== event.target.getStage()) {
    return
  }
  openTheEmptySpotMenu(event)
}

function copyFromTheMenu(): void {
  copyTheSelection()
  contextMenuOpen.value = false
}

async function pasteFromTheMenu(): Promise<void> {
  const at = contextMenu.value?.drawingPoint
  contextMenuOpen.value = false
  if (at !== undefined) {
    await pasteTables(at)
  }
}

function isATextControl(target: EventTarget | null): boolean {
  return (
    target instanceof HTMLElement
    && (target.tagName === 'INPUT'
      || target.tagName === 'TEXTAREA'
      || target.tagName === 'SELECT'
      || target.isContentEditable)
  )
}

function aDialogIsOpen(): boolean {
  return document.querySelector('[role="dialog"]') !== null
}

function handleShortcut(event: KeyboardEvent): void {
  if (!(event.ctrlKey || event.metaKey) || event.altKey) {
    return
  }
  const key = event.key.toLowerCase()
  if (key !== 'c' && key !== 'v') {
    return
  }
  if (isATextControl(event.target) || aDialogIsOpen()) {
    return
  }
  if (key === 'c') {
    if (selectedTableIds(selection.value).length === 0) {
      return
    }
    event.preventDefault()
    copyTheSelection()
    return
  }
  if (!tablePlan.hasCopiedTables) {
    return
  }
  event.preventDefault()
  void pasteTables()
}

function listenersOf(shape: ShapeOnThePlan): Konva.GroupConfig {
  return {
    onClick: (event: KonvaEventObject<MouseEvent, Konva.Group>) => {
      if (event.evt.button === 0) {
        chooseTheShape(shape.shape, event.evt.ctrlKey || event.evt.shiftKey)
      }
    },
    onTap: () => chooseTheShape(shape.shape, false),
    onContextmenu: (event: KonvaEventObject<MouseEvent, Konva.Group>) =>
      openTheTableMenu(shape.shape, event),
    onDragstart: () => startDragging(shape),
    onDragmove: (event: KonvaEventObject<DragEvent, Konva.Group>) =>
      groupDrag === null ? snapWhileDragging(event) : snapTheGroupWhileDragging(event),
    onDragend: (event: KonvaEventObject<DragEvent, Konva.Group>) =>
      groupDrag === null ? void saveTheNewGeometry(shape, event) : void saveTheMovedGroup(),
    onTransformstart: () => startTransforming(shape),
    onTransform: keepTheNameUnstretched,
    onTransformend: (event: KonvaEventObject<Event, Konva.Group>) => saveTheNewGeometry(shape, event),
    onMouseenter: (event: KonvaEventObject<MouseEvent, Konva.Group>) => showTheCursor(event, 'move'),
    onMouseleave: (event: KonvaEventObject<MouseEvent, Konva.Group>) => showTheCursor(event, ''),
  }
}

function showTheCursor(event: KonvaEventObject<MouseEvent, Konva.Group>, cursor: string): void {
  const container = event.currentTarget.getStage()?.container()
  if (container !== undefined) {
    container.style.cursor = cursor
  }
}

function firstSelectedNode(): Konva.Node | undefined {
  const selected = selection.value
  const layerNode = layer.value?.getNode()
  if (selected === null || layerNode === undefined) {
    return undefined
  }
  switch (selected.kind) {
    case 'tables':
      return selected.tableIds.length === 0
        ? undefined
        : layerNode.findOne(`#${nodeIdOfTable(selected.tableIds[0])}`)
    case 'station':
      return layerNode.findOne(`#${nodeIdOfStation(selected.stationId)}`)
    default:
      return assertNever(selected)
  }
}

function onlySelectedNode(): Konva.Node | undefined {
  const selected = selection.value
  if (selected === null) {
    return undefined
  }
  if (selected.kind === 'station') {
    return layer.value?.getNode().findOne(`#${nodeIdOfStation(selected.stationId)}`)
  }
  return selected.tableIds.length === 1
    ? layer.value?.getNode().findOne(`#${nodeIdOfTable(selected.tableIds[0])}`)
    : undefined
}

function attachTheTransformer(): void {
  const handles = transformer.value?.getNode()
  const node = onlySelectedNode()
  if (handles === undefined || handles.nodes()[0] === node) {
    return
  }
  handles.nodes(node === undefined ? [] : [node])
}

function scrollToTheSelectedShape(): void {
  const node = firstSelectedNode()
  const scroller = scrollArea.value
  if (node === undefined || scroller === null) {
    return
  }
  const box = node.getClientRect({ skipStroke: true })
  const isAboveTheView = box.y < scroller.scrollTop
  const isBelowTheView = box.y + box.height > scroller.scrollTop + scroller.clientHeight
  if (isAboveTheView || isBelowTheView) {
    scroller.scrollTop = Math.max(0, box.y - SCROLL_MARGIN_PIXELS)
  }
}

function measureTheView(): void {
  visibleHeight.value = scrollArea.value?.clientHeight ?? 0
}

watch([selection, drawnShapes], attachTheTransformer, { flush: 'post' })

watch(selection, scrollToTheSelectedShape, { flush: 'post' })

onMounted(() => {
  measureTheView()
  attachTheTransformer()
  window.addEventListener('keydown', handleShortcut)
  if (scrollArea.value !== null) {
    resizeObserver = new ResizeObserver(measureTheView)
    resizeObserver.observe(scrollArea.value)
  }
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleShortcut)
  stopWatchingTheMarquee()
  resizeObserver?.disconnect()
  resizeObserver = null
})
</script>

<template>
  <section class="table-plan-drawing" data-test="table-plan-drawing-column">
    <v-alert v-if="refusalText !== null" class="refusal mb-2" data-test="refusal" type="warning" variant="tonal">
      {{ refusalText }}
    </v-alert>
    <div ref="scrollArea" class="drawing-scroll" data-test="drawing-scroll">
      <div class="drawing-frame" data-test="table-plan-drawing">
        <KonvaStage
          ref="stage"
          :config="stageConfig"
          @click="deselectOnAnEmptySpot"
          @tap="deselectOnAnEmptySpot"
          @contextmenu="handleTheDrawingMenu"
          @mousedown="beginPressingTheDrawing"
        >
          <KonvaLayer
            ref="layer"
            :config="layerConfig"
          >
            <KonvaGroup
              v-for="shape in drawnShapes"
              :key="shape.key"
              :config="shape.group"
            >
              <KonvaRect :config="shape.rect" />
              <KonvaText :config="shape.text" />
            </KonvaGroup>
            <KonvaLine
              v-for="guide in guideLines"
              :key="guide.key"
              :config="guide.config"
            />
            <KonvaRect
              v-if="marqueeBox !== null"
              :config="marqueeFillConfig"
            />
            <KonvaRect
              v-if="marqueeBox !== null"
              :config="marqueeStrokeConfig"
            />
            <KonvaTransformer
              ref="transformer"
              :config="transformerConfig"
            />
          </KonvaLayer>
        </KonvaStage>
      </div>
    </div>

    <v-menu
      v-model="contextMenuOpen"
      :target="menuTarget"
      location="bottom start"
      scroll-strategy="none"
    >
      <v-list v-if="contextMenu !== null" data-test="table-plan-menu">
        <v-list-item
          v-if="contextMenu.on === 'table'"
          data-test="copy-table"
          @click="copyFromTheMenu"
        >
          <v-list-item-title>{{ t('admin.tablePlan.actions.copy') }}</v-list-item-title>
        </v-list-item>
        <v-list-item
          v-else
          data-test="paste-table"
          @click="pasteFromTheMenu"
        >
          <v-list-item-title>{{ t('admin.tablePlan.actions.paste') }}</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>
  </section>
</template>

<style scoped>
.table-plan-drawing {
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.drawing-scroll {
  border: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
  flex: 1 1 0;
  min-height: 0;
  overflow-y: auto;
  scrollbar-gutter: stable;
  width: fit-content;
}

.drawing-frame {
  background-color: rgb(var(--v-theme-surface));
}
</style>
