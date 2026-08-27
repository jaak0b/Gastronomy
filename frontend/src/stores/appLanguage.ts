import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../api/client'
import type { AppLanguage } from '../core/apiTypes'

export const useAppLanguageStore = defineStore('appLanguage', () => {
  const language = ref<AppLanguage>('de')

  async function load(): Promise<void> {
    const result = await request<{ language: string }>('/api/language')
    if (result.kind === 'ok' && (result.data.language === 'de' || result.data.language === 'en')) {
      language.value = result.data.language
    }
  }

  return { language, load }
})
