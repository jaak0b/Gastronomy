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

function refuseEveryConnection() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      throw new TypeError('Failed to fetch')
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

async function failureNoticeOf(landing: ReturnType<typeof mountLanding>): Promise<string> {
  await vi.waitFor(() => expect(landing.find('.failure-notice').exists()).toBe(true))
  return landing.get('.failure-notice').text()
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

  it('leaves no way back to the used invitation once the phone is set up', async () => {
    answerWith(200, {
      deviceToken: 'token-3',
      staffMember: { id: 'staff-3', name: 'Carla' },
      language: 'de',
    })

    mountLanding()
    await vi.waitFor(() => expect(currentRoute.value).toEqual({ name: 'home' }))

    const popped = new Promise<void>((resolve) => {
      window.addEventListener('popstate', () => resolve(), { once: true })
    })
    window.history.back()
    await popped

    expect(window.location.pathname).toBe('/')
  })

  it('asks for a name when the invitation belongs to nobody yet', async () => {
    answerWith(400, { code: 'ValidationFailed', messageKey: 'enrolment.nameMissing' })

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('.enrolment').exists()).toBe(true))

    expect(landing.find('.failure-notice').exists()).toBe(false)
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

  it('tells the volunteer to wait when the laptop has too many requests at once', async () => {
    answerInTurn(
      { status: 400, body: { code: 'ValidationFailed', messageKey: 'enrolment.nameMissing' } },
      {
        status: 429,
        body: { code: 'TooManyRequests', messageKey: 'session.tooManyRequests', parameters: {} },
      },
    )

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('.enrolment').exists()).toBe(true))

    await landing.get('.name-field input').setValue('Bernd')
    await landing.get('.continue').trigger('click')

    await vi.waitFor(() => expect(landing.find('.error').exists()).toBe(true))
    expect(landing.get('.error').text()).toBe(
      'Warten Sie einen Moment und versuchen Sie es dann noch einmal. Der Laptop bekommt gerade zu viele Anfragen auf einmal.',
    )
  })

  it('says the code is no longer valid when the laptop refuses it', async () => {
    answerWith(410, { code: 'EnrolmentCodeNoLongerValid', messageKey: 'enrolment.codeNoLongerValid' })

    const notice = await failureNoticeOf(mountLanding())

    expect(notice).toContain('Dieser Code gilt nicht mehr.')
  })

  it('names the WiFi when the phone cannot reach the laptop', async () => {
    refuseEveryConnection()

    const notice = await failureNoticeOf(mountLanding())

    expect(notice).toContain('Prüfen Sie, ob Sie im WLAN des Festes sind.')
  })

  it('names the waiter being off the list when the laptop says so', async () => {
    answerWith(410, { code: 'StaffMemberIsOffTheList', messageKey: 'enrolment.staffMemberIsOffTheList' })

    const notice = await failureNoticeOf(mountLanding())

    expect(notice).toContain('Dieser Kellner steht nicht mehr in der Liste.')
  })

  it('reads the six digit code as wrong when the laptop knows no such code', async () => {
    answerWith(404, {})

    const notice = await failureNoticeOf(mountLanding())

    expect(notice).toContain('Dieser Code stimmt nicht.')
  })

  it('offers to carry on when the phone is already set up', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-already-here')
    answerWith(410, { code: 'EnrolmentCodeNoLongerValid', messageKey: 'enrolment.codeNoLongerValid' })

    const landing = mountLanding()
    const notice = await failureNoticeOf(landing)

    expect(notice).toContain('Dieser Code gilt nicht mehr.')
    expect(landing.get('.carry-on').text()).toContain('Mit diesem Telefon weiterarbeiten')
  })
})

describe('the length of the name a waiter types while enrolling', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/j/abc123')
  })

  it('stops where the laptop stops storing it', async () => {
    answerWith(400, { code: 'ValidationFailed', messageKey: 'enrolment.nameMissing' })

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('.enrolment').exists()).toBe(true))

    expect(landing.get('.name-field input').attributes('maxlength')).toBe('40')
  })
})
