import { expect, vi } from 'vitest'
import { createVuetify } from 'vuetify'
import { createI18n } from 'vue-i18n'
import de from '../../src/locales/de.json'
import en from '../../src/locales/en.json'

export function testPlugins(locale: 'de' | 'en' = 'de') {
  return [createI18n({ legacy: false, locale, messages: { de, en } }), createVuetify()]
}

export function openDialog(): Element | null {
  return document.querySelector('.confirm-dialog')
}

export async function waitForDialog(): Promise<void> {
  await vi.waitFor(() => expect(openDialog()).not.toBeNull())
}

export function dialogText(selector: string): string {
  return document.querySelector(selector)?.textContent ?? ''
}

export async function pressInDialog(selector: string): Promise<void> {
  await waitForDialog()
  ;(document.querySelector(selector) as HTMLElement).click()
  await vi.waitFor(() => expect(true).toBe(true))
}
