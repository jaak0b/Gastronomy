import { onUnmounted } from 'vue'
import { assertNever } from '../../shared/core/assertNever'
import { navigate } from '../../shared/router/router'
import { useOpenItemsStore } from '../stores/openItems'
import { useOrderStore, type SettlingIntent } from '../stores/order'
import { useTablePlanStore } from '../stores/tablePlan'

export function useSendAndLeave() {
  const order = useOrderStore()
  const openItems = useOpenItemsStore()
  const tablePlan = useTablePlanStore()
  let isMounted = true

  onUnmounted(() => {
    isMounted = false
  })

  function openTheTableWithTheOrderTicked(): void {
    const acceptedForSettling = order.takeTheOrderAcceptedForSettling()
    if (acceptedForSettling !== null) {
      openItems.openTableAndSelectItemsOnceLoaded(
        acceptedForSettling.tableId,
        acceptedForSettling.itemIds,
      )
    }
    navigate('/open-items')
  }

  function leaveIfTheLaptopAccepted(): void {
    if (!isMounted || order.sendState !== 'accepted') {
      return
    }
    const intent: SettlingIntent = order.settlingIntent
    switch (intent) {
      case 'leaveOpen':
        tablePlan.closeArea()
        navigate('/')
        return
      case 'settleRightAway':
        openTheTableWithTheOrderTicked()
        return
      default:
        assertNever(intent)
    }
  }

  async function send(intent: SettlingIntent): Promise<void> {
    await order.send(intent)
    leaveIfTheLaptopAccepted()
  }

  async function sendAgain(): Promise<void> {
    await order.sendAgain()
    leaveIfTheLaptopAccepted()
  }

  return { send, sendAgain }
}
