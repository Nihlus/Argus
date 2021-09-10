import React from 'react';
import {ChildrenProps} from "../utils";

export function Card({children}: ChildrenProps) {
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