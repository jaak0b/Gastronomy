import { emptyDraft, saveDraft, type DraftTable } from '../../src/phone/core/draftCart'

export const TISCH_3: DraftTable = { tableId: 'table-3', name: 'Tisch 3' }

export function theWaiterTakesTheOrderAt(table: DraftTable): void {
  saveDraft({ ...emptyDraft(), table })
}
