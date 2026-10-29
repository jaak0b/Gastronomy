import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import {
  aBrowserThatCountsTheTap,
  aBrowserWithoutUserActivation,
  pressBack,
  shownOnAStartedRouterAt,
  theAppWasLeftByBack,
} from './support/browserHistory'
import { waitUntil } from './support/dom'
import { stubLaptop, answer } from './support/laptop'

enableAutoUnmount(afterEach)

const WASSER_ORDERED = {
  orderItemId: 'item-wasser-1',
  orderId: 'order-1',
  globalOrderNumber: 1,
  itemName: 'Wasser',
  note: null,
  unitPriceCents: 200,
  state: 'open',
  stateSetAtUtc: '2026-09-05T18:00:00Z',
  wasHandedOutWhenCancelled: false,
  settledAtUtc: null,
  chargedPriceCents: null,
}

const WASSER_ON_THE_MENU = {
  id: 'item-wasser',
  name: 'Wasser',
  categoryId: 'category-getraenke',
  priceCents: 200,
  sortOrder: 1,
  isAvailable: true,
  stationIds: ['station-bar', 'station-schank'],
  productionMinutes: null,
  isQueueIndependent: false,
}

function timesEmitted(dialog: VueWrapper, event: string): number {
  return dialog.emitted(event)?.length ?? 0
}

async function pressBackThatTheDialogRefuses(): Promise<void> {
  const theDialogsEntry: unknown = window.history.state
  await pressBack()
  await waitUntil(() => expect(window.history.state).toEqual(theDialogsEntry))
}

beforeEach(() => {
  vi.resetModules()
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  stubLaptop().answersEverythingElse(answer({}))
  aBrowserThatCountsTheTap()
})

afterEach(() => {
  aBrowserWithoutUserActivation()
})

describe('back while a confirmation is open', () => {
  it('cancels it and stays in the app', async () => {
    const dialog = await shownOnAStartedRouterAt('/admin/festivals', () => import('../src/shared/components/BaseConfirmDialog.vue'), {
      title: 'Fest ausblenden?',
      confirmLabel: 'Ausblenden',
    })

    await pressBack()

    expect({ cancelled: timesEmitted(dialog, 'cancel'), leftTheApp: theAppWasLeftByBack() }).toEqual({ cancelled: 1, leftTheApp: false })
  })
})

describe('back while a form dialog is open', () => {
  it('cancels it and stays in the app', async () => {
    const dialog = await shownOnAStartedRouterAt('/admin/festivals', () => import('../src/shared/components/BaseFormDialog.vue'), {
      title: 'Neues Fest',
      errorText: null,
    })

    await pressBack()

    expect({ cancelled: timesEmitted(dialog, 'cancel'), leftTheApp: theAppWasLeftByBack() }).toEqual({ cancelled: 1, leftTheApp: false })
  })

  it('keeps it open while it saves, so the next back after the answer cancels it', async () => {
    const dialog = await shownOnAStartedRouterAt('/admin/festivals', () => import('../src/shared/components/BaseFormDialog.vue'), {
      title: 'Neues Fest',
      errorText: null,
      busy: true,
    })
    await pressBackThatTheDialogRefuses()
    await dialog.setProps({ busy: false, errorText: 'Das Fest überschneidet sich mit dem Sommerfest.' })

    await pressBack()

    expect({ cancelled: timesEmitted(dialog, 'cancel'), leftTheApp: theAppWasLeftByBack() }).toEqual({ cancelled: 1, leftTheApp: false })
  })
})

describe('back while the amount page is open', () => {
  it('cancels it and stays in the app', async () => {
    const dialog = await shownOnAStartedRouterAt('/open-items', () => import('../src/phone/components/openItems/AmountPaidDialog.vue'), {
      isSettling: false,
      notice: null,
      selectedTotalCents: 350,
      language: 'de',
    })

    await pressBack()

    expect({ cancelled: timesEmitted(dialog, 'cancel'), leftTheApp: theAppWasLeftByBack() }).toEqual({ cancelled: 1, leftTheApp: false })
  })

  it('keeps it open while it settles, so the next back after the answer cancels it', async () => {
    const dialog = await shownOnAStartedRouterAt('/open-items', () => import('../src/phone/components/openItems/AmountPaidDialog.vue'), {
      isSettling: true,
      notice: null,
      selectedTotalCents: 350,
      language: 'de',
    })
    await pressBackThatTheDialogRefuses()
    await dialog.setProps({ isSettling: false })

    await pressBack()

    expect({ cancelled: timesEmitted(dialog, 'cancel'), leftTheApp: theAppWasLeftByBack() }).toEqual({ cancelled: 1, leftTheApp: false })
  })
})

describe('back while the cancel page is open', () => {
  it('closes it and stays in the app', async () => {
    const dialog = await shownOnAStartedRouterAt('/open-items', () => import('../src/phone/components/openItems/CancelItemsDialog.vue'), {
      items: [WASSER_ORDERED],
      cashToReturnCents: 0,
      isCancelling: false,
      notice: null,
      language: 'de',
    })

    await pressBack()

    expect({ closed: timesEmitted(dialog, 'back'), leftTheApp: theAppWasLeftByBack() }).toEqual({ closed: 1, leftTheApp: false })
  })

  it('keeps it open while it cancels, so the next back after the answer closes it', async () => {
    const dialog = await shownOnAStartedRouterAt('/open-items', () => import('../src/phone/components/openItems/CancelItemsDialog.vue'), {
      items: [WASSER_ORDERED],
      cashToReturnCents: 0,
      isCancelling: true,
      notice: null,
      language: 'de',
    })
    await pressBackThatTheDialogRefuses()
    await dialog.setProps({ isCancelling: false })

    await pressBack()

    expect({ closed: timesEmitted(dialog, 'back'), leftTheApp: theAppWasLeftByBack() }).toEqual({ closed: 1, leftTheApp: false })
  })
})

describe('back while one of the phone\'s dialogs is open', () => {
  it('closes the history of an item', async () => {
    const dialog = await shownOnAStartedRouterAt('/order-overview', () => import('../src/phone/components/orderList/ItemHistoryDialog.vue'), {
      item: WASSER_ORDERED,
      language: 'de',
    })

    await pressBack()

    expect({ closed: timesEmitted(dialog, 'close'), leftTheApp: theAppWasLeftByBack() }).toEqual({ closed: 1, leftTheApp: false })
  })

  it('answers the question about unsent items with Zurück', async () => {
    const dialog = await shownOnAStartedRouterAt('/', () => import('../src/phone/components/tablePlan/UnsentOrderDialog.vue'), {
      draftTableName: '5',
      itemCount: 1,
      totalCents: 200,
      language: 'de',
    })

    await pressBack()

    expect({ stayed: timesEmitted(dialog, 'stay-on-the-plan'), leftTheApp: theAppWasLeftByBack() }).toEqual({ stayed: 1, leftTheApp: false })
  })

  it('cancels the station question of an article', async () => {
    const dialog = await shownOnAStartedRouterAt('/catalog', () => import('../src/phone/components/catalog/LineStationSheet.vue'), {
      item: WASSER_ON_THE_MENU,
      language: 'de',
      stationNameFor: (stationId: string) => (stationId === 'station-bar' ? 'Bar' : 'Schank'),
      estimateFor: () => null,
      currentStationId: null,
      withNote: true,
      focusTheNote: false,
    })

    await pressBack()

    expect({ cancelled: timesEmitted(dialog, 'cancel'), leftTheApp: theAppWasLeftByBack() }).toEqual({ cancelled: 1, leftTheApp: false })
  })

  it('closes the note of an article, which opens and closes through its own model', async () => {
    const dialog = await shownOnAStartedRouterAt('/catalog', () => import('../src/phone/components/catalog/ItemNoteDialog.vue'), {
      itemName: 'Bratwurst',
      isCorrecting: false,
      isOpen: false,
      note: '',
    })
    await dialog.setProps({ isOpen: true })
    await flushPromises()

    await pressBack()

    expect({ model: dialog.emitted('update:isOpen'), leftTheApp: theAppWasLeftByBack() }).toEqual({ model: [[false]], leftTheApp: false })
  })
})

describe('back while the station\'s hand-out confirmation is open', () => {
  it('cancels it and stays on the station screen', async () => {
    const dialog = await shownOnAStartedRouterAt('/stations', () => import('../src/station/components/StationDoneDialog.vue'), {
      tableName: '12',
      lines: [{ key: 'line-wasser', itemName: 'Wasser', note: null, isCancelled: false, units: 2 }],
    })

    await pressBack()

    expect({ cancelled: timesEmitted(dialog, 'cancelled'), leftTheApp: theAppWasLeftByBack() }).toEqual({ cancelled: 1, leftTheApp: false })
  })
})
