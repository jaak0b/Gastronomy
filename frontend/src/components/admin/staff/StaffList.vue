<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminStaffStore } from '../../../stores/admin/staff'
import { useAdminEnrolmentStore } from '../../../stores/admin/enrolment'
import ConfirmDialog from '../ConfirmDialog.vue'
import InvitationPanel from '../enrolment/InvitationPanel.vue'
import { useRefusalText } from '../refusalText'

const { t } = useI18n()
const staff = useAdminStaffStore()
const enrolment = useAdminEnrolmentStore()
const renamingId = ref<string | null>(null)
const newName = ref('')
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const isAddingPerson = ref(false)
const isSavingTheNewPerson = ref(false)
const newPersonName = ref('')
let stopListening: (() => void) | null = null

const shown = computed(() =>
  staff.staffMembers.filter((staffMember) => showsDeactivated.value || staffMember.isActive),
)

const refusalText = useRefusalText([() => enrolment.errorMessage, () => staff.errorMessage])

function inviteStaffMember(staffMemberId: string): void {
  void enrolment.createInvitation({ kind: 'staffMember', staffMemberId })
}

function startAddingPerson(): void {
  isAddingPerson.value = true
  newPersonName.value = ''
}

function stopAddingPerson(): void {
  isAddingPerson.value = false
  newPersonName.value = ''
}

async function addPerson(): Promise<void> {
  if (isSavingTheNewPerson.value) {
    return
  }
  isSavingTheNewPerson.value = true
  const staffMemberId = await staff.create(newPersonName.value.trim())
  isSavingTheNewPerson.value = false
  if (staffMemberId === null) {
    return
  }
  stopAddingPerson()
  inviteStaffMember(staffMemberId)
}

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
    <h1 class="text-h5 mb-2">{{ t('admin.staff.title') }}</h1>
    <p class="help text-medium-emphasis mb-4">{{ t('admin.staff.help') }}</p>

    <v-alert
      v-if="enrolment.enrolledStaffMemberName !== null"
      class="enrolled mb-4"
      type="success"
      variant="tonal"
    >
      {{ t('admin.enrol.done', { name: enrolment.enrolledStaffMemberName }) }}
    </v-alert>
    <v-alert v-if="refusalText !== null" class="refusal mb-4" type="warning" variant="tonal">
      {{ refusalText }}
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
        <v-btn class="new-code" variant="text" @click="inviteStaffMember(staffMember.staffMemberId)">
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
          v-if="enrolment.invitation?.staffMember?.id === staffMember.staffMemberId"
          :invitation="enrolment.invitation"
          :qr="enrolment.invitationQr"
          @close="enrolment.closeInvitation"
          @renew="inviteStaffMember(staffMember.staffMemberId)"
        />
      </v-expand-transition>
    </v-card>

    <v-card v-if="isAddingPerson" class="new-person mb-3">
      <v-card-text>
        <v-text-field
          v-model="newPersonName"
          class="new-person-name"
          :label="t('admin.staff.newName')"
        />
        <p class="help text-medium-emphasis mb-4">{{ t('admin.staff.newHelp') }}</p>
        <v-btn
          class="save-new-person me-2"
          color="primary"
          :disabled="newPersonName.trim().length === 0 || isSavingTheNewPerson"
          @click="addPerson"
        >
          {{ t('admin.save') }}
        </v-btn>
        <v-btn class="cancel-new-person" variant="text" @click="stopAddingPerson">
          {{ t('admin.cancel') }}
        </v-btn>
      </v-card-text>
    </v-card>

    <v-btn v-else class="new-staff-member" color="primary" @click="startAddingPerson">
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
  </v-container>
</template>
