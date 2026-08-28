<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminStaffStore } from '../../../stores/admin/staff'
import ConfirmDialog from '../ConfirmDialog.vue'
import InvitationPanel from './InvitationPanel.vue'

const { t } = useI18n()
const staff = useAdminStaffStore()
const renamingId = ref<string | null>(null)
const newName = ref('')
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)

const shown = computed(() =>
  staff.staffMembers.filter((staffMember) => showsDeactivated.value || staffMember.isActive),
)

async function deactivate(): Promise<void> {
  const staffMemberId = askingAboutId.value
  askingAboutId.value = null
  if (staffMemberId !== null) {
    await staff.setActive(staffMemberId, false)
  }
}

async function rename(id: string): Promise<void> {
  const renamed = await staff.rename(id, newName.value.trim())
  if (!renamed) {
    return
  }
  renamingId.value = null
  newName.value = ''
}

onMounted(async () => {
  staff.listen()
  await staff.load()
})
</script>

<template>
  <v-container class="admin-staff">
    <h1 class="text-h5 mb-2">{{ t('admin.staff.title') }}</h1>
    <p class="help text-medium-emphasis mb-4">{{ t('admin.staff.help') }}</p>

    <v-alert v-if="staff.enrolledName !== null" class="enrolled mb-4" type="success" variant="tonal">
      {{ t('admin.enrol.done', { name: staff.enrolledName }) }}
    </v-alert>
    <v-alert v-if="staff.errorMessage !== null" class="refusal mb-4" type="warning" variant="tonal">
      {{
        staff.errorMessage.count === null
          ? t(staff.errorMessage.key, staff.errorMessage.parameters)
          : t(staff.errorMessage.key, staff.errorMessage.parameters, staff.errorMessage.count)
      }}
    </v-alert>
    <v-alert v-if="staff.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>

    <v-checkbox
      v-model="showsDeactivated"
      class="show-deactivated"
      :label="t('admin.showDeactivated')"
    />

    <v-card v-for="staffMember in shown" :key="staffMember.staffMemberId" class="staff-row mb-3">
      <v-card-text v-if="renamingId === staffMember.staffMemberId">
        <v-text-field v-model="newName" class="rename-field" :label="t('admin.staff.rename')" />
        <p class="help text-medium-emphasis">{{ t('admin.staff.renameHelp') }}</p>
        <v-btn class="save-name" color="primary" @click="rename(staffMember.staffMemberId)">
          {{ t('admin.save') }}
        </v-btn>
      </v-card-text>
      <v-card-actions class="staff-row-line">
        <span class="name text-h6 ms-2 me-2">{{ staffMember.name }}</span>
        <v-chip v-if="!staffMember.isActive" class="deactivated me-2" size="small" color="grey">
          {{ t('admin.deactivated') }}
        </v-chip>
        <v-chip v-if="!staffMember.hasDevice" class="no-phone me-2" size="small" color="warning">
          {{ t('admin.staff.noPhone') }}
        </v-chip>
        <v-btn
          class="new-code"
          variant="text"
          @click="staff.createInvitation(staffMember.staffMemberId)"
        >
          {{ t('admin.staff.newCode') }}
        </v-btn>
        <v-btn
          class="rename"
          variant="text"
          @click="
            () => {
              renamingId = staffMember.staffMemberId
              newName = staffMember.name
            }
          "
        >
          {{ t('admin.staff.rename') }}
        </v-btn>
        <v-spacer />
        <v-btn
          v-if="staffMember.isActive"
          class="deactivate"
          icon="mdi-delete"
          variant="text"
          color="error"
          :aria-label="t('admin.deactivate')"
          @click="askingAboutId = staffMember.staffMemberId"
        />
        <v-btn
          v-else
          class="reactivate"
          variant="text"
          @click="staff.setActive(staffMember.staffMemberId, true)"
        >
          {{ t('admin.staff.activate') }}
        </v-btn>
      </v-card-actions>
      <v-expand-transition>
        <InvitationPanel
          v-if="staff.invitation?.staffMember?.id === staffMember.staffMemberId"
          :invitation="staff.invitation"
          @close="staff.closeInvitation"
        />
      </v-expand-transition>
    </v-card>

    <v-btn class="new-staff-member" color="primary" @click="staff.createInvitation()">
      {{ t('admin.staff.new') }}
    </v-btn>

    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.staff.deactivateTitle')"
      :body="t('admin.staff.deactivateBody')"
      :confirm-label="t('admin.staff.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
    <InvitationPanel
      v-if="staff.invitation !== null && staff.invitation.staffMember === null"
      :invitation="staff.invitation"
      @close="staff.closeInvitation"
    />
  </v-container>
</template>
