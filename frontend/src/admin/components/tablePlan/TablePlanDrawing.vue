<script setup lang="ts">
import Konva from 'konva'
import type { KonvaEventObject } from 'konva/lib/Node'
import type { Box } from 'konva/lib/shapes/Transformer'
import type { Vector2d } from 'konva/lib/types'
import { computed, onMounted, onUnmounted, ref, useTemplateRef, watch } from 'vue'
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
import { TABLE_PLAN_DRAWING_WIDTH } from '../../../shared/core/tablePlanDrawing'
import type { AdminActionResult } from '../../core/adminActionResult'
import { drawnTablesOf, editorDrawingLength, fitsTheDrawing } from '../../core/tablePlanPlacement'
import { shapeNodeIdOf, type ShapeSelection } from '../../core/tablePlanSelection'
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

interface ShapeOnThePlan {
  selection: ShapeSelection
  geometry: AreaShapeGeometry
  saveGeometry: (geometry: AreaShapeGeometry) => Promise<AdminActionResult<null>>
}

interface DrawnShape {
  key: string
  geometry: AreaShapeGeometry
  group: Konva.GroupConfig
  rect: Konva.RectConfig
  text: Konva.TextConfig
}

const props = defineProps<{ festivalId: string; area: AdminAreaView }>()
const selectedShape = defineModel<ShapeSelection | null>('selectedShape', { required: true })

const tablePlan = useAdminTablePlanStore()
const colours = useThemedValue(LIGHT_TABLE_PLAN_COLOURS, DARK_TABLE_PLAN_COLOURS)
const { refusal, refusalText, showRefusalOf } = useRefusalDisplay()
const scrollArea = useTemplateRef<HTMLElement>('scrollArea')
const layer = useTemplateRef<VueKonvaRef<Konva.Layer>>('layer')
const transformer = useTemplateRef<VueKonvaRef<Konva.Transformer>>('transformer')
const visibleHeight = ref(0)
const guides = ref<SnapGuide[]>([])
let resizeObserver: ResizeObserver | null = null

const widthOfANameWord = useShapeNameWordWidth()

const layerConfig = { x: MARGIN_AROUND_THE_DRAWING, y: MARGIN_AROUND_THE_DRAWING }

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
        selection: { kind: 'table', tableId: table.tableId },
        geometry: table.geometry,
        saveGeometry: (geometry) =>
          tablePlan.saveTable(props.festivalId, table.tableId, {
            areaId: props.area.areaId,
            name: table.name,
            geometry,
          }),
      }
      return {
        key: shapeNodeIdOf(shape.selection),
        geometry: table.geometry,
        group: {
          id: shapeNodeIdOf(shape.selection),
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
          stroke: palette.ink,
          strokeWidth: 1,
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
        selection: { kind: 'station', stationId: stationShape.stationId },
        geometry: stationShape.geometry,
        saveGeometry: (geometry) =>
          tablePlan.placeStation(props.festivalId, stationShape.stationId, {
            areaId: props.area.areaId,
            geometry,
          }),
      }
      return {
        key: shapeNodeIdOf(shape.selection),
        geometry: stationShape.geometry,
        group: {
          id: shapeNodeIdOf(shape.selection),
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

function stopsBeside(node: Konva.Node): SnapStops {
  const otherShapes = layer.value?.getNode().find(`.${PLAN_SHAPE_NAME}`) ?? []
  return snapStopsBeside(otherShapes.filter((other) => other !== node).map((other) => boxOf(other)))
}

function drawGeometryOn(group: Konva.Group, geometry: AreaShapeGeometry): void {
  group.setAttrs({ ...geometry, scaleX: 1, scaleY: 1 })
  for (const part of group.getChildren()) {
    part.setAttrs({ width: geometry.width, height: geometry.height })
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

function select(selection: ShapeSelection): void {
  const selected = selectedShape.value
  if (selected === null || shapeNodeIdOf(selected) !== shapeNodeIdOf(selection)) {
    selectedShape.value = selection
  }
}

function startChangingTheShape(selection: ShapeSelection): void {
  refusal.value = null
  select(selection)
}

function snapWhileDragging(event: KonvaEventObject<DragEvent, Konva.Group>): void {
  const group = event.currentTarget
  const box = boxOf(group)
  const snapped = snapDraggedBox(box, stopsBeside(group))
  group.position({ x: group.x() - box.x + snapped.x, y: group.y() - box.y + snapped.y })
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
  const saved = await shape.saveGeometry({
    x: group.x(),
    y: group.y(),
    width: group.width(),
    height: group.height(),
    rotation: group.rotation(),
  })
  if (showRefusalOf(saved)) {
    drawGeometryOn(group, shape.geometry)
    transformer.value?.getNode().forceUpdate()
  }
}

function deselectOnAnEmptySpot(event: KonvaEventObject<MouseEvent>): void {
  if (event.target === event.target.getStage()) {
    selectedShape.value = null
  }
}

function showTheCursor(event: KonvaEventObject<MouseEvent, Konva.Group>, cursor: string): void {
  const stage = event.currentTarget.getStage()
  if (stage !== null) {
    stage.container().style.cursor = cursor
  }
}

function listenersOf(shape: ShapeOnThePlan): Konva.GroupConfig {
  return {
    onClick: () => select(shape.selection),
    onTap: () => select(shape.selection),
    onDragstart: () => startChangingTheShape(shape.selection),
    onDragmove: snapWhileDragging,
    onDragend: (event: KonvaEventObject<DragEvent, Konva.Group>) => saveTheNewGeometry(shape, event),
    onTransformstart: () => startChangingTheShape(shape.selection),
    onTransform: keepTheNameUnstretched,
    onTransformend: (event: KonvaEventObject<Event, Konva.Group>) => saveTheNewGeometry(shape, event),
    onMouseenter: (event: KonvaEventObject<MouseEvent, Konva.Group>) => showTheCursor(event, 'move'),
    onMouseleave: (event: KonvaEventObject<MouseEvent, Konva.Group>) => showTheCursor(event, ''),
  }
}

function selectedNode(): Konva.Node | undefined {
  const selection = selectedShape.value
  return selection === null
    ? undefined
    : layer.value?.getNode().findOne(`#${shapeNodeIdOf(selection)}`)
}

function attachTheTransformer(): void {
  const handles = transformer.value?.getNode()
  const node = selectedNode()
  if (handles === undefined || handles.nodes()[0] === node) {
    return
  }
  handles.nodes(node === undefined ? [] : [node])
}

function scrollToTheSelectedShape(): void {
  const node = selectedNode()
  const scroller = scrollArea.value
  if (node === undefined || scroller === null) {
    return
  }
  const box = node.getClientRect()
  const isAboveTheView = box.y < scroller.scrollTop
  const isBelowTheView = box.y + box.height > scroller.scrollTop + scroller.clientHeight
  if (isAboveTheView || isBelowTheView) {
    scroller.scrollTop = Math.max(0, box.y - SCROLL_MARGIN_PIXELS)
  }
}

function measureTheView(): void {
  visibleHeight.value = scrollArea.value?.clientHeight ?? 0
}

watch([selectedShape, drawnShapes], attachTheTransformer, { flush: 'post' })

watch(selectedShape, scrollToTheSelectedShape, { flush: 'post' })

onMounted(() => {
  measureTheView()
  attachTheTransformer()
  if (scrollArea.value !== null) {
    resizeObserver = new ResizeObserver(measureTheView)
    resizeObserver.observe(scrollArea.value)
  }
})

onUnmounted(() => {
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
          :config="stageConfig"
          @click="deselectOnAnEmptySpot"
          @tap="deselectOnAnEmptySpot"
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
            <KonvaTransformer
              ref="transformer"
              :config="transformerConfig"
            />
          </KonvaLayer>
        </KonvaStage>
      </div>
    </div>
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
