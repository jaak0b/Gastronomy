import { watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSessionStore } from './stores/session'

export function bindLocaleToSession(): void {
  const session = useSessionStore()
  const { locale } = useI18n()
  watch(
    () => session.language,
    (next) => {
      locale.value = next
    },
    { immediate: true },
  )
}
