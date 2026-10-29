import { assertNever } from '../../shared/core/assertNever'
import { navigate } from '../../shared/router/router'
import { useOpenItemsStore } from '../stores/openItems'
import { useOrderStore, type SettlingIntent } from '../stores/order'

export function useSendAndLeave() {
  const order = useOrderStore()
  const openItems = useOpenItemsStore()

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

  function leaveIfTheLaptopAccepted(): void {
    if (order.sendState !== 'accepted') {
      return
    }
    const intent: SettlingIntent = order.settlingIntent
    switch (intent) {
      case 'leaveOpen':
        navigate('/', 'redirect')
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
