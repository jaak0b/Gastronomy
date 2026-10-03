import { onUnmounted } from 'vue'
import { assertNever } from '../../shared/core/assertNever'
import { navigate } from '../../shared/router/router'
import { useOpenItemsStore } from '../stores/openItems'
import { useOrderStore, type SettlingIntent } from '../stores/order'

function pageAfterSending(intent: SettlingIntent): string {
  switch (intent) {
    case 'leaveOpen':
      return '/'
    case 'settleRightAway':
      return '/open-items'
    default:
      return assertNever(intent)
  }
}

export function useSendAndLeave() {
  const order = useOrderStore()
  const openItems = useOpenItemsStore()
  let isMounted = true

  onUnmounted(() => {
    isMounted = false
  })

  function leaveIfTheLaptopAccepted(): void {
    if (!isMounted || order.sendState !== 'accepted') {
      return
    }
    const acceptedForSettling = order.takeTheOrderAcceptedForSettling()
    if (acceptedForSettling !== null) {
      openItems.openTableAndSelectItemsOnceLoaded(
        acceptedForSettling.tableName,
        acceptedForSettling.itemIds,
      )
    }
    navigate(pageAfterSending(order.settlingIntent))
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
