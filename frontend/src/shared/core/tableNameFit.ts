export const LARGEST_NAME_FONT_SIZE = 14

export function nameFontSizeKeepingWordsWhole(
  name: string,
  roomAcross: number,
  widthAtTheLargestSize: (word: string) => number,
): number {
  const widestWord = Math.max(0, ...name.split(/\s+/).map((word) => widthAtTheLargestSize(word)))
  if (widestWord <= roomAcross) {
    return LARGEST_NAME_FONT_SIZE
  }
  return Math.floor((LARGEST_NAME_FONT_SIZE * roomAcross * 10) / widestWord) / 10
}
