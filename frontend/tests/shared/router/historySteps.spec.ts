import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import {
  aBrowserThatCountsTheTap,
  aBrowserWithoutUserActivation,
  aTabThatOpenedTheAppAt,
  pressBack,
  theAppWasLeftByBack,
  theNextMoveThroughTheHistory,
} from '../../support/browserHistory'
import { waitUntil } from '../../support/dom'

const FESTIVAL_LIST = { name: 'admin', section: 'festivals', festivalId: null }

async function theRouterOfAnOpenedTab() {
  const router = await import('../../../src/shared/router/router')
  const { markBackButtonTrapActive } = await import('../../../src/shared/router/backButtonTrap')
  router.startRouter()
  markBackButtonTrapActive(router.currentRoute.value)
  return router
}

beforeEach(() => {
  vi.resetModules()
  aTabThatOpenedTheAppAt('/admin/festivals')
  aBrowserThatCountsTheTap()
})

afterEach(() => {
  aBrowserWithoutUserActivation()
})

describe('a tap that leads one level deeper', () => {
  it('adds one step, so the back button returns to the screen the tap left', async () => {
    const { currentRoute, navigate } = await theRouterOfAnOpenedTab()
    navigate('/admin/festivals/fest-1')
    await flushPromises()

    await pressBack()

    expect(currentRoute.value).toEqual(FESTIVAL_LIST)
  })

  it('replaces the entry in a browser without the user activation API, so back reaches the doors', async () => {
    aBrowserWithoutUserActivation()
    const { navigate } = await theRouterOfAnOpenedTab()
    navigate('/admin/festivals/fest-1')
    await flushPromises()

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })
})

describe('a tap after a back press', () => {
  it('adds no step until the next tap, because the browser no longer counts the earlier one', async () => {
    const { navigate } = await theRouterOfAnOpenedTab()
    navigate('/admin/festivals/fest-1')
    await flushPromises()
    await pressBack()
    navigate('/admin/festivals/fest-2')
    await flushPromises()

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })

  it('adds a step again once the next tap came', async () => {
    const { currentRoute, navigate } = await theRouterOfAnOpenedTab()
    navigate('/admin/festivals/fest-1')
    await flushPromises()
    await pressBack()
    document.body.click()
    navigate('/admin/festivals/fest-2')
    await flushPromises()

    await pressBack()

    expect(currentRoute.value).toEqual(FESTIVAL_LIST)
  })
})

describe('a redirect', () => {
  it('replaces the entry even right after a tap, so back does not return to the screen it left', async () => {
    const { navigate } = await theRouterOfAnOpenedTab()
    navigate('/admin/festivals/fest-1', 'redirect')
    await flushPromises()

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })
})

describe('a move up to a screen the history holds', () => {
  it('adds no step, so back from there goes further up', async () => {
    const { navigate } = await theRouterOfAnOpenedTab()
    navigate('/admin/festivals/fest-1')
    await flushPromises()
    const theWayUp = theNextMoveThroughTheHistory()
    navigate('/admin/festivals')
    await theWayUp

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })
})

describe('a step opened on a screen', () => {
  it('is closed by back, and the screen stays', async () => {
    const { currentRoute, openStep, openStepValue } = await theRouterOfAnOpenedTab()
    openStep({ name: 'area', value: 'area-halle', kind: 'level' })
    await flushPromises()

    await pressBack()

    expect({ route: currentRoute.value, area: openStepValue('area') }).toEqual({ route: FESTIVAL_LIST, area: null })
  })

  it('closes the dialog it belongs to when back takes it away', async () => {
    const { openStep } = await theRouterOfAnOpenedTab()
    const close = vi.fn()
    openStep({ name: 'dialog-1', value: 'dialog-1', kind: 'dialog' }, { close, refusesBack: () => false })
    await flushPromises()

    await pressBack()

    expect(close).toHaveBeenCalledTimes(1)
  })

  it('takes its entry away again when its own button closes it, so the next back leaves the screen', async () => {
    const { closeStep, openStep } = await theRouterOfAnOpenedTab()
    openStep({ name: 'dialog-1', value: 'dialog-1', kind: 'dialog' })
    await flushPromises()
    const theDialogsEntryIsGone = theNextMoveThroughTheHistory()
    closeStep('dialog-1')
    await theDialogsEntryIsGone

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })
})

describe('the address the doors return to', () => {
  it('is the address of the screen a tapped move opened', async () => {
    const { navigate } = await theRouterOfAnOpenedTab()

    navigate('/admin/festivals/fest-1')
    await flushPromises()

    expect(sessionStorage.getItem('theDoorTarget')).toBe('/admin/festivals/fest-1')
  })

  it('is the address of the screen a way up returned to', async () => {
    const { navigate } = await theRouterOfAnOpenedTab()
    navigate('/admin/festivals/fest-1')
    await flushPromises()

    const theWayUp = theNextMoveThroughTheHistory()
    navigate('/admin/festivals')
    await theWayUp
    await flushPromises()

    expect(sessionStorage.getItem('theDoorTarget')).toBe('/admin/festivals')
  })
})

describe('a forward press after a way up', () => {
  it('never shows the screen the way up left', async () => {
    const { currentRoute, navigate } = await theRouterOfAnOpenedTab()
    navigate('/admin/festivals/fest-1')
    await flushPromises()
    const theWayUp = theNextMoveThroughTheHistory()
    navigate('/admin/festivals')
    await theWayUp

    const theWayForward = theNextMoveThroughTheHistory()
    window.history.forward()
    await theWayForward
    await flushPromises()

    expect(currentRoute.value).toEqual(FESTIVAL_LIST)
  })
})

describe('a lower dialog that closes after a higher one opened on it', () => {
  it('leaves no dead step, so back closes the higher one and the next back leaves the first screen', async () => {
    const { closeStep, openStep } = await theRouterOfAnOpenedTab()
    openStep({ name: 'dialog-lower', value: 'dialog-lower', kind: 'dialog' }, { close: () => undefined, refusesBack: () => false })
    await flushPromises()
    const theEntryOfTheLowerDialog: unknown = window.history.state
    openStep({ name: 'dialog-higher', value: 'dialog-higher', kind: 'dialog' }, { close: () => undefined, refusesBack: () => false })
    await flushPromises()
    closeStep('dialog-lower')
    await waitUntil(() => expect(window.history.state).toEqual(theEntryOfTheLowerDialog))

    await pressBack()
    await pressBack()

    await waitUntil(() => expect(theAppWasLeftByBack()).toBe(true))
  })
})
