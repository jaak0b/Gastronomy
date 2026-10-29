import { assertNever } from '../../shared/core/assertNever'
import { navigate } from '../../shared/router/router'
import { useOpenItemsStore } from '../stores/openItems'
import { useOrderStore, type SettlingIntent } from '../stores/order'
import { useTablePlanStore } from '../stores/tablePlan'

export function useSendAndLeave() {
  const order = useOrderStore()
  const openItems = useOpenItemsStore()
  const tablePlan = useTablePlanStore()

  function openTheTableWithTheOrderTicked(): void {
    const acceptedForSettling = order.takeTheOrderAcceptedForSettling()
    if (acceptedForSettling !== null) {
      openItems.openTableAndSelectItemsOnceLoaded(
        acceptedForSettling.tableId,
        acceptedForSettling.itemIds,
      )
    }
    navigate('/open-items', 'redirect')
  }

  function landOnThePlanOfTheTable(tableId: string | null): void {
    navigate('/', 'redirect')
    if (tableId !== null) {
      tablePlan.openTheAreaOfTheTable(tableId)
    }
  }

  function leaveIfTheLaptopAccepted(sentTableId: string | null): void {
    if (order.sendState !== 'accepted') {
      return
    }
    const intent: SettlingIntent = order.settlingIntent
    switch (intent) {
      case 'leaveOpen':
        landOnThePlanOfTheTable(sentTableId)
        return
      case 'settleRightAway':
        openTheTableWithTheOrderTicked()
        return
      default:
        assertNever(intent)
    }
  }

  async function send(intent: SettlingIntent): Promise<void> {
    const tableId = order.draft.table?.tableId ?? null
    await order.send(intent)
    leaveIfTheLaptopAccepted(tableId)
  }

  async function sendAgain(): Promise<void> {
    const tableId = order.draft.table?.tableId ?? null
    await order.sendAgain()
    leaveIfTheLaptopAccepted(tableId)
  }

  return { send, sendAgain }
}
