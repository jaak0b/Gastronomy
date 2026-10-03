import { expect, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'

export function testIdSelector(testId: string, id?: string): string {
  return id === undefined
    ? `[data-test="${testId}"]`
    : `[data-test="${testId}"][data-test-id="${id}"]`
}

export function onScreen(selector: string, root: ParentNode = document): HTMLElement {
  const found = root.querySelector<HTMLElement>(selector)
  if (found === null) {
    throw new Error(`nothing on screen matches ${selector}`)
  }
  return found
}

export function allOnScreen(selector: string, root: ParentNode = document): HTMLElement[] {
  return [...root.querySelectorAll<HTMLElement>(selector)]
}

export function isOnScreen(selector: string, root: ParentNode = document): boolean {
  return root.querySelector(selector) !== null
}

export function textOnScreen(selector: string, root: ParentNode = document): string {
  return root.querySelector(selector)?.textContent ?? ''
}

export function inputOf(field: HTMLElement | string): HTMLInputElement {
  const element = typeof field === 'string' ? onScreen(field) : field
  if (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) {
    return element as HTMLInputElement
  }
  return onScreen('input, textarea', element) as HTMLInputElement
}

export function typeInto(field: HTMLElement | string, value: string): void {
  const input = inputOf(field)
  input.value = value
  input.dispatchEvent(new Event('input', { bubbles: true }))
}

export async function typeIn(field: HTMLElement | string, value: string): Promise<void> {
  typeInto(field, value)
  await flushPromises()
}

export function leave(field: HTMLElement | string): void {
  inputOf(field).dispatchEvent(new FocusEvent('blur'))
}

export async function clickOn(selector: string, root: ParentNode = document): Promise<void> {
  onScreen(selector, root).click()
  await flushPromises()
}

export async function waitUntil(observation: () => void): Promise<void> {
  await vi.waitFor(observation)
}

export function openDialog(): Element | null {
  return document.querySelector('[data-test="confirm-dialog"]')
}

export async function waitForDialog(): Promise<void> {
  await waitUntil(() => expect(openDialog()).not.toBeNull())
}

export function dialogText(selector: string): string {
  return textOnScreen(selector)
}

export async function pressInDialog(selector: string): Promise<void> {
  await waitForDialog()
  onScreen(selector).click()
  await flushPromises()
}
