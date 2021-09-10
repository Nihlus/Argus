import React from 'react'
import { ChildrenProps } from '../../utils'

export function CardContent({ children }: ChildrenProps) {
  return (
    <>
      <div>{children}</div>
      <style jsx>{`
        div {
          height: 100%;
        }
      `}</style>
    </>
  )
}
