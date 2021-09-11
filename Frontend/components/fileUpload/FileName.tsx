import React from 'react'
import { ChildrenProps } from '../../utils'

export function FileName({ children }: ChildrenProps) {
  return (
    <>
      <h4>{children}</h4>
      <style jsx>{`
        h4 {
          margin: 0;
          color: var(--primary-main-color);
        }
      `}</style>
    </>
  )
}
