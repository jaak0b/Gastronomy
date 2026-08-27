<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminPeopleStore } from '../../../stores/admin/people'
import ConfirmDialog from '../ConfirmDialog.vue'
import InvitationPanel from './InvitationPanel.vue'

const { t } = useI18n()
const people = useAdminPeopleStore()
const renamingId = ref<string | null>(null)
const revokedIds = ref<string[]>([])
const newName = ref('')
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)

const shown = computed(() =>
  people.people.filter((person) => showsDeactivated.value || person.isActive),
)

async function deactivate(): Promise<void> {
  const serverPersonId = askingAboutId.value
  askingAboutId.value = null
  if (serverPersonId !== null) {
    await people.setActive(serverPersonId, false)
  }
}

async function revoke(id: string): Promise<void> {
  const revoked = await people.revokeDevice(id)
  if (revoked) {
    revokedIds.value = [...revokedIds.value, id]
  }
}

async function rename(id: string): Promise<void> {
  const renamed = await people.rename(id, newName.value.trim())
  if (!renamed) {
    return
  }
  renamingId.value = null
  newName.value = ''
}

onMounted(async () => {
  people.listen()
  await people.load()
})
</script>

<template>
  <v-container class="admin-people">
    <h1 class="text-h5 mb-2">{{ t('admin.people.title') }}</h1>
    <p class="help text-medium-emphasis mb-4">{{ t('admin.people.help') }}</p>

    <v-alert v-if="people.enrolledName !== null" class="enrolled mb-4" type="success" variant="tonal">
      {{ t('admin.enrol.done', { name: people.enrolledName }) }}
    </v-alert>
    <v-alert v-if="people.errorMessage !== null" class="refusal mb-4" type="warning" variant="tonal">
      {{
        people.errorMessage.count === null
          ? t(people.errorMessage.key, people.errorMessage.parameters)
          : t(people.errorMessage.key, people.errorMessage.parameters, people.errorMessage.count)
      }}
    </v-alert>
    <v-alert v-if="people.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>
    <v-alert v-else-if="people.people.length === 0" class="empty" type="info" variant="tonal">
      {{ t('admin.people.empty') }}
    </v-alert>

    <v-checkbox
      v-model="showsDeactivated"
      class="show-deactivated"
      :label="t('admin.showDeactivated')"
    />

    <v-card v-for="person in shown" :key="person.serverPersonId" class="person-row mb-3">
      <v-card-item>
        <v-card-title class="name">
          {{ person.name }}
          <v-chip v-if="!person.isActive" class="deactivated ms-2" size="small" color="grey">
            {{ t('admin.deactivated') }}
          </v-chip>
          <v-chip v-if="!person.hasDevice" class="no-phone ms-2" size="small" color="warning">
            {{ t('admin.people.noPhone') }}
          </v-chip>
        </v-card-title>
        <v-card-subtitle v-if="person.hasDevice && person.lastSeenAtUtc !== null" class="last-seen">
          {{ t('admin.people.lastSeen', { time: person.lastSeenAtUtc }) }}
        </v-card-subtitle>
      </v-card-item>
      <v-card-text v-if="person.hasDevice">
        <p class="new-code-effect text-medium-emphasis">
          {{ t('admin.people.newCodeEffect', { name: person.name }) }}
        </p>
        <p class="revoke-confirm text-medium-emphasis">
          {{ t('admin.people.revokeConfirm', { name: person.name }) }}
        </p>
      </v-card-text>
      <v-card-text v-if="renamingId === person.serverPersonId">
        <v-text-field v-model="newName" class="rename-field" :label="t('admin.people.rename')" />
        <p class="help text-medium-emphasis">{{ t('admin.people.renameHelp') }}</p>
        <v-btn class="save-name" color="primary" @click="rename(person.serverPersonId)">
          {{ t('admin.save') }}
        </v-btn>
      </v-card-text>
      <v-card-actions>
        <v-btn
          class="new-code"
          variant="text"
          @click="people.createInvitation(person.serverPersonId)"
        >
          {{ t('admin.people.newCode') }}
        </v-btn>
        <v-btn
          class="rename"
          variant="text"
          @click="
            () => {
              renamingId = person.serverPersonId
              newName = person.name
            }
          "
        >
          {{ t('admin.people.rename') }}
        </v-btn>
        <v-btn
          v-if="person.hasDevice"
          class="revoke"
          variant="text"
          @click="revoke(person.serverPersonId)"
        >
          {{ t('admin.people.revoke') }}
        </v-btn>
        <v-chip v-if="revokedIds.includes(person.serverPersonId)" class="revoked" size="small">
          {{ t('admin.people.revoked') }}
        </v-chip>
        <v-btn
          v-if="person.isActive"
          class="deactivate"
          icon="mdi-delete"
          variant="text"
          color="error"
          :aria-label="t('admin.deactivate')"
          @click="askingAboutId = person.serverPersonId"
        />
        <v-btn
          v-else
          class="reactivate"
          variant="text"
          @click="people.setActive(person.serverPersonId, true)"
        >
          {{ t('admin.people.activate') }}
        </v-btn>
      </v-card-actions>
    </v-card>

    <v-btn class="new-person" color="primary" @click="people.createInvitation()">
      {{ t('admin.people.new') }}
    </v-btn>

    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.people.deactivateTitle')"
      :body="t('admin.people.deactivateBody')"
      :confirm-label="t('admin.people.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
    <InvitationPanel
      v-if="people.invitation !== null"
      :invitation="people.invitation"
      @close="people.closeInvitation"
    />
  </v-container>
</template>
