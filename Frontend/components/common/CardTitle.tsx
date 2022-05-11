import React, { PropsWithChildren } from 'react'
import { ChildrenProps } from '../../utils'

export function CardTitle({ children }: ChildrenProps) {
  return (
    <>
      <h2>{children}</h2>
      <style jsx>{`
        h2 {
          margin: 0;
          color: var(--primary-main-color);
          padding: 0 4px;
        }
      `}</style>
    </>
  )
}