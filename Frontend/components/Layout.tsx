import React from 'react';
import {ChildrenProps} from "../utils";


export function Layout({children}: ChildrenProps) {
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
