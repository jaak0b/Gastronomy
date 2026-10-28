import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import TablePlanEditor from '../../../../src/admin/components/tablePlan/TablePlanEditor.vue'
import { currentRoute, navigate } from '../../../../src/shared/router/router'
import { clickOn, textOnScreen, waitUntil } from '../../../support/dom'
import { useConnectionStore } from '../../../../src/shared/stores/connection'
import { aHold, answer, heldUntil, inTurn, stubLaptop } from '../../../support/laptop'
import { testPlugins } from '../../../support/plugins'
import { FESTIVAL_ID, HALL, PLAN_PATH, TENT, editorLaptop, mountEditor } from './tablePlanEditorFixture'

enableAutoUnmount(afterEach)

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
  navigate(`/admin/festivals/${FESTIVAL_ID}/areas/area-halle`)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the editor of one area', () => {
  it('names the area as its heading', async () => {
    editorLaptop()

    await mountEditor()

    expect(textOnScreen('[data-test="area-name"]').trim()).toBe('Halle')
  })

  it('shows the area it was opened for, not the first one of the festival', async () => {
    editorLaptop()

    await mountEditor('de', [], 'area-zelt')

    expect(textOnScreen('[data-test="area-name"]').trim()).toBe('Zelt')
  })

  it('leads back to the festival page', async () => {
    editorLaptop()
    await mountEditor()

    await clickOn('[data-test="back-to-festival"]')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'festivals', festivalId: FESTIVAL_ID })
  })

  it('returns to the festival page for an area the festival does not have', async () => {
    editorLaptop()

    mount(TablePlanEditor, {
      props: { festivalId: FESTIVAL_ID, areaId: 'area-unknown' },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })

    await waitUntil(() =>
      expect(currentRoute.value).toEqual({ name: 'admin', section: 'festivals', festivalId: FESTIVAL_ID }),
    )
  })

  it('stays on the area when a newer read of the plan overtakes the first one', async () => {
    const firstRead = aHold()
    const newerRead = aHold()
    const plan = answer({ areas: [HALL, TENT] })
    const laptop = editorLaptop({ planReply: inTurn(heldUntil(firstRead.released, plan), heldUntil(newerRead.released, plan)) })
    mount(TablePlanEditor, {
      props: { festivalId: FESTIVAL_ID, areaId: 'area-halle' },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await waitUntil(() => expect(laptop.urls()).toContain(PLAN_PATH))
    const refetch = useConnectionStore().refetchAll()

    firstRead.release()
    await flushPromises()
    newerRead.release()
    await refetch

    await waitUntil(() => expect(textOnScreen('[data-test="area-name"]').trim()).toBe('Halle'))
    expect(currentRoute.value).toEqual({
      name: 'admin',
      section: 'festivals',
      festivalId: FESTIVAL_ID,
      areaId: 'area-halle',
    })
  })

  it('stays on the page when the plan cannot be read', async () => {
    const laptop = editorLaptop({ planReply: answer({ message: 'kaputt' }, 500) })

    mount(TablePlanEditor, {
      props: { festivalId: FESTIVAL_ID, areaId: 'area-halle' },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })

    await waitUntil(() => expect(laptop.urls()).toContain(PLAN_PATH))
    await flushPromises()
    expect(currentRoute.value).toEqual({
      name: 'admin',
      section: 'festivals',
      festivalId: FESTIVAL_ID,
      areaId: 'area-halle',
    })
  })

  it('returns to the overview for a festival the laptop does not know, as the festival page does', async () => {
    stubLaptop().answersEverythingElse(answer({ festivals: [] }))

    mount(TablePlanEditor, {
      props: { festivalId: 'fest-unknown', areaId: 'area-halle' },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await flushPromises()

    await waitUntil(() =>
      expect(currentRoute.value).toEqual({ name: 'admin', section: 'overview', festivalId: null }),
    )
  })
})
