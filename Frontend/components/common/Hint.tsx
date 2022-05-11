import React, { PropsWithChildren, ReactNode } from 'react'

export interface HintProps {
  adornment: ReactNode
}

export function Hint({ children, adornment }: PropsWithChildren<HintProps>) {
  return (
    <>
      <div className="hint">
        <div>{adornment}</div>
        <div>{children}</div>
      </div>
      <style jsx>
        {`
          .hint {
            display: flex;
            flex-flow: column;
            place-items: center;
            gap: 16px;
            color: (--text-color-hint);
          }
        `}
      </style>
    </>
  )
}
