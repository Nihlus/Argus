import React, { useMemo } from 'react'
import { Blob } from 'buffer'

export interface DroppedFileProps {
  file: File
}

export function DroppedFile({ file }: DroppedFileProps) {
  const url = useMemo(() => URL.createObjectURL(file), [file])

  return (
    <>
      <div>
        <img src={url} alt={file.name} />
        <div className="file">{file.name}</div>
      </div>
      <style jsx>{`
        img {
          width: 200px;
        }
      `}</style>
    </>
  )
}
