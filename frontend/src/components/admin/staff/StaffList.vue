<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { refusalFrom, type AdminActionResult } from '../../../core/adminActionResult'
import type { AdminErrorMessage } from '../../../core/adminErrorMessage'
import type { AdminStaffMember } from '../../../shared/api/apiTypes'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminStaffStore } from '../../../stores/admin/staff'
import { useAdminEnrolmentStore } from '../../../stores/admin/enrolment'
import ConfirmDialog from '../ConfirmDialog.vue'
import InvitationPanel from '../enrolment/InvitationPanel.vue'
import { useRefusalText } from '../refusalText'
import StaffRenameDialog from './StaffRenameDialog.vue'

const { t } = useI18n()
const staff = useAdminStaffStore()
const enrolment = useAdminEnrolmentStore()
const renamingStaffMember = ref<AdminStaffMember | null>(null)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const refusal = ref<AdminErrorMessage | null>(null)
let stopListening: (() => void) | null = null

const shown = computed(() =>
  staff.staffMembers.filter((staffMember) => showsDeactivated.value || staffMember.isActive),
)

const refusalText = useRefusalText(refusal)

function note(result: AdminActionResult<unknown>): void {
  const message = refusalFrom(result)
  if (message !== null) {
    refusal.value = message
  }
}

async function inviteStaffMember(staffMemberId: string): Promise<void> {
  refusal.value = null
  note(await enrolment.createInvitation({ kind: 'staffMember', staffMemberId }))
}

async function inviteSomebodyNew(): Promise<void> {
  refusal.value = null
  note(await enrolment.createInvitation({ kind: 'somebodyNew' }))
}

async function deactivate(): Promise<void> {
  const staffMemberId = askingAboutId.value
  askingAboutId.value = null
  if (staffMemberId !== null) {
    refusal.value = null
    note(await staff.setActive(staffMemberId, false))
  }
}

async function reactivate(staffMemberId: string): Promise<void> {
  refusal.value = null
  note(await staff.setActive(staffMemberId, true))
}

async function rename(name: string): Promise<void> {
  const staffMember = renamingStaffMember.value
  if (staffMember === null) {
    return
  }
  refusal.value = null
  const renamed = await staff.rename(staffMember.staffMemberId, name)
  switch (renamed.kind) {
    case 'ok':
      renamingStaffMember.value = null
      return
    case 'failed':
      refusal.value = renamed.message
      return
    default:
      assertNever(renamed)
  }
}

function startRenaming(staffMember: AdminStaffMember): void {
  renamingStaffMember.value = staffMember
  refusal.value = null
}

function stopRenaming(): void {
  renamingStaffMember.value = null
  refusal.value = null
}

onMounted(async () => {
  stopListening = staff.listen()
  await staff.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-staff">
    <div class="admin-heading d-flex align-center flex-wrap justify-space-between ga-2 mb-4">
      <h1 class="text-h5">{{ t('admin.staff.title') }}</h1>
      <v-checkbox
        v-model="showsDeactivated"
        class="show-deactivated"
        density="compact"
        hide-details
        :label="t('admin.showDeactivated')"
      />
    </div>

    <v-alert
      v-if="enrolment.enrolledStaffMemberName !== null"
      class="enrolled mb-4"
      type="success"
      variant="tonal"
    >
      {{ t('admin.enrol.done', { name: enrolment.enrolledStaffMemberName }) }}
    </v-alert>
    <v-alert
      v-if="refusalText !== null && renamingStaffMember === null"
      class="refusal mb-4"
      type="warning"
      variant="tonal"
    >
      {{ refusalText }}
    </v-alert>
    <v-alert v-if="staff.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>

    <v-card v-for="staffMember in shown" :key="staffMember.staffMemberId" class="staff-row mb-3">
      <v-card-actions class="staff-row-line">
        <span class="name text-h6 ms-2 me-2">{{ staffMember.name }}</span>
        <v-chip v-if="!staffMember.isActive" class="deactivated me-2" size="small" color="grey">
          {{ t('admin.deactivated') }}
        </v-chip>
        <v-chip v-if="!staffMember.hasDevice" class="no-phone me-2" size="small" color="warning">
          {{ t('admin.staff.noPhone') }}
        </v-chip>
        <v-btn class="new-code" variant="text" @click="inviteStaffMember(staffMember.staffMemberId)">
          {{ t('admin.staff.newCode') }}
        </v-btn>
        <v-btn
          class="rename"
          variant="text"
          @click="startRenaming(staffMember)"
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
          @click="reactivate(staffMember.staffMemberId)"
        >
          {{ t('admin.staff.activate') }}
        </v-btn>
      </v-card-actions>
      <v-expand-transition>
        <InvitationPanel
          v-if="enrolment.invitation?.staffMember?.id === staffMember.staffMemberId"
          :invitation="enrolment.invitation"
          :qr="enrolment.invitationQr"
          @close="enrolment.closeInvitation"
          @renew="inviteStaffMember(staffMember.staffMemberId)"
        />
      </v-expand-transition>
    </v-card>

    <v-btn class="new-staff-member mt-6" color="primary" @click="inviteSomebodyNew">
      {{ t('admin.staff.new') }}
    </v-btn>

    <StaffRenameDialog
      v-if="renamingStaffMember !== null"
      :staff-member="renamingStaffMember"
      :error-text="refusalText"
      @save="rename"
      @cancel="stopRenaming"
    />

    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.staff.deactivateTitle')"
      :body="t('admin.staff.deactivateBody')"
      :confirm-label="t('admin.staff.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
    <InvitationPanel
      v-if="
        enrolment.invitation !== null &&
        enrolment.invitation.staffMember === null &&
        enrolment.invitation.station === null
      "
      :invitation="enrolment.invitation"
      :qr="enrolment.invitationQr"
      @close="enrolment.closeInvitation"
      @renew="inviteSomebodyNew"
    />
  </v-container>
</template>
