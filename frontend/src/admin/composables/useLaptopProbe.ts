import { requestAction } from '../../shared/api/client'

export function useLaptopProbe() {
  async function isThisTheLaptop(): Promise<boolean> {
    const result = await requestAction('/api/admin/festivals')
    return !(result.kind === 'error' && result.status === 404)
  }

  return { isThisTheLaptop }
}
