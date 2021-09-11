import React, { useMemo } from 'react'
import { Blob } from 'buffer'
import { useEnrichedFileInfo } from '@/components/fileUpload/useEnrichedFileInfo'

export interface DroppedFileProps {
  file: File
}

export function DroppedFile({ file }: DroppedFileProps) {
  const {
    objectUrl,
    lastModified,
    imageType,
    original: { name },
  } = useEnrichedFileInfo(file)
  return (
    <>
      <div>
        <img src={objectUrl} alt={file.name} />
        <div className="file">
          {name}
          {imageType}
          {lastModified.toLocaleString()}
        </div>
      </div>
      <style jsx>{`
        img {
          width: 200px;
        }
      `}</style>
    </>
  )
}
