import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import QrLanding from '../../../src/components/enrolment/QrLanding.vue'
import { TOKEN_STORAGE_KEY } from '../../../src/stores/session'
import { currentRoute, navigate } from '../../../src/router'
import { testPlugins } from '../../support/plugins'

function answerWith(status: number, body: object) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(body), { status })),
  )
}

function answerInTurn(...answers: { status: number; body: object }[]) {
  let next = 0
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      const answer = answers[Math.min(next, answers.length - 1)]
      next += 1
      return new Response(JSON.stringify(answer.body), { status: answer.status })
    }),
  )
}

function mountLanding() {
  return mount(QrLanding, {
    props: { code: 'abc123' },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

describe('landing on a QR code link', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/j/abc123')
  })

  it('opens the order screen when the code belongs to somebody already', async () => {
    answerWith(200, {
      deviceToken: 'token-1',
      staffMember: { id: 'staff-1', name: 'Anna' },
      language: 'de',
    })

    mountLanding()

    await vi.waitFor(() => expect(currentRoute.value).toEqual({ name: 'home' }))
  })

  it('opens the order screen once the name has been entered', async () => {
    answerInTurn(
      { status: 400, body: { code: 'ValidationFailed', messageKey: 'enrolment.nameMissing' } },
      {
        status: 200,
        body: {
          deviceToken: 'token-2',
          staffMember: { id: 'staff-2', name: 'Bernd' },
          language: 'de',
        },
      },
    )

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('.enrolment').exists()).toBe(true))

    await landing.get('.name-field input').setValue('Bernd')
    await landing.get('.continue').trigger('click')

    await vi.waitFor(() => expect(currentRoute.value).toEqual({ name: 'home' }))
  })

  it('asks for a name only when the laptop says the name is missing', async () => {
    answerWith(400, { code: 'ValidationFailed', messageKey: 'enrolment.nameMissing' })

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('.enrolment').exists()).toBe(true))

    expect(landing.find('.code-spent').exists()).toBe(false)
  })

  it('says a used code is used instead of asking for a name', async () => {
    answerWith(410, { code: 'EnrolmentCodeNoLongerValid', messageKey: 'enrolment.codeNoLongerValid' })

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('.code-spent').exists()).toBe(true))

    expect(landing.find('.enrolment').exists()).toBe(false)
    expect(landing.get('.code-spent').text()).toContain('Lassen Sie sich am Laptop')
  })

  it('offers to carry on when the phone is already set up', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-already-here')
    answerWith(410, { code: 'EnrolmentCodeNoLongerValid', messageKey: 'enrolment.codeNoLongerValid' })

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('.code-spent').exists()).toBe(true))

    expect(landing.get('.code-spent').text()).toContain('Sie können mit diesem Telefon weiterarbeiten')
    expect(landing.find('.carry-on').exists()).toBe(true)
  })
})
