import React, {PropsWithChildren} from 'react';

export function Headline({children}: PropsWithChildren<{}>) {
  return <>
    <h1>{children}</h1>
    <style jsx>{`
        h1 {
          text-align: center;
          color: var(--primary-main-color);
        }
    `}</style>
  </>;
}