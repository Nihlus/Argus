import React from 'react';
import {ChildrenProps} from "../utils";

export function InteractionViewLayout({children}: ChildrenProps) {
  return <>
    <section>
      {children}
    </section>
    <style jsx>
      {`
        section {
          display: grid;
          grid-template-columns: 1fr 3fr;
          gap: 8px;
          height: 75vh;
        }
      `}
    </style>
  </>;
}