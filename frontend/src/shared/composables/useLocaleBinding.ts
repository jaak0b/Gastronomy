import { toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../api/apiTypes'

export function useLocaleBinding(language: MaybeRefOrGetter<AppLanguage>): void {
  const { locale } = useI18n()
  watch(
    () => toValue(language),
    (next) => {
      locale.value = next
    },
    { immediate: true },
  )
}
