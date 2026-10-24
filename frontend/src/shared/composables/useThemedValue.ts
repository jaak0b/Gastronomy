import { computed, type ComputedRef } from 'vue'
import { useTheme } from 'vuetify'

export function useThemedValue<T>(lightValue: T, darkValue: T): ComputedRef<T> {
  const appTheme = useTheme()
  return computed(() => (appTheme.current.value.dark ? darkValue : lightValue))
}
