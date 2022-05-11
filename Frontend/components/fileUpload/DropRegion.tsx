import React, {
  DragEventHandler,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react'
import { Hint } from '@/components/common/Hint'
import { BsFillImageFill } from 'react-icons/bs'
import { DroppedFile } from '@/components/fileUpload/DroppedFile'
//
// const useDropRegion = () => {
//   const dropTarget = useRef<HTMLDivElement>()
//
//   useEffect(() => {
//     const dropElement = dropTarget.current;
//     if(!dropElement) {
//       return;
//     }
//
//     dropElement
//
//     return () => {
//
//     }
//   }, [dropTarget])
//
//   return [dropTarget] as const
// }

export function DropRegion() {
  const [droppedFiles, setDroppedFiles] = useState<File[]>([])

  const handleDrop: DragEventHandler<HTMLDivElement> = useCallback((e) => {
    setDroppedFiles((prev) => [...prev, ...Array.from(e.dataTransfer.files)])
    e.preventDefault()
    e.stopPropagation()
  }, [])

  const preventContextSwitch: DragEventHandler<HTMLDivElement> = useCallback(
    (e) => e.preventDefault(),
    []
  )

  const hasFiles = droppedFiles.length > 0

  return (
    <>
      <div
        className={hasFiles ? 'fileOverview' : 'container'}
        onDrop={handleDrop}
        onDragOver={preventContextSwitch}
        draggable
      >
        {hasFiles ? (
          <div>
            {droppedFiles.map((file, key) => (
              <DroppedFile file={file} key={key} />
            ))}
          </div>
        ) : (
          <Hint adornment={<BsFillImageFill />}>
            Drop your files here or click to choose!
          </Hint>
        )}
      </div>
      <style jsx>{`
        .container {
          height: 100%;
          display: flex;
          flex-flow: column;
          place-items: center;
          place-content: center;
        }
        .fileOverview {
          display: flex;
          place-content: center;
          padding: 12px 0;
        }
      `}</style>
    </>
  )
}
