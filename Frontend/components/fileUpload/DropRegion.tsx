import React from 'react'
import { Hint } from '@/components/common/Hint'
import { BsFillImageFill } from 'react-icons/bs'

export function DropRegion() {
  return (
    <>
      <div className="container">
        <Hint adornment={<BsFillImageFill />}>
          Drop your files here or click to choose!
        </Hint>
      </div>
      <style jsx>{`
        .container {
          height: 100%;
          display: flex;
          flex-flow: column;
          place-items: center;
          place-content: center;
        }
      `}</style>
    </>
  )
}
