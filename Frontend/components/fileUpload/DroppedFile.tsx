import React, { useMemo } from 'react'
import { Blob } from 'buffer'
import { useEnrichedFileInfo } from '@/components/fileUpload/useEnrichedFileInfo'
import { FileName } from '@/components/fileUpload/FileName'

export interface DroppedFileProps {
  file: File
}

export function DroppedFile({ file }: DroppedFileProps) {
  const {
    objectUrl,
    lastModified,
    original: { name },
  } = useEnrichedFileInfo(file)
  return (
    <>
      <article>
        <img src={objectUrl} alt={file.name} />
        <div className="fileInfo">
          <FileName>{name}</FileName>
          <div>Last modified: {lastModified.toLocaleString()}</div>
        </div>
      </article>
      <style jsx>{`
        .fileInfo {
          display: flex;
          flex-flow: column;
        }
        .fileName {
        }
        article {
          display: flex;
          overflow: hidden;
          gap: 8px;
          text-overflow: ellipsis;
          padding: 4px;
        }
        img {
          width: 150px;
        }
      `}</style>
    </>
  )
}
