import type { PlanSize } from './tablePlanDrawing'

export const LARGEST_NAME_FONT_SIZE = 14

const FONT_SIZE_STEP = 0.1

export function nameFontSizeFittingEveryWordAcross(
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

export function nameFontSizeFittingEveryLineInside(
  name: string,
  room: PlanSize,
  widthAtTheLargestSize: (text: string) => number,
): number {
  const words = name.split(/\s+/).filter((word) => word !== '')
  let fontSize = nameFontSizeFittingEveryWordAcross(name, room.width, widthAtTheLargestSize)
  while (
    fontSize > FONT_SIZE_STEP
    && !everyLineIsDrawn(lineCountOf(words, fontSize, room.width, widthAtTheLargestSize), fontSize, room.height)
  ) {
    fontSize = Math.round((fontSize - FONT_SIZE_STEP) * 10) / 10
  }
  return fontSize
}

function lineCountOf(
  words: readonly string[],
  fontSize: number,
  roomAcross: number,
  widthAtTheLargestSize: (text: string) => number,
): number {
  const scale = fontSize / LARGEST_NAME_FONT_SIZE
  let finishedLines = 0
  let line = ''
  for (const word of words) {
    const longerLine = line === '' ? word : `${line} ${word}`
    if (line === '' || widthAtTheLargestSize(longerLine) * scale <= roomAcross) {
      line = longerLine
    } else {
      finishedLines += 1
      line = word
    }
  }
  return line === '' ? finishedLines : finishedLines + 1
}

function everyLineIsDrawn(lineCount: number, fontSize: number, roomDown: number): boolean {
  const aSingleLineIsAlwaysDrawn = lineCount <= 1
  return aSingleLineIsAlwaysDrawn || lineCount * fontSize <= roomDown
}
