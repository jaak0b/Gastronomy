import { useI18n } from 'vue-i18n'
import { appLanguageOf } from '../../shared/core/deviceLanguage'
import { formatNumber } from '../../shared/core/numberText'

const SHOWN_FRACTION_DIGITS = 3

export function useIngredientAmountText(): (amount: number, unitName: string) => string {
  const { locale } = useI18n()
  return (amount, unitName) =>
    `${formatNumber(amount, appLanguageOf(locale.value), SHOWN_FRACTION_DIGITS)} ${unitName}`
}
