import React, {PropsWithChildren} from 'react';

export function Card({children}: PropsWithChildren<{}>) {
  return <>
    <div>{children}</div>
    <style jsx>
      {`
        div {
          background-color: var(--secondary-light-color);
        }
      `}
    </style>
  </>;
}