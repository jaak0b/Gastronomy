import type {
  AdminAreaView,
  AdminFestivalIngredientView,
  AdminFestivalView,
  AdminIngredientView,
  AdminItemIngredientView,
  AdminItemView,
  AdminStationShapeView,
  AdminStationView,
  AdminTableView,
  AdminUnitView,
  AreaShapeGeometry,
  TablePlanAreaView,
  TablePlanStationShapeView,
  TablePlanTableView,
  StationOrderQueueView,
  StationQueueItemView,
} from '../../src/shared/api/generatedSchemas'

export function anAdminItem(overrides: Partial<AdminItemView> = {}): AdminItemView {
  return {
    itemId: 'item-bratwurst',
    name: 'Bratwurst',
    categoryId: 'category-speisen',
    sortOrder: 1,
    isActive: true,
    productionMinutes: null,
    isQueueIndependent: false,
    atTheFestival: null,
    ingredients: [],
    ...overrides,
  }
}

export function anAdminItemIngredient(
  overrides: Partial<AdminItemIngredientView> = {},
): AdminItemIngredientView {
  return {
    ingredientId: 'ingredient-broetchen',
    name: 'Brötchen',
    unitId: 'unit-stueck',
    unitName: 'Stück',
    amount: 1,
    ...overrides,
  }
}

export function anAdminIngredient(overrides: Partial<AdminIngredientView> = {}): AdminIngredientView {
  return {
    ingredientId: 'ingredient-broetchen',
    name: 'Brötchen',
    unitId: 'unit-stueck',
    unitName: 'Stück',
    isActive: true,
    activeItemNames: [],
    ...overrides,
  }
}

export function anAdminUnit(overrides: Partial<AdminUnitView> = {}): AdminUnitView {
  return {
    unitId: 'unit-stueck',
    name: 'Stück',
    isActive: true,
    usedByIngredientNames: [],
    ...overrides,
  }
}

export const SEEDED_UNITS = [
  anAdminUnit(),
  anAdminUnit({ unitId: 'unit-gram', name: 'g' }),
  anAdminUnit({ unitId: 'unit-kilogram', name: 'kg' }),
  anAdminUnit({ unitId: 'unit-millilitre', name: 'ml' }),
  anAdminUnit({ unitId: 'unit-litre', name: 'l' }),
]

export function anAdminFestivalIngredient(
  overrides: Partial<AdminFestivalIngredientView> = {},
): AdminFestivalIngredientView {
  return {
    ingredientId: 'ingredient-mehl',
    name: 'Mehl',
    unitId: 'unit-gram',
    unitName: 'g',
    availableAmount: 25000,
    usedAmount: 1250,
    usage: [
      { atUtc: '2026-07-18T10:00:00Z', usedAmount: 0 },
      { atUtc: '2026-07-18T15:00:00Z', usedAmount: 1250 },
      { atUtc: '2026-07-18T18:00:00Z', usedAmount: 1250 },
    ],
    runsOutAtUtcSinceStart: null,
    runsOutAtUtcFromLastTwoHours: null,
    stockWarning: 'none',
    runsOutSoonAtUtc: null,
    ...overrides,
  }
}

export function aFestival(overrides: Partial<AdminFestivalView> = {}): AdminFestivalView {
  return {
    festivalId: 'fest-1',
    name: 'Sommerfest',
    startsAtUtc: '2026-07-18T10:00:00Z',
    endsAtUtc: '2026-07-19T02:00:00Z',
    isHidden: false,
    isRunning: true,
    stationCount: 1,
    menuItemCount: 1,
    orderCount: 0,
    ...overrides,
  }
}

export function anAdminStation(overrides: Partial<AdminStationView> = {}): AdminStationView {
  return {
    stationId: 'station-kueche',
    name: 'Küche',
    sortOrder: 1,
    isActive: true,
    hasDevice: true,
    isAtFestival: true,
    hasItemsToHandOutAtTheRunningFestival: false,
    strandedItemCountWhenSwitchedOff: null,
    strandedItemCountWhenRemovedFromTheFestival: null,
    ...overrides,
  }
}

export function aQueuedItem(overrides: Partial<StationQueueItemView> = {}): StationQueueItemView {
  return {
    orderItemId: 'order-item-1',
    itemName: 'Bratwurst',
    note: null,
    state: 'open',
    stateSetAtUtc: '2026-07-18T18:00:00Z',
    ...overrides,
  }
}

export function aStationOrder(
  overrides: Partial<StationOrderQueueView> = {},
): StationOrderQueueView {
  const items = overrides.items ?? [aQueuedItem()]
  return {
    stationOrderId: 'station-order-1',
    globalOrderNumber: 1,
    stationOrderNumber: 1,
    tableName: 'Tisch 4',
    staffMemberName: 'Anna',
    deliveryMode: 'together',
    createdAtUtc: '2026-07-18T18:00:00Z',
    isHiddenFromAsItComesQueue: false,
    itemCount: items.filter((item) => item.state === 'open' || item.state === 'handedOut').length,
    fulfilledItemCount: items.filter((item) => item.state === 'handedOut').length,
    ...overrides,
    items,
  }
}

export function aGeometry(overrides: Partial<AreaShapeGeometry> = {}): AreaShapeGeometry {
  return { x: 20, y: 30, width: 80, height: 40, rotation: 0, ...overrides }
}

export function anAdminTable(overrides: Partial<AdminTableView> = {}): AdminTableView {
  return {
    tableId: 'table-a1',
    name: 'A1',
    geometry: aGeometry(),
    isHidden: false,
    hasItemsToHandOutOrSettleAtTheRunningFestival: false,
    ...overrides,
  }
}

export function anAdminStationShape(
  overrides: Partial<AdminStationShapeView> = {},
): AdminStationShapeView {
  return {
    stationId: 'station-schank',
    stationName: 'Schank',
    geometry: aGeometry({ x: 200, y: 30, width: 120, height: 40 }),
    ...overrides,
  }
}

export function anAdminArea(overrides: Partial<AdminAreaView> = {}): AdminAreaView {
  return {
    areaId: 'area-halle',
    name: 'Halle',
    isHidden: false,
    hasItemsToHandOutOrSettleAtTheRunningFestival: false,
    tables: [],
    stationShapes: [],
    ...overrides,
  }
}

export function aPlanTable(overrides: Partial<TablePlanTableView> = {}): TablePlanTableView {
  return {
    tableId: 'table-4',
    name: 'Tisch 4',
    geometry: aGeometry(),
    handOutProgress: 'all',
    settlementProgress: 'all',
    ...overrides,
  }
}

export function aPlanStationShape(
  overrides: Partial<TablePlanStationShapeView> = {},
): TablePlanStationShapeView {
  return {
    stationName: 'Schank',
    geometry: aGeometry({ x: 200, y: 30, width: 120, height: 40 }),
    ...overrides,
  }
}

export function aPlanArea(overrides: Partial<TablePlanAreaView> = {}): TablePlanAreaView {
  return {
    areaId: 'area-halle',
    name: 'Halle',
    tables: [],
    stationShapes: [],
    ...overrides,
  }
}
