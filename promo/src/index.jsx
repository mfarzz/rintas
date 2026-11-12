import React from 'react';
import {Composition, registerRoot} from 'remotion';
import {RintasPromo} from './video';

const Root = () => (
  <Composition
    id="RintasPromo"
    component={RintasPromo}
    durationInFrames={645}
    fps={30}
    width={1080}
    height={1920}
    defaultProps={{withVoiceover: false}}
  />
);

registerRoot(Root);
