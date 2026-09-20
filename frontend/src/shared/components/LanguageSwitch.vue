<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../core/apiTypes'

const props = defineProps<{ language: AppLanguage }>()
const emit = defineEmits<{ select: [language: AppLanguage] }>()

const { t } = useI18n()

const options = computed(() => [
  { value: 'de', title: t('language.german'), props: { class: 'option option-de' } },
  { value: 'en', title: t('language.english'), props: { class: 'option option-en' } },
])

function choose(language: AppLanguage): void {
  emit('select', language)
}
</script>

<template>
  <v-select
    class="language-switch"
    :model-value="props.language"
    :items="options"
    :aria-label="t('language.label')"
    variant="outlined"
    density="compact"
    hide-details
    @update:model-value="choose"
  />
</template>

<style scoped>
.language-switch {
  max-width: 10rem;
}
</style>
