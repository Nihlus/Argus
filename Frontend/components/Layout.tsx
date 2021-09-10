import React, {PropsWithChildren} from 'react';


export function Layout({children}: PropsWithChildren<{}>) {
  return <>
    <div>{children}</div>
    <style jsx>{`
      
 
    `}</style>
  </>;
}
