<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { localInputToUtcIso, utcIsoToLocalInput } from '../../../core/festivalTimes'
import type { AdminFestival, FestivalDraft } from '../../../stores/admin/festivals'

const props = defineProps<{
  festival: AdminFestival | null
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
  <v-dialog :model-value="true" max-width="560" persistent scrollable>
    <v-card class="festival-form" role="dialog" aria-modal="true">
      <v-card-title class="festival-form-title">{{ title }}</v-card-title>
      <v-card-text>
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
        <v-alert v-if="errorText !== null" class="error mt-4" type="error" variant="tonal">
          {{ errorText }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn class="cancel" variant="text" @click="emit('cancel')">
          {{ t('admin.cancel') }}
        </v-btn>
        <v-btn class="confirm" color="primary" :disabled="!isFilledIn" @click="save">
          {{ confirmLabel }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
