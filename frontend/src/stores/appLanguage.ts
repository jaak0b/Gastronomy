import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../shared/api/client'
import { languageSchema } from '../shared/api/apiSchemas'
import type { AppLanguage } from '../shared/api/apiTypes'

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
