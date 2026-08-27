import { watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAppLanguageStore } from './stores/appLanguage'

export function bindLocaleToLaptop(): void {
  const appLanguage = useAppLanguageStore()
  const { locale } = useI18n()
  watch(
    () => appLanguage.language,
    (next) => {
      locale.value = next
    },
    { immediate: true },
  )
  void appLanguage.load()
}
