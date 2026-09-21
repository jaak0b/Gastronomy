<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { AdminFestivalView } from '../../../shared/api/generatedSchemas'
import { localInputToUtcIso, utcIsoToLocalInput } from '../../core/festivalTimes'
import type { FestivalDraft } from '../../stores/festivals'
import BaseFormDialog from '../BaseFormDialog.vue'

const props = defineProps<{
  festival: AdminFestivalView | null
  title: string
  confirmLabel: string
  errorText: string | null
}>()
const emit = defineEmits<{ save: [draft: FestivalDraft]; cancel: [] }>()

const { t } = useI18n()
const name = ref(props.festival?.name ?? '')
const startsAt = ref(
  props.festival === null ? '' : utcIsoToLocalInput(props.festival.startsAtUtc),
)
const endsAt = ref(props.festival === null ? '' : utcIsoToLocalInput(props.festival.endsAtUtc))

const isFilledIn = computed(
  () =>
    name.value.trim().length > 0
    && localInputToUtcIso(startsAt.value) !== null
    && localInputToUtcIso(endsAt.value) !== null,
)

function save(): void {
  const startsAtUtc = localInputToUtcIso(startsAt.value)
  const endsAtUtc = localInputToUtcIso(endsAt.value)
  if (startsAtUtc === null || endsAtUtc === null) {
    return
  }
  emit('save', { name: name.value, startsAtUtc, endsAtUtc })
}
</script>

<template>
  <BaseFormDialog
    :title="title"
    :error-text="errorText"
    :save-label="confirmLabel"
    :save-disabled="!isFilledIn"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="festival-name-field mb-4"
      maxlength="80"
      :label="t('admin.festivals.name')"
    />
    <v-text-field
      v-model="startsAt"
      class="festival-start-field mb-4"
      type="datetime-local"
      :label="t('admin.festivals.start')"
    />
    <v-text-field
      v-model="endsAt"
      class="festival-end-field"
      type="datetime-local"
      :label="t('admin.festivals.end')"
    />
  </BaseFormDialog>
</template>
