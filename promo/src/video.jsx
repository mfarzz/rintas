import React from 'react';
import {AbsoluteFill, Audio, Img, interpolate, staticFile, useCurrentFrame} from 'remotion';

const NAVY = '#14141E';
const TEAL = '#4FD1C5';
const PAPER = '#F4F4F6';
const WHITE = '#FFFFFF';
const clamp = {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'};

const Box = ({x, y, w, h, color = NAVY, border}) => (
  <rect x={x} y={y} width={w} height={h} fill={color} stroke={border} strokeWidth={border ? 5 : 0} />
);

const Courier = ({frame, mood = 'happy', size = 350}) => {
  const bounce = Math.sin(frame * 0.22) * 8;
  const step = Math.sin(frame * 0.33) * 10;
  return (
    <svg width={size} height={size} viewBox="0 0 360 360" style={{overflow: 'visible', transform: `translateY(${bounce}px)`}}>
      <Box x={73} y={46} w={214} h={205} color={NAVY} />
      <Box x={100} y={70} w={160} h={152} color={PAPER} />
      <Box x={112} y={92} w={18} h={18} color={NAVY} />
      <Box x={225} y={92} w={18} h={18} color={NAVY} />
      {mood === 'bored' ? <Box x={151} y={164} w={58} h={8} color={NAVY} /> : <>
        <Box x={150} y={162} w={14} h={10} color={NAVY} />
        <Box x={164} y={172} w={31} h={10} color={NAVY} />
        <Box x={195} y={162} w={14} h={10} color={NAVY} />
      </>}
      <Box x={73} y={234} w={214} h={18} color={TEAL} />
      <Box x={33} y={138 + step / 2} w={40} h={26} color={NAVY} />
      <Box x={287} y={138 - step / 2} w={40} h={26} color={NAVY} />
      <Box x={113} y={252} w={37} h={44 + step} color={NAVY} />
      <Box x={210} y={252} w={37} h={44 - step} color={NAVY} />
      <Box x={102} y={288 + step} w={61} h={17} color={NAVY} />
      <Box x={198} y={288 - step} w={61} h={17} color={NAVY} />
      <Box x={73} y={46} w={43} h={43} color={TEAL} />
    </svg>
  );
};

const Text = ({children, size = 82, color = NAVY, top = 290, weight = 800, align = 'center'}) => (
  <div style={{position: 'absolute', top, left: 100, width: 850, color, fontFamily: 'Arial, sans-serif', fontSize: size,
    fontWeight: weight, letterSpacing: -3, lineHeight: 1.08, textAlign: align}}>{children}</div>
);

const Tag = ({children, dark = false}) => (
  <div style={{position: 'absolute', top: 145, left: 100, border: `4px solid ${dark ? PAPER : NAVY}`,
    padding: '13px 23px', fontFamily: 'Arial, sans-serif', color: dark ? PAPER : NAVY,
    fontSize: 31, fontWeight: 800, letterSpacing: 2}}>{children}</div>
);

const Card = ({children, x = 150, y = 800, w = 780, h = 480, dark = false}) => (
  <div style={{position: 'absolute', left: x, top: y, width: w, height: h, background: dark ? NAVY : WHITE,
    border: `6px solid ${dark ? TEAL : NAVY}`, display: 'flex', alignItems: 'center', justifyContent: 'center'}}>{children}</div>
);

const Scene = ({frame, start, end, dark = false, children}) => {
  if (frame < start || frame >= end) return null;
  return <AbsoluteFill style={{background: dark ? NAVY : PAPER, overflow: 'hidden'}}>{children(frame - start)}</AbsoluteFill>;
};

export const RintasPromo = ({withVoiceover = false}) => {
  const frame = useCurrentFrame();
  return <AbsoluteFill style={{background: PAPER}}>
    <Scene frame={frame} start={0} end={75}>
      {(t) => <>
        <Tag>RINTAS / 01</Tag>
        <Text top={340}>KIRIM FILE<br/>MASIH RIBET?</Text>
        <div style={{position: 'absolute', top: 790, left: 366}}><Courier frame={t} mood="bored" /></div>
        <div style={{position: 'absolute', top: 1240, left: 463, fontSize: 110, fontWeight: 900, color: TEAL,
          transform: `rotate(${t * 8}deg)`, fontFamily: 'Arial'}}>↻</div>
        <Text top={1480} size={49}>Tunggu upload. Cari link. Kirim.</Text>
      </>}
    </Scene>
    <Scene frame={frame} start={75} end={160} dark>
      {(t) => <>
        <Tag dark>RINTAS / 02</Tag>
        <Text top={355} color={PAPER}>KENALAN<br/>DENGAN RINTAS.</Text>
        <div style={{position: 'absolute', top: 800, left: 365,
          transform: `scale(${interpolate(t, [0, 15], [0.5, 1], clamp)})`}}>
          <Courier frame={t} />
        </div>
        <div style={{position: 'absolute', top: 1300, left: 130, width: 820, height: 10, background: TEAL,
          transform: `scaleX(${interpolate(t, [10, 65], [0, 1], clamp)})`}} />
        <Text top={1410} color={PAPER} size={52}>Berbagi langsung dari laptop.</Text>
      </>}
    </Scene>
    <Scene frame={frame} start={160} end={285}>
      {(t) => <>
        <Tag>RINTAS / 03</Tag>
        <Text top={340}>PILIH FILE.<br/>KLIK BAGIKAN.</Text>
        <Card y={810}>
          <div style={{width: 520, height: 270, border: `6px solid ${NAVY}`, position: 'relative', background: PAPER}}>
            <div style={{height: 48, background: NAVY}} />
            <div style={{position: 'absolute', left: 44, top: 91, width: 70, height: 86, border: `5px solid ${NAVY}`}} />
            <div style={{position: 'absolute', left: 142, top: 105, width: 294, height: 19, background: NAVY}} />
            <div style={{position: 'absolute', left: 142, top: 143, width: 224, height: 14, background: '#A1A3AD'}} />
            <div style={{position: 'absolute', left: 142, top: 190, width: 260, height: 43, background: TEAL,
              border: `4px solid ${NAVY}`, transform: `scale(${interpolate(t, [35, 45, 53], [1, 0.9, 1], clamp)})`}} />
          </div>
        </Card>
        <Text top={1460} size={49}>Tautannya langsung tersalin.</Text>
      </>}
    </Scene>
    <Scene frame={frame} start={285} end={420} dark>
      {(t) => <>
        <Tag dark>RINTAS / 04</Tag>
        <Text top={345} color={PAPER}>KIRIM LINK.<br/>TEMAN UNDUH.</Text>
        <div style={{position: 'absolute', top: 830, left: 120, width: 250, height: 370, border: `8px solid ${PAPER}`}}>
          <div style={{margin: 40, width: 155, height: 120, background: TEAL}} />
          <div style={{marginLeft: 40, width: 155, height: 18, background: PAPER}} />
        </div>
        <div style={{position: 'absolute', top: 1000, left: 410, width: 300, height: 12, background: TEAL,
          transformOrigin: 'left', transform: `scaleX(${interpolate(t, [12, 65], [0, 1], clamp)})`}} />
        <div style={{position: 'absolute', top: 870, left: 755, width: 210, height: 350, border: `8px solid ${PAPER}`}}>
          <div style={{position: 'absolute', left: 70, top: 80, color: TEAL, fontSize: 95, fontWeight: 900}}>↓</div>
        </div>
        <Text top={1430} color={PAPER} size={48}>Lewat browser, tanpa akun penerima.</Text>
      </>}
    </Scene>
    <Scene frame={frame} start={420} end={555}>
      {(t) => <>
        <Tag>RINTAS / 05</Tag>
        <Text top={340}>FILE TETAP<br/>DI LAPTOPMU.</Text>
        <div style={{position: 'absolute', top: 820, left: 368}}><Courier frame={t} size={350} /></div>
        <div style={{position: 'absolute', top: 1320, left: 162, width: 756, border: `6px solid ${NAVY}`,
          padding: 28, boxSizing: 'border-box', textAlign: 'center', color: NAVY, fontSize: 43, fontWeight: 800,
          fontFamily: 'Arial'}}>AKSES BISA DIATUR</div>
      </>}
    </Scene>
    <Scene frame={frame} start={555} end={645} dark>
      {() => <>
        <div style={{position: 'absolute', top: 440, left: 325, width: 430, height: 430}}>
          <Img src={staticFile('rintas-mark.png')} style={{width: '100%', height: '100%', objectFit: 'contain'}} />
        </div>
        <Text top={1000} color={PAPER} size={115}>RINTAS</Text>
        <Text top={1190} color={TEAL} size={56}>Berbagi file tanpa ribet.</Text>
        <div style={{position: 'absolute', top: 1460, left: 210, width: 660, padding: '22px 0',
          border: `5px solid ${TEAL}`, color: PAPER, fontFamily: 'Arial', fontSize: 43,
          fontWeight: 800, textAlign: 'center'}}>COBA RINTAS</div>
      </>}
    </Scene>
    {withVoiceover && <Audio src={staticFile('voiceover.mp3')} />}
  </AbsoluteFill>;
};
