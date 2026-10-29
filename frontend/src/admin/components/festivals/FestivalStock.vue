<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AdminFestivalIngredientView,
  AdminFestivalView,
} from '../../../shared/api/generatedSchemas'
import { formatTimeOfDay, spansMoreThanOneDay } from '../../../shared/core/moments'
import type { ParsedAmountInput } from '../../core/ingredientAmounts'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminFestivalStockStore } from '../../stores/festivalStock'
import { useIngredientAmountText } from '../../composables/useIngredientAmountText'
import { useRowRefusals } from '../../composables/useRowRefusals'
import BaseListRow from '../BaseListRow.vue'
import BaseWarningText from '../BaseWarningText.vue'
import IngredientAmountField from '../ingredients/IngredientAmountField.vue'
import FestivalStockChart from './FestivalStockChart.vue'

const STOCK_INVALID_KEY = 'errors.admin.ingredients.stockInvalid'

const props = defineProps<{ festival: AdminFestivalView }>()

const { t, locale } = useI18n()
const stock = useAdminFestivalStockStore()
const amountText = useIngredientAmountText()
const ingredientRefusals = useRowRefusals()
const ingredientIdShowingHistory = ref<string | null>(null)

const rows = computed(() =>
  stock.ingredients.map((ingredient) => ({ ingredient, warningText: stockWarningTextOf(ingredient) })),
)

async function saveAvailableAmount(
  ingredientId: string,
  availableAmount: number | null,
): Promise<void> {
  ingredientRefusals.showRefusalOf(
    ingredientId,
    await stock.setAvailableAmount(props.festival.festivalId, ingredientId, availableAmount),
  )
}

async function changeAvailableAmount(
  ingredientId: string,
  parsed: ParsedAmountInput,
): Promise<void> {
  ingredientRefusals.forgetRefusalOf(ingredientId)
  switch (parsed.kind) {
    case 'amount':
      await saveAvailableAmount(ingredientId, parsed.amount)
      return
    case 'empty':
      await saveAvailableAmount(ingredientId, null)
      return
    case 'unreadable':
      ingredientRefusals.refuse(ingredientId, STOCK_INVALID_KEY)
      return
    default:
      assertNever(parsed)
  }
}

function stockWarningTextOf(ingredient: AdminFestivalIngredientView): string | null {
  switch (ingredient.stockWarning) {
    case 'none':
      return null
    case 'runsOutSoon':
      if (ingredient.runsOutSoonAtUtc === null) {
        throw new Error(`The laptop warned that ${ingredient.name} runs out soon without naming when.`)
      }
      return t('admin.ingredients.labels.runsOutAt', {
        moment: formatTimeOfDay(
          new Date(ingredient.runsOutSoonAtUtc),
          locale.value,
          spansMoreThanOneDay(props.festival.startsAtUtc, props.festival.endsAtUtc),
        ),
      })
    case 'usedUp':
      return t('admin.ingredients.labels.usedUp')
    default:
      return assertNever(ingredient.stockWarning)
  }
}

function historyToggleIconOf(ingredientId: string): string {
  return ingredientIdShowingHistory.value === ingredientId ? 'mdi-chevron-up' : 'mdi-chevron-down'
}

function toggleHistory(ingredientId: string): void {
  ingredientIdShowingHistory.value = ingredientIdShowingHistory.value === ingredientId ? null : ingredientId
}
</script>

<template>
  <section v-if="stock.ingredients.length > 0" class="festival-stock admin-section-gap" data-test="festival-stock">
    <v-card variant="outlined">
      <div class="admin-card-body">
        <h2 class="section-heading app-section-title mb-3" data-test="section-heading">{{ t('admin.ingredients.title') }}</h2>
        <BaseListRow
          v-for="({ ingredient, warningText }, position) in rows"
          :key="ingredient.ingredientId"
          class="festival-ingredient-row"
          data-test="festival-ingredient-row"
          :data-test-id="ingredient.ingredientId"
          :position="position"
          :name="ingredient.name"
          name-test-name="ingredient-name"
          :refusal-text="ingredientRefusals.refusalTextOf(ingredient.ingredientId)"
        >
          <template #facts>
            <IngredientAmountField
              :unit-name="ingredient.unitName"
              :saved-amount="ingredient.availableAmount"
              :label="t('admin.ingredients.labels.available')"
              @amount-typed="ingredientRefusals.forgetRefusalOf(ingredient.ingredientId)"
              @amount-changed="changeAvailableAmount(ingredient.ingredientId, $event)"
            />
            <span
              class="used-amount app-small-text"
              data-test="used-amount"
            >
              {{
                t('admin.ingredients.labels.used', {
                  amount: amountText(ingredient.usedAmount, ingredient.unitName),
                })
              }}
            </span>
            <span
              class="stock-warning"
              data-test="stock-warning"
            >
              <BaseWarningText
                v-if="warningText !== null"
                data-test="stock-warning-text"
                :text="warningText"
              />
            </span>
            <v-btn
              class="history-toggle"
              data-test="history-toggle"
              variant="text"
              :append-icon="historyToggleIconOf(ingredient.ingredientId)"
              @click="toggleHistory(ingredient.ingredientId)"
            >
              {{ t('admin.ingredients.actions.history') }}
            </v-btn>
          </template>
          <template v-if="ingredientIdShowingHistory === ingredient.ingredientId">
            <p
              v-if="ingredient.usedAmount === 0"
              class="nothing-used-yet app-small-text text-medium-emphasis admin-row-details mb-0"
              data-test="nothing-used-yet"
            >
              {{ t('admin.ingredients.history.nothingUsedYet') }}
            </p>
            <FestivalStockChart
              v-else
              class="admin-row-details"
              :ingredient="ingredient"
              :festival="festival"
            />
          </template>
        </BaseListRow>
      </div>
    </v-card>
  </section>
</template>

<style scoped>
.festival-ingredient-row :deep(.row-line) {
  display: grid;
  grid-template-columns: minmax(8rem, 1fr) 16rem 10rem 17rem 7rem;
  align-items: center;
}
</style>
