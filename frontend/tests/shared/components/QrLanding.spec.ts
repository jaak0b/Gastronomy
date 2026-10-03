import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import QrLanding from '../../../src/shared/components/QrLanding.vue'
import { useLocaleBinding } from '../../../src/shared/composables/useLocaleBinding'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { navigate, startOverAt } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { stubLaptop, answer, noConnection, inTurn } from '../../support/laptop'

vi.mock('../../../src/shared/router/router', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../../src/shared/router/router')>()),
  startOverAt: vi.fn(),
}))

function answerWith(status: number, body: object): void {
  stubLaptop().answersEverythingElse(answer(body, status))
}

function answerInTurn(...answers: { status: number; body: object }[]): void {
  stubLaptop().answersEverythingElse(inTurn(...answers.map(({ status, body }) => answer(body, status))))
}

function refuseEveryConnection() {
  stubLaptop().answersEverythingElse(noConnection())
}

function mountLanding() {
  return mount(QrLanding, {
    props: { code: 'abc123' },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

const QrLandingFollowingTheLanguage = defineComponent({
  components: { QrLanding },
  setup() {
    const session = useSessionStore()
    useLocaleBinding(() => session.language)
  },
  template: '<QrLanding code="abc123" />',
})

function mountLandingFollowingTheChosenLanguage() {
  localStorage.setItem('language', 'de')
  return mount(QrLandingFollowingTheLanguage, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

async function failureNoticeOf(landing: ReturnType<typeof mountLanding>): Promise<string> {
  await vi.waitFor(() => expect(landing.find('[data-test="failure-notice"]').exists()).toBe(true))
  return landing.get('[data-test="failure-notice"]').text()
}

describe('landing on a QR code link', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    vi.mocked(startOverAt).mockClear()
    navigate('/j/abc123')
  })

  it('starts the app over on the order screen when the code belongs to somebody already', async () => {
    answerWith(200, {
      deviceId: 'device-1',
      deviceToken: 'token-1',
      staffMember: { id: 'staff-1', name: 'Anna' },
      station: null,
      language: 'de',
    })

    mountLanding()

    await vi.waitFor(() => expect(vi.mocked(startOverAt)).toHaveBeenCalledWith('/'))
  })

  it('asks for a name when the invitation belongs to nobody yet', async () => {
    answerWith(400, { code: 'ValidationFailed', messageKey: 'errors.enrolment.nameMissing', parameters: {}, details: null })

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('[data-test="enrolment"]').exists()).toBe(true))

    expect(landing.find('[data-test="failure-notice"]').exists()).toBe(false)
  })

  it('starts the app over once the name has been entered', async () => {
    answerInTurn(
      { status: 400, body: { code: 'ValidationFailed', messageKey: 'errors.enrolment.nameMissing', parameters: {}, details: null } },
      {
        status: 200,
        body: {
          deviceId: 'device-2',
          deviceToken: 'token-2',
          staffMember: { id: 'staff-2', name: 'Bernd' },
          station: null,
          language: 'de',
        },
      },
    )

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('[data-test="enrolment"]').exists()).toBe(true))

    await landing.get('[data-test="name-field"] input').setValue('Bernd')
    await landing.get('[data-test="continue"]').trigger('click')

    await vi.waitFor(() => expect(vi.mocked(startOverAt)).toHaveBeenCalledWith('/'))
  })

  it('tells the volunteer to wait when the laptop has too many requests at once', async () => {
    answerInTurn(
      { status: 400, body: { code: 'ValidationFailed', messageKey: 'errors.enrolment.nameMissing', parameters: {}, details: null } },
      {
        status: 429,
        body: {
          code: 'TooManyRequests',
          messageKey: 'errors.session.tooManyRequests',
          parameters: {},
          details: null,
        },
      },
    )

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('[data-test="enrolment"]').exists()).toBe(true))

    await landing.get('[data-test="name-field"] input').setValue('Bernd')
    await landing.get('[data-test="continue"]').trigger('click')

    await vi.waitFor(() => expect(landing.find('[data-test="redeem-error"]').exists()).toBe(true))
    expect(landing.get('[data-test="redeem-error"]').text()).toBe(
      'Warten Sie einen Moment und versuchen Sie es dann noch einmal. Der Rechner bekommt gerade zu viele Anfragen auf einmal.',
    )
  })

  it('says the code is no longer valid when the laptop refuses it', async () => {
    answerWith(410, { code: 'EnrolmentCodeNoLongerValid', messageKey: 'errors.enrolment.codeNoLongerValid', parameters: {}, details: null })

    const notice = await failureNoticeOf(mountLanding())

    expect(notice).toContain('Dieser Code gilt nicht mehr.')
  })

  it('names the WiFi when the phone cannot reach the laptop', async () => {
    refuseEveryConnection()

    const notice = await failureNoticeOf(mountLanding())

    expect(notice).toContain('Prüfen Sie, ob Sie im WLAN des Festes sind.')
  })

  it('names the waiter being off the list when the laptop says so', async () => {
    answerWith(410, { code: 'StaffMemberIsOffTheList', messageKey: 'errors.enrolment.staffMemberIsOffTheList', parameters: {}, details: null })

    const notice = await failureNoticeOf(mountLanding())

    expect(notice).toContain('Dieser Kellner steht nicht mehr in der Liste.')
  })

  it('reads the six digit code as wrong when the laptop knows no such code', async () => {
    answerWith(404, {})

    const notice = await failureNoticeOf(mountLanding())

    expect(notice).toContain('Dieser Code stimmt nicht.')
  })

  it('offers to carry on when the phone is already set up', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.already-here')
    answerWith(410, { code: 'EnrolmentCodeNoLongerValid', messageKey: 'errors.enrolment.codeNoLongerValid', parameters: {}, details: null })

    const landing = mountLanding()
    const notice = await failureNoticeOf(landing)

    expect(notice).toContain('Dieser Code gilt nicht mehr.')
    expect(landing.get('[data-test="carry-on"]').text()).toContain('Mit diesem Telefon weiterarbeiten')
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
    answerWith(400, { code: 'ValidationFailed', messageKey: 'errors.enrolment.nameMissing', parameters: {}, details: null })

    const landing = mountLanding()
    await vi.waitFor(() => expect(landing.find('[data-test="enrolment"]').exists()).toBe(true))

    expect(landing.get('[data-test="name-field"] input').attributes('maxlength')).toBe('40')
  })
})

describe('the language picker on the QR landing', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/j/abc123')
  })

  it('writes the enrolment screen in the language the reader chooses', async () => {
    answerWith(400, { code: 'ValidationFailed', messageKey: 'errors.enrolment.nameMissing', parameters: {}, details: null })

    const landing = mountLandingFollowingTheChosenLanguage()
    await vi.waitFor(() => expect(landing.find('[data-test="enrolment"]').exists()).toBe(true))

    expect(landing.get('h1').text()).toBe('Dieses Telefon einrichten')

    await landing.get('[data-test="language-switch"] .v-field').trigger('mousedown')
    await vi.waitFor(() => expect(document.querySelector('[data-test="option-en"]')).not.toBeNull())
    ;(document.querySelector('[data-test="option-en"]') as HTMLElement).click()
    await flushPromises()

    expect(landing.get('h1').text()).toBe('Set up this phone')
  })
})
