import { useMemo } from 'react'

export interface EnrichedFile {
  original: File
  lastModified: Date
  imageType: string
  objectUrl: string
}

export function useEnrichedFileInfo(original: File): EnrichedFile {
  return useMemo(
    () => ({
      original,
      imageType: original.type.split('/')[1],
      objectUrl: URL.createObjectURL(original),
      lastModified: new Date(original.lastModified),
    }),
    [original]
  )
}
