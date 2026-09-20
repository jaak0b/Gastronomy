import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../api/client'
import { languageSchema } from '../core/apiSchemas'
import type { AppLanguage } from '../core/apiTypes'

export const useAppLanguageStore = defineStore('appLanguage', () => {
  const language = ref<AppLanguage>('de')

  async function load(): Promise<void> {
    const result = await request('/api/language', { schema: languageSchema })
    if (result.kind === 'ok') {
      language.value = result.data.language
    }
  }

  return { language, load }
})
