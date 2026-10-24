import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import TablePlanEditor from '../../../../src/admin/components/tablePlan/TablePlanEditor.vue'
import { currentRoute, navigate } from '../../../../src/shared/router/router'
import { clickOn, textOnScreen, waitUntil } from '../../../support/dom'
import { answer, stubLaptop } from '../../../support/laptop'
import { testPlugins } from '../../../support/plugins'
import { FESTIVAL_ID, editorLaptop, mountEditor } from './tablePlanEditorFixture'

enableAutoUnmount(afterEach)

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
  navigate(`/admin/festivals/${FESTIVAL_ID}/table-plan`)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the table plan editor', () => {
  it('names the festival whose plan it shows', async () => {
    editorLaptop()

    await mountEditor()

    expect(textOnScreen('[data-test="festival-name"]').trim()).toBe('Sommerfest')
  })

  it('leads back to the festival page', async () => {
    editorLaptop()
    await mountEditor()

    await clickOn('[data-test="back-to-festival"]')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'festivals', festivalId: FESTIVAL_ID })
  })

  it('asks for an area first when the festival has none', async () => {
    editorLaptop({ areas: [] })

    mount(TablePlanEditor, {
      props: { festivalId: FESTIVAL_ID },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })

    await waitUntil(() =>
      expect(textOnScreen('[data-test="no-area"]').trim()).toBe('Legen Sie zuerst einen Bereich an.'),
    )
  })

  it('returns to the overview for a festival the laptop does not know, as the festival page does', async () => {
    stubLaptop().answersEverythingElse(answer({ festivals: [] }))

    mount(TablePlanEditor, {
      props: { festivalId: 'fest-unknown' },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await flushPromises()

    await waitUntil(() =>
      expect(currentRoute.value).toEqual({ name: 'admin', section: 'overview', festivalId: null }),
    )
  })
})
