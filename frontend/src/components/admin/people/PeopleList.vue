<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminPeopleStore } from '../../../stores/admin/people'
import InvitationPanel from './InvitationPanel.vue'

const { t } = useI18n()
const people = useAdminPeopleStore()
const renamingId = ref<string | null>(null)
const revokedIds = ref<string[]>([])
const newName = ref('')

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
  <section class="admin-people">
    <h1>{{ t('admin.people.title') }}</h1>
    <p class="help">{{ t('admin.people.help') }}</p>
    <p v-if="people.enrolledName !== null" class="enrolled">
      {{ t('admin.enrol.done', { name: people.enrolledName }) }}
    </p>
    <p v-if="people.errorMessage !== null" class="refusal error">
      {{
        people.errorMessage.count === null
          ? t(people.errorMessage.key, people.errorMessage.parameters)
          : t(people.errorMessage.key, people.errorMessage.parameters, people.errorMessage.count)
      }}
    </p>
    <p v-if="people.loadFailed" class="error">{{ t('admin.loadFailed') }}</p>
    <p v-else-if="people.people.length === 0" class="empty">{{ t('admin.people.empty') }}</p>
    <ul>
      <li v-for="person in people.people" :key="person.serverPersonId">
        <span class="name">{{ person.name }}</span>
        <span v-if="!person.isActive" class="off-the-list">{{ t('admin.people.offTheList') }}</span>
        <span v-if="!person.hasDevice" class="no-phone">{{ t('admin.people.noPhone') }}</span>
        <span v-else-if="person.lastSeenAtUtc !== null" class="last-seen">
          {{ t('admin.people.lastSeen', { time: person.lastSeenAtUtc }) }}
        </span>
        <button type="button" class="new-code" @click="people.createInvitation(person.serverPersonId)">
          {{ t('admin.people.newCode') }}
        </button>
        <p v-if="person.hasDevice" class="new-code-effect">
          {{ t('admin.people.newCodeEffect', { name: person.name }) }}
        </p>
        <button
          type="button"
          class="rename"
          @click="
            () => {
              renamingId = person.serverPersonId
              newName = person.name
            }
          "
        >
          {{ t('admin.people.rename') }}
        </button>
        <template v-if="renamingId === person.serverPersonId">
          <input v-model="newName" type="text" />
          <button type="button" @click="rename(person.serverPersonId)">{{ t('admin.save') }}</button>
          <p class="help">{{ t('admin.people.renameHelp') }}</p>
        </template>
        <button
          v-if="person.hasDevice"
          type="button"
          class="revoke"
          @click="revoke(person.serverPersonId)"
        >
          {{ t('admin.people.revoke') }}
        </button>
        <span v-if="revokedIds.includes(person.serverPersonId)" class="revoked">
          {{ t('admin.people.revoked') }}
        </span>
        <p v-if="person.hasDevice" class="revoke-confirm">
          {{ t('admin.people.revokeConfirm', { name: person.name }) }}
        </p>
        <button
          type="button"
          class="toggle-active"
          @click="people.setActive(person.serverPersonId, !person.isActive)"
        >
          {{ person.isActive ? t('admin.people.deactivate') : t('admin.people.activate') }}
        </button>
        <p class="deactivate-help">{{ t('admin.people.deactivateHelp') }}</p>
      </li>
    </ul>
    <button type="button" class="new-person" @click="people.createInvitation()">
      {{ t('admin.people.new') }}
    </button>
    <InvitationPanel
      v-if="people.invitation !== null"
      :invitation="people.invitation"
      @close="people.closeInvitation"
    />
  </section>
</template>
