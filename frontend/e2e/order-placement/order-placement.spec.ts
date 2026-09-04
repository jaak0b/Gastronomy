import { expect, test } from '@playwright/test'

const BASE_URL = process.env.GASTRONOMY_E2E_BASE_URL
const ENROL_CODE = process.env.GASTRONOMY_E2E_ENROL_CODE ?? ''
const SKIP_REASON =
  'requires the backend from T002-T005 and a practice event session, per spec 11.3'

test('a server enrols a phone, builds an order, and sends it', async ({
  page,
}) => {
  test.skip(BASE_URL === undefined, SKIP_REASON)

  await page.goto(`${BASE_URL}/`)
  await expect(page.getByRole('heading')).toHaveText('Einrichten mit dem sechsstelligen Code')

  await page.locator('.code-field input').fill(ENROL_CODE)
  await page.locator('.name-field input').fill('Anna')
  await page.getByRole('button', { name: 'Weiter' }).click()

  await expect(page.locator('.basket-bar .summary')).toHaveText('Noch nichts ausgewählt')

  const bratwurst = page.locator('.item-row', { hasText: 'Bratwurst' })
  await bratwurst.locator('.add').click()
  await expect(bratwurst.locator('.count')).toHaveText('1')
  await bratwurst.locator('.add').click()
  await expect(bratwurst.locator('.count')).toHaveText('2')
  await expect(page.locator('.basket-bar .summary')).toHaveText('2 Artikel, 7,00 €')

  await page.getByRole('button', { name: 'Weiter zur Übersicht' }).click()
  await expect(page.locator('.table-field')).toHaveClass(/is-missing/)

  await page.locator('.table-field input').fill('Tisch 12')
  await expect(page.locator('.table-field')).not.toHaveClass(/is-missing/)
  await page.getByRole('button', { name: 'Weiter zur Übersicht' }).click()

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Bestellung prüfen')
  await expect(page.locator('.line-list .station-slip .station-name')).toHaveText('Geht an Küche')
  await expect(page.locator('.line-list .line .line-name')).toHaveText('2 x Bratwurst')
  await expect(page.locator('.table-name')).toHaveText('Tisch: Tisch 12')
  await expect(page.locator('.total-display .amount')).toHaveText('7,00 €')

  await page.locator('button.send').click()

  const confirmation = page.locator('.sent')
  await expect(confirmation).toHaveText(/^Bestellung \d+ ist angekommen\.$/)

  await page.goto(`${BASE_URL}/`)
  await expect(page.locator('.basket-bar .summary')).toHaveText('Noch nichts ausgewählt')
})
