import { expect, test } from '@playwright/test'

const BASE_URL = process.env.GASTRONOMY_E2E_BASE_URL
const ENROL_CODE = process.env.GASTRONOMY_E2E_ENROL_CODE ?? ''
const SKIP_REASON =
  'requires the backend from T002-T005 and a practice event session, per spec 11.3'

test('a server enrols a phone, builds an order, sends it, and finds it in the order list', async ({
  page,
}) => {
  test.skip(BASE_URL === undefined, SKIP_REASON)

  await page.goto(`${BASE_URL}/`)
  await expect(page.getByRole('heading')).toHaveText('Einrichten mit dem sechsstelligen Code')

  await page.locator('.code-field input').fill(ENROL_CODE)
  await page.locator('.name-field input').fill('Anna')
  await page.getByRole('button', { name: 'Weiter' }).click()

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Bestellung aufnehmen')
  await expect(page.locator('.basket-bar .summary')).toHaveText('Noch nichts ausgewählt')

  const bratwurst = page.locator('.item-button', { hasText: 'Bratwurst' })
  await bratwurst.locator('.add').click()
  await expect(bratwurst.locator('.quantity')).toHaveText('1')
  await bratwurst.locator('.add').click()
  await expect(bratwurst.locator('.quantity')).toHaveText('2')
  await expect(page.locator('.basket-bar .summary')).toHaveText('2 Artikel, 7,00 €')

  await page.getByRole('button', { name: 'Weiter zur Übersicht' }).click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Bestellung prüfen')
  await expect(page.locator('.line-list .group h3')).toHaveText('Geht an Küche')
  await expect(page.locator('.total-display .amount')).toHaveText('7,00 €')

  await expect(page.locator('.table-missing')).toHaveText(
    'Tragen Sie einen Tisch ein, bevor Sie senden.',
  )
  await expect(page.locator('button.send')).toBeDisabled()

  await page.locator('.table-field input').fill('Tisch 12')
  await expect(page.locator('button.send')).toBeEnabled()
  await page.locator('button.send').click()

  const confirmation = page.locator('.sent')
  await expect(confirmation).toHaveText(/^Bestellung \d+ ist angekommen\.$/)
  const orderNumber = ((await confirmation.textContent()) ?? '').replace(/\D/g, '')

  await page.goto(`${BASE_URL}/orders`)
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Meine Bestellungen')
  const orderRow = page.locator('.order-row', { hasText: `Bestellung ${orderNumber}, Tisch 12` })
  await expect(orderRow).toBeVisible()
  await expect(orderRow.locator('.status')).toHaveText(
    /^(Wird gedruckt|Gedruckt|Von der Ausgabestelle übernommen|Bitte prüfen)$/,
  )

  await orderRow.click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(`Bestellung ${orderNumber}`)
  await expect(page.locator('.ticket-chip .label').first()).toHaveText(/^Küche, Bon \d{3}$/)
  await expect(page.locator('.changed-mind')).toHaveText(
    'Sagen Sie an der Ausgabestelle Bescheid und nehmen Sie die Änderung als neue Bestellung auf. Eine gesendete Bestellung lässt sich hier nicht zurücknehmen, weil der Bon schon gedruckt wird.',
  )

  await page.goto(`${BASE_URL}/`)
  await expect(page.locator('.basket-bar .summary')).toHaveText('Noch nichts ausgewählt')
})
