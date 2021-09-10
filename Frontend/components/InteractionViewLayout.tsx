import React, {PropsWithChildren} from 'react';

export function InteractionViewLayout({children}: PropsWithChildren<{}>) {
  return <>
    <section>
      {children}
    </section>
    <style jsx>
      {`
        section {
          display: grid;
          grid-template-columns: 1fr 3fr;
          width:100%;
        }
      `}
    </style>
  </>;
}