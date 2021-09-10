import React, {PropsWithChildren} from 'react';


export function Layout({children}: PropsWithChildren<{}>) {
  return <>
    <main>{children}</main>
    <style jsx>{`
      main {
        padding: 2rem;
        min-height:100vh;
        width: 100%;
      }
    `}</style>
  </>;
}
