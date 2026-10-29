<script setup lang="ts">
import Konva from 'konva'
import type { KonvaEventObject } from 'konva/lib/Node'
import type { Vector2d } from 'konva/lib/types'
import { computed, onMounted, onUnmounted, ref, useTemplateRef, watch } from 'vue'
import {
  Group as KonvaGroup,
  Layer as KonvaLayer,
  Rect as KonvaRect,
  Stage as KonvaStage,
  Text as KonvaText,
  type VueKonvaRef,
} from 'vue-konva'
import type { TablePlanAreaView, TablePlanTableView } from '../../../shared/api/generatedSchemas'
import { useShapeNameWordWidth } from '../../../shared/composables/useShapeNameWordWidth'
import { useThemedValue } from '../../../shared/composables/useThemedValue'
import { DARK_TABLE_PLAN_COLOURS, LIGHT_TABLE_PLAN_COLOURS } from '../../../shared/core/tablePlanColours'
import { TABLE_PLAN_DRAWING_WIDTH } from '../../../shared/core/tablePlanDrawing'
import {
  cameraFittingTheWidth,
  clampedPosition,
  pinchedCamera,
  pinchOf,
  waiterDrawingLength,
  type Pinch,
  type PlanCamera,
  type PlanViewport,
} from '../../core/tablePlanCamera'
import { rectangleToStrokeInside } from '../../core/tablePlanFrame'
import { LARGEST_NAME_FONT_SIZE, nameFontSizeKeepingWordsWhole } from '../../../shared/core/shapeNameFit'
import { scrollBarsOf } from '../../core/tablePlanScrollBars'

const TABLE_NAME = 'plan-table'

const STATION_NAME = 'plan-station'

const NAME_PADDING = 4

const TABLE_BORDER_WIDTH = 4

const PIXELS_BEFORE_A_PAN_STARTS = 8

const props = defineProps<{ area: TablePlanAreaView }>()
const emit = defineEmits<{ 'open-table': [table: TablePlanTableView] }>()

const colours = useThemedValue(LIGHT_TABLE_PLAN_COLOURS, DARK_TABLE_PLAN_COLOURS)
const frame = useTemplateRef<HTMLElement>('frame')
const stage = useTemplateRef<VueKonvaRef<Konva.Stage>>('stage')
const measured = ref<PlanViewport>({ width: 0, height: 0 })
let resizeObserver: ResizeObserver | null = null
let lastPinch: Pinch | null = null
let panWasStoppedForAPinch = false
let thisGestureIsAPinch = false

const drawingLength = computed(() =>
  waiterDrawingLength([
    ...props.area.tables.map((table) => table.geometry),
    ...props.area.stationShapes.map((stationShape) => stationShape.geometry),
  ]),
)

const viewport = computed<PlanViewport>(() =>
  measured.value.width > 0 && measured.value.height > 0
    ? measured.value
    : { width: TABLE_PLAN_DRAWING_WIDTH, height: drawingLength.value },
)

const camera = ref<PlanCamera>(cameraFittingTheWidth(viewport.value))

function keepThePlanInView(position: Vector2d): Vector2d {
  return clampedPosition(position, camera.value.scale, viewport.value, drawingLength.value)
}

const scrollBars = computed(() => scrollBarsOf(camera.value, viewport.value, drawingLength.value))

const verticalBarStyle = computed(() => {
  const bar = scrollBars.value.vertical
  return bar === null ? null : { top: `${bar.offset}px`, height: `${bar.length}px` }
})

const horizontalBarStyle = computed(() => {
  const bar = scrollBars.value.horizontal
  return bar === null ? null : { left: `${bar.offset}px`, width: `${bar.length}px` }
})

const stageConfig = computed(() => ({
  width: viewport.value.width,
  height: viewport.value.height,
  scaleX: camera.value.scale,
  scaleY: camera.value.scale,
  x: camera.value.x,
  y: camera.value.y,
  draggable: true,
  dragDistance: PIXELS_BEFORE_A_PAN_STARTS,
  dragBoundFunc: keepThePlanInView,
}))

const nameText = {
  fontSize: LARGEST_NAME_FONT_SIZE,
  align: 'center',
  verticalAlign: 'middle',
  padding: NAME_PADDING,
  wrap: 'word',
  ellipsis: true,
  listening: false,
}

const widthOfANameWord = useShapeNameWordWidth()

function openUnlessThisGestureIsAPinch(table: TablePlanTableView): void {
  if (!thisGestureIsAPinch) {
    emit('open-table', table)
  }
}

function stopTheClickThatFollowsTheTap(tap: KonvaEventObject<TouchEvent>): void {
  tap.evt.preventDefault()
}

const drawnTables = computed(() => {
  const palette = colours.value
  return props.area.tables.map((table) => ({
    key: table.tableId,
    group: {
      id: `table-${table.tableId}`,
      name: TABLE_NAME,
      ...table.geometry,
      onClick: () => openUnlessThisGestureIsAPinch(table),
      onTap: (tap: KonvaEventObject<TouchEvent>) => {
        stopTheClickThatFollowsTheTap(tap)
        openUnlessThisGestureIsAPinch(table)
      },
    },
    rect: {
      ...rectangleToStrokeInside(table.geometry, TABLE_BORDER_WIDTH),
      fill: palette.handOutFill[table.handOutProgress],
      stroke: palette.settlementBorder[table.settlementProgress],
      strokeWidth: TABLE_BORDER_WIDTH,
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
  }))
})

const drawnStations = computed(() => {
  const palette = colours.value
  return props.area.stationShapes.map((stationShape) => ({
    key: stationShape.stationName,
    group: {
      name: STATION_NAME,
      ...stationShape.geometry,
      listening: false,
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
      fontSize: nameFontSizeKeepingWordsWhole(
        stationShape.stationName,
        stationShape.geometry.width - 2 * NAME_PADDING,
        widthOfANameWord,
      ),
      width: stationShape.geometry.width,
      height: stationShape.geometry.height,
      text: stationShape.stationName,
      fill: palette.stationInk,
    },
  }))
})

function followThePan(): void {
  const node = stage.value?.getNode()
  if (node !== undefined) {
    camera.value = { ...camera.value, x: node.x(), y: node.y() }
  }
}

function noteTheFingersDown(event: KonvaEventObject<TouchEvent, Konva.Stage>): void {
  if (event.evt.touches.length === 1) {
    thisGestureIsAPinch = false
    panWasStoppedForAPinch = false
    return
  }
  thisGestureIsAPinch = true
}

function forgetTheLastPinch(): void {
  thisGestureIsAPinch = false
}

function followTheFingers(event: KonvaEventObject<TouchEvent, Konva.Stage>): void {
  event.evt.preventDefault()
  const node = event.currentTarget
  const touches = event.evt.touches
  if (touches.length === 1 && panWasStoppedForAPinch && !node.isDragging()) {
    node.startDrag()
    panWasStoppedForAPinch = false
  }
  if (touches.length < 2) {
    return
  }
  thisGestureIsAPinch = true
  if (node.isDragging()) {
    panWasStoppedForAPinch = true
    node.stopDrag()
  }
  const box = node.container().getBoundingClientRect()
  const pinch = pinchOf(
    { x: touches[0].clientX - box.left, y: touches[0].clientY - box.top },
    { x: touches[1].clientX - box.left, y: touches[1].clientY - box.top },
  )
  if (lastPinch !== null) {
    camera.value = pinchedCamera(camera.value, lastPinch, pinch, viewport.value, drawingLength.value)
  }
  lastPinch = pinch
}

function letGoOfThePinch(): void {
  lastPinch = null
}

function measureTheFrame(): void {
  measured.value = { width: frame.value?.clientWidth ?? 0, height: frame.value?.clientHeight ?? 0 }
}

watch([() => props.area.areaId, () => viewport.value.width], () => {
  camera.value = cameraFittingTheWidth(viewport.value)
})

onMounted(() => {
  Konva.hitOnDragEnabled = true
  measureTheFrame()
  if (frame.value !== null) {
    resizeObserver = new ResizeObserver(measureTheFrame)
    resizeObserver.observe(frame.value)
  }
})

onUnmounted(() => {
  resizeObserver?.disconnect()
  resizeObserver = null
})
</script>

<template>
  <div ref="frame" class="area-plan" data-test="area-plan" :data-test-id="area.areaId">
    <KonvaStage
      ref="stage"
      :config="stageConfig"
      @dragmove="followThePan"
      @dragend="followThePan"
      @mousedown="forgetTheLastPinch"
      @touchstart="noteTheFingersDown"
      @touchmove="followTheFingers"
      @touchend="letGoOfThePinch"
    >
      <KonvaLayer>
        <KonvaGroup
          v-for="station in drawnStations"
          :key="station.key"
          :config="station.group"
        >
          <KonvaRect :config="station.rect" />
          <KonvaText :config="station.text" />
        </KonvaGroup>
        <KonvaGroup
          v-for="table in drawnTables"
          :key="table.key"
          :config="table.group"
        >
          <KonvaRect :config="table.rect" />
          <KonvaText :config="table.text" />
        </KonvaGroup>
      </KonvaLayer>
    </KonvaStage>
    <div
      v-if="verticalBarStyle !== null"
      class="scroll-bar scroll-bar-vertical"
      data-test="plan-scroll-bar"
      data-test-id="vertical"
      :style="verticalBarStyle"
    />
    <div
      v-if="horizontalBarStyle !== null"
      class="scroll-bar scroll-bar-horizontal"
      data-test="plan-scroll-bar"
      data-test-id="horizontal"
      :style="horizontalBarStyle"
    />
  </div>
</template>

<style scoped>
.area-plan {
  position: relative;
  flex: 1 1 0;
  min-height: 0;
  overflow: hidden;
  touch-action: none;
  background-color: rgb(var(--v-theme-surface));
}

.scroll-bar {
  position: absolute;
  pointer-events: none;
  border-radius: 2px;
  background-color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
}

.scroll-bar-vertical {
  right: 2px;
  width: 4px;
}

.scroll-bar-horizontal {
  bottom: 2px;
  height: 4px;
}
</style>
