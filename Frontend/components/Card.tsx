import React, {PropsWithChildren} from 'react';

export function Card({children}: PropsWithChildren<{}>) {
  return <>
    <div>{children}</div>
    <style jsx>
      {`
        div {
          background-color: var(--secondary-light-color);
          padding: 8px;
          border-radius: 4px;
          overflow-y: auto;
        }
      `}
    </style>
  </>;
}