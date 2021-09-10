import React, { PropsWithChildren, ReactNode } from 'react'

export interface HintProps {
  adornment: ReactNode
}

export function Hint({ children, adornment }: PropsWithChildren<HintProps>) {
  return (
    <div>
      {adornment}
      {children}
    </div>
  )
}
