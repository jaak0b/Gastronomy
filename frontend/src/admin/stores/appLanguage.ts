import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../../shared/api/client'
import { LanguageView } from '../../shared/api/generatedSchemas'
import { appLanguageSchema, type AppLanguage } from '../../shared/core/deviceLanguage'

const languageAnswerSchema = LanguageView.extend({ language: appLanguageSchema })

export const useAppLanguageStore = defineStore('appLanguage', () => {
  const language = ref<AppLanguage>('de')

  async function load(): Promise<void> {
    const result = await request('/api/language', { schema: languageAnswerSchema })
    if (result.kind === 'ok') {
      language.value = result.data.language
    }
  }

  return { language, load }
})
