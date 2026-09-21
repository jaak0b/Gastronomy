import { computed, ref, watch, type ComputedRef, type Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { AdminItemView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import { appLanguageOf } from '../../shared/core/deviceLanguage'
import { formatEuroInput, parseEuroInput } from '../../shared/core/money'
import { refusalFrom, type AdminActionResult } from '../core/adminActionResult'
import {

  adminErrorMessageForKey,
  adminErrorMessageText,
  type AdminErrorMessage,
} from '../core/adminErrorMessage'
import {

  laptopConfirmedField,
  stillDiffersFromTheLaptop,
  type LaptopConfirmedField,
} from '../core/laptopConfirmedField'
import { useAdminItemsStore } from '../stores/items'

export type PlacedItem = AdminItemView & { atTheFestival: NonNullable<AdminItemView['atTheFestival']> }

export interface Placement {
  priceText: string
  stationIds: string[]
}

export interface FestivalItemRow {
  price: LaptopConfirmedField<string>
  stations: LaptopConfirmedField<string[]>
  onItsWay: Placement | null
  soldOutSwitchRenderKey: number
  refusal: AdminErrorMessage | null
}

export interface FestivalItemRows {
  itemsAtTheFestival: ComputedRef<PlacedItem[]>
  rowShownFor: (itemId: string) => FestivalItemRow
  rowRefusalText: (itemId: string) => string | null
  priceIsUnreadable: (itemId: string) => boolean
  typePrice: (itemId: string, typed: string) => void
  save: (itemId: string) => Promise<void>
  changeStations: (itemId: string, stationIds: string[]) => Promise<void>
  setSoldOut: (item: AdminItemView, isSoldOut: boolean) => Promise<void>
  removeFromTheFestival: (item: AdminItemView) => Promise<void>
}

function namesTheSameStations(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((stationId) => right.includes(stationId))
}

function emptyFestivalItemRow(): FestivalItemRow {
  return {
    price: laptopConfirmedField('', ''),
    stations: laptopConfirmedField<string[]>([], []),
    onItsWay: null,
    soldOutSwitchRenderKey: 0,
    refusal: null,
  }
}

export function useFestivalItemRows(festivalId: Ref<string>): FestivalItemRows {
  const { t, locale } = useI18n()
  const items = useAdminItemsStore()
  const rows = ref(new Map<string, FestivalItemRow>())

  const itemsAtTheFestival = computed(() =>
    items.items.filter((item): item is PlacedItem => item.atTheFestival !== null),
  )

  function rowForPlacedItem(item: PlacedItem): FestivalItemRow {
    const priceText = formatEuroInput(item.atTheFestival.priceCents, appLanguageOf(locale.value))
    const stationIds = [...item.atTheFestival.stationIds]
    return {
      ...emptyFestivalItemRow(),
      price: laptopConfirmedField(priceText, priceText),
      stations: laptopConfirmedField([...stationIds], [...stationIds]),
      soldOutSwitchRenderKey: rows.value.get(item.itemId)?.soldOutSwitchRenderKey ?? 0,
    }
  }

  function awaitsTheLaptop(row: FestivalItemRow): boolean {
    return (
      stillDiffersFromTheLaptop(row.price)
      || stillDiffersFromTheLaptop(row.stations, namesTheSameStations)
    )
  }

  function isBeingEdited(row: FestivalItemRow): boolean {
    return row.onItsWay !== null || row.refusal !== null || awaitsTheLaptop(row)
  }

  watch(
    itemsAtTheFestival,
    (listed) => {
      const next = new Map<string, FestivalItemRow>()
      for (const item of listed) {
        const known = rows.value.get(item.itemId)
        next.set(
          item.itemId,
          known !== undefined && isBeingEdited(known) ? known : rowForPlacedItem(item),
        )
      }
      rows.value = next
    },
    { immediate: true },
  )

  function rowShownFor(itemId: string): FestivalItemRow {
    return rows.value.get(itemId) ?? emptyFestivalItemRow()
  }

  function matchesPlacement(row: FestivalItemRow, placement: Placement): boolean {
    return (
      row.price.edited === placement.priceText
      && namesTheSameStations(row.stations.edited, placement.stationIds)
    )
  }

  function isAlreadyAtTheLaptop(row: FestivalItemRow): boolean {
    return row.onItsWay === null ? !awaitsTheLaptop(row) : matchesPlacement(row, row.onItsWay)
  }

  function itemNameFor(itemId: string): string {
    return items.items.find((item) => item.itemId === itemId)?.name ?? ''
  }

  function refuse(
    itemId: string,
    messageKey: string,
    parameters: Record<string, string | number> = {},
  ): void {
    const row = rows.value.get(itemId)
    if (row !== undefined) {
      row.refusal = adminErrorMessageForKey(messageKey, parameters)
    }
  }

  function showRowRefusal(itemId: string, result: AdminActionResult<unknown>): void {
    const message = refusalFrom(result)
    if (message === null) {
      return
    }
    const row = rows.value.get(itemId)
    if (row !== undefined) {
      row.refusal = message
    }
  }

  function rowRefusalText(itemId: string): string | null {
    const message = rowShownFor(itemId).refusal
    return message === null ? null : adminErrorMessageText(t, message)
  }

  function typePrice(itemId: string, typed: string): void {
    const row = rows.value.get(itemId)
    if (row !== undefined) {
      row.price.edited = typed
    }
  }

  function priceIsUnreadable(itemId: string): boolean {
    const typed = rowShownFor(itemId).price.edited
    return typed.trim().length > 0 && parseEuroInput(typed) === null
  }

  function priceTheLaptopCanTake(itemId: string, row: FestivalItemRow): number | null {
    const priceCents = parseEuroInput(row.price.edited)
    if (priceCents === null) {
      refuse(itemId, 'admin.itemPriceOutOfRange')
      return null
    }
    if (row.stations.edited.length === 0) {
      refuse(itemId, 'admin.festival.itemNeedsAStation', { item: itemNameFor(itemId) })
      return null
    }
    return priceCents
  }

  async function save(itemId: string): Promise<void> {
    const row = rows.value.get(itemId)
    if (row === undefined) {
      return
    }
    if (isAlreadyAtTheLaptop(row)) {
      return
    }
    if (priceIsUnreadable(itemId)) {
      return
    }
    if (priceTheLaptopCanTake(itemId, row) === null) {
      return
    }
    row.refusal = null
    if (row.onItsWay !== null) {
      return
    }
    await sendUntilTheLaptopHasTheRow(itemId, row)
  }

  async function sendUntilTheLaptopHasTheRow(itemId: string, row: FestivalItemRow): Promise<void> {
    while (awaitsTheLaptop(row)) {
      const priceCents = priceTheLaptopCanTake(itemId, row)
      if (priceCents === null) {
        return
      }
      const onItsWay: Placement = {
        priceText: row.price.edited,
        stationIds: [...row.stations.edited],
      }
      row.onItsWay = onItsWay
      row.refusal = null
      const placed = await items.putAtTheFestival(festivalId.value, itemId, {
        priceCents,
        stationIds: [...onItsWay.stationIds],
      })
      row.soldOutSwitchRenderKey += 1
      switch (placed.kind) {
        case 'ok':
          row.price.confirmedByTheLaptop = onItsWay.priceText
          row.stations.confirmedByTheLaptop = [...onItsWay.stationIds]
          row.onItsWay = null
          row.refusal = null
          break
        case 'failed':
          row.refusal = placed.message
          row.price.edited = row.price.confirmedByTheLaptop
          row.stations.edited = [...row.stations.confirmedByTheLaptop]
          row.onItsWay = null
          return
        default:
          assertNever(placed)
      }
    }
  }

  async function changeStations(itemId: string, stationIds: string[]): Promise<void> {
    const row = rows.value.get(itemId)
    if (row === undefined) {
      return
    }
    if (priceIsUnreadable(itemId)) {
      refuse(itemId, 'admin.itemPriceOutOfRange')
      return
    }
    if (stationIds.length === 0) {
      refuse(itemId, 'admin.festival.itemNeedsAStation', { item: itemNameFor(itemId) })
      return
    }
    row.stations.edited = [...stationIds]
    await save(itemId)
  }

  async function setSoldOut(item: AdminItemView, isSoldOut: boolean): Promise<void> {
    const started = rows.value.get(item.itemId)
    if (started !== undefined) {
      started.refusal = null
    }
    const accepted = await items.setAvailability(festivalId.value, item.itemId, !isSoldOut)
    const row = rows.value.get(item.itemId)
    if (row !== undefined) {
      row.soldOutSwitchRenderKey += 1
    }
    showRowRefusal(item.itemId, accepted)
  }

  async function removeFromTheFestival(item: AdminItemView): Promise<void> {
    const row = rows.value.get(item.itemId)
    if (row !== undefined) {
      row.refusal = null
    }
    const accepted = await items.removeFromTheFestival(festivalId.value, item.itemId)
    showRowRefusal(item.itemId, accepted)
  }

  return {
    itemsAtTheFestival,
    rowShownFor,
    rowRefusalText,
    priceIsUnreadable,
    typePrice,
    save,
    changeStations,
    setSoldOut,
    removeFromTheFestival,
  }
}
