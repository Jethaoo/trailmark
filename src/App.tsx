import { useState } from "react";

type Screen = "home" | "library" | "map" | "settings" | "details";

const tracks = [
  { name: "Cedar Ridge Loop", meta: "Today · 08:42", distance: "12.4 km", elevation: "680 m", color: "lime" },
  { name: "Lakeview Traverse", meta: "18 May · 2h 16m", distance: "18.7 km", elevation: "940 m", color: "blue" },
  { name: "Pine Valley Run", meta: "12 May · 54m", distance: "8.2 km", elevation: "210 m", color: "orange" },
];

function Icon({ name, size = 22 }: { name: string; size?: number }) {
  const paths: Record<string, React.ReactNode> = {
    home: <><path d="m3 10 9-7 9 7"/><path d="M5 9v11h14V9"/><path d="M9 20v-6h6v6"/></>,
    list: <><path d="M8 6h13M8 12h13M8 18h13"/><path d="M3 6h.01M3 12h.01M3 18h.01"/></>,
    map: <><path d="m3 6 6-3 6 3 6-3v15l-6 3-6-3-6 3Z"/><path d="M9 3v15M15 6v15"/></>,
    settings: <><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06-2.83 2.83-.06-.06a1.7 1.7 0 0 0-1.88-.34 1.7 1.7 0 0 0-1.03 1.56V21h-4v-.09A1.7 1.7 0 0 0 9 19.36a1.7 1.7 0 0 0-1.88.34l-.06.06-2.83-2.83.06-.06A1.7 1.7 0 0 0 4.63 15 1.7 1.7 0 0 0 3.07 14H3v-4h.09A1.7 1.7 0 0 0 4.64 9a1.7 1.7 0 0 0-.34-1.88l-.06-.06 2.83-2.83.06.06A1.7 1.7 0 0 0 9 4.63h.02A1.7 1.7 0 0 0 10 3.09V3h4v.09A1.7 1.7 0 0 0 15 4.64a1.7 1.7 0 0 0 1.88-.34l.06-.06 2.83 2.83-.06.06A1.7 1.7 0 0 0 19.37 9v.02A1.7 1.7 0 0 0 20.91 10H21v4h-.09A1.7 1.7 0 0 0 19.4 15Z"/></>,
    plus: <path d="M12 5v14M5 12h14"/>,
    search: <><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></>,
    more: <><circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/></>,
    back: <><path d="m15 18-6-6 6-6"/></>,
    locate: <><circle cx="12" cy="12" r="3"/><circle cx="12" cy="12" r="8"/><path d="M12 2V0M12 24v-2M2 12H0M24 12h-2"/></>,
    layers: <><path d="m12 2 9 5-9 5-9-5Z"/><path d="m3 12 9 5 9-5M3 17l9 5 9-5"/></>,
    chevron: <path d="m9 18 6-6-6-6"/>,
    upload: <><path d="M12 16V3M7 8l5-5 5 5"/><path d="M4 14v6h16v-6"/></>,
    share: <><circle cx="18" cy="5" r="2"/><circle cx="6" cy="12" r="2"/><circle cx="18" cy="19" r="2"/><path d="m8 11 8-5M8 13l8 5"/></>,
  };
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>{paths[name]}</svg>;
}

function MiniMap({ color = "lime" }: { color?: string }) {
  return <div className={`mini-map ${color}`}><span className="road r1"/><span className="road r2"/><svg viewBox="0 0 120 80"><path d="M8 65C30 61 27 28 48 31s17 32 34 21 13-29 32-34"/></svg></div>;
}

function TrackRow({ track, onOpen }: { track: typeof tracks[number]; onOpen: () => void }) {
  return <button className="track-row" onClick={onOpen}>
    <MiniMap color={track.color}/>
    <span className="track-copy"><strong>{track.name}</strong><small>{track.meta}</small><span className="metrics"><b>{track.distance}</b><span>↗ {track.elevation}</span></span></span>
    <span className="more"><Icon name="more"/></span>
  </button>;
}

function Header({ title, eyebrow, back, action }: { title: string; eyebrow?: string; back?: () => void; action?: React.ReactNode }) {
  return <header className="app-bar">
    {back && <button className="icon-btn" onClick={back} aria-label="Back"><Icon name="back"/></button>}
    <span className="title-block">{eyebrow && <small>{eyebrow}</small>}<strong>{title}</strong></span>
    {action}
  </header>;
}

function Home({ go, importGpx }: { go: (s: Screen) => void; importGpx: () => void }) {
  return <main className="page">
    <Header eyebrow="GOOD MORNING" title="Your trails" action={<button className="avatar">AM</button>}/>
    <section className="hero-card">
      <MiniMap/>
      <div className="hero-overlay"><span className="label">LAST OPENED</span><strong>Cedar Ridge Loop</strong><span>12.4 km · 680 m ascent</span>
        <button className="light-button" onClick={() => go("map")}>Open map <Icon name="chevron" size={18}/></button>
      </div>
    </section>
    <section className="quick-stats">
      <div><small>THIS MONTH</small><strong>64.8 <em>km</em></strong><span>4 tracks</span></div>
      <div><small>ELEVATION</small><strong>2,840 <em>m</em></strong><span>↑ 18% vs Apr</span></div>
    </section>
    <div className="section-title"><strong>Recent tracks</strong><button onClick={() => go("library")}>See all</button></div>
    <div className="track-list">{tracks.slice(0, 2).map(t => <TrackRow key={t.name} track={t} onOpen={() => go("details")}/>)}</div>
    <button className="fab" onClick={importGpx}><Icon name="plus"/><span>Import GPX</span></button>
  </main>;
}

function Library({ go, importGpx }: { go: (s: Screen) => void; importGpx: () => void }) {
  return <main className="page">
    <Header title="Track library" action={<button className="icon-btn"><Icon name="more"/></button>}/>
    <div className="search"><Icon name="search"/><input aria-label="Search tracks" placeholder="Search tracks"/><button><Icon name="layers" size={19}/></button></div>
    <div className="chips"><button className="chip active">Recent</button><button className="chip">Longest</button><button className="chip">Highest gain</button></div>
    <div className="section-title compact"><strong>12 tracks</strong><small>Newest first</small></div>
    <div className="track-list">{tracks.map(t => <TrackRow key={t.name} track={t} onOpen={() => go("details")}/>)}</div>
    <button className="fab compact-fab" onClick={importGpx}><Icon name="plus"/></button>
  </main>;
}

function Details({ go }: { go: (s: Screen) => void }) {
  return <main className="page details">
    <Header title="Track details" back={() => go("library")} action={<button className="icon-btn"><Icon name="more"/></button>}/>
    <MiniMap/>
    <div className="detail-heading"><span className="route-mark"/><div><small>HIKING · TODAY</small><strong>Cedar Ridge Loop</strong><span>Boulder County, Colorado</span></div></div>
    <button className="primary wide" onClick={() => go("map")}><Icon name="map"/> View on map</button>
    <section className="stat-grid">
      <div><small>DISTANCE</small><strong>12.4 <em>km</em></strong></div><div><small>DURATION</small><strong>3:24 <em>h</em></strong></div>
      <div><small>GAIN / LOSS</small><strong>680 / 675 <em>m</em></strong></div><div><small>AVG. SPEED</small><strong>3.6 <em>km/h</em></strong></div>
    </section>
    <section className="chart-card"><div className="section-title"><strong>Elevation</strong><span>1,942 m high</span></div>
      <svg viewBox="0 0 340 110" preserveAspectRatio="none"><defs><linearGradient id="fill" x1="0" y1="0" x2="0" y2="1"><stop stopColor="#b5db68" stopOpacity=".45"/><stop offset="1" stopColor="#b5db68" stopOpacity=".04"/></linearGradient></defs><path className="area" d="M0 92C22 80 32 87 47 68s30-12 43-27 25 2 39-3 21-28 41-18 31 56 49 45 29-3 42-23 20-4 32 2 30-14 47-7 20 27 40 17V110H0Z"/><path className="line" d="M0 92C22 80 32 87 47 68s30-12 43-27 25 2 39-3 21-28 41-18 31 56 49 45 29-3 42-23 20-4 32 2 30-14 47-7 20 27 40 17"/></svg>
      <div className="chart-labels"><span>1,262 m low</span><span>12.4 km</span></div>
    </section>
    <div className="actions"><button><Icon name="share"/> Share GPX</button><button className="danger">Delete</button></div>
  </main>;
}

function MapScreen({ go }: { go: (s: Screen) => void }) {
  const [expanded, setExpanded] = useState(false);
  return <main className="map-screen">
    <div className="map-canvas">
      <span className="contour c1"/><span className="contour c2"/><span className="contour c3"/><span className="water"/>
      <span className="map-label l1">Cedar Ridge</span><span className="map-label l2">PINE VALLEY</span><span className="map-label l3">Lookout Peak</span>
      <svg className="route" viewBox="0 0 390 700" preserveAspectRatio="none"><path className="route-shadow" d="M84 540C99 449 60 412 110 347s121-23 139-93 14-98 71-125"/><path d="M84 540C99 449 60 412 110 347s121-23 139-93 14-98 71-125"/></svg>
      <span className="pin start"/><span className="pin end"/>
    </div>
    <div className="map-header"><button className="map-button" onClick={() => go("details")}><Icon name="back"/></button><div><small>CEDAR RIDGE LOOP</small><strong>12.4 km</strong></div><button className="map-button"><Icon name="more"/></button></div>
    <div className="map-tools"><button className="map-button"><Icon name="layers"/></button><button className="map-button emphasized"><Icon name="locate"/></button></div>
    <section className={`bottom-sheet ${expanded ? "expanded" : ""}`}>
      <button className="sheet-handle" onClick={() => setExpanded(!expanded)} aria-label="Expand statistics"><span/></button>
      <div className="sheet-title"><div><small>ON TRACK</small><strong>Cedar Ridge Loop</strong></div><span className="status-dot">GPS</span></div>
      <div className="map-stats"><div><small>DISTANCE</small><strong>12.4 <em>km</em></strong></div><div><small>ASCENT</small><strong>680 <em>m</em></strong></div><div><small>DURATION</small><strong>3:24</strong></div></div>
      {expanded && <div className="expanded-content"><div><span>Highest point</span><strong>1,942 m</strong></div><div><span>Waypoints</span><strong>4</strong></div></div>}
    </section>
  </main>;
}

function Settings() {
  const [theme, setTheme] = useState("System");
  return <main className="page"><Header title="Settings"/>
    <section className="settings-group"><small>MAP</small><div className="setting-row"><span><strong>Map style</strong><small>MapTiler Outdoor</small></span><span className="preview-swatch"/><Icon name="chevron" size={18}/></div></section>
    <section className="settings-group"><small>UNITS</small><div className="setting-row"><span><strong>Distance</strong><small>Kilometers</small></span><div className="seg"><button className="active">km</button><button>mi</button></div></div><div className="setting-row"><span><strong>Elevation</strong><small>Meters</small></span><div className="seg"><button className="active">m</button><button>ft</button></div></div></section>
    <section className="settings-group"><small>APPEARANCE</small><div className="setting-row vertical"><strong>Theme</strong><div className="seg full">{["System","Light","Dark"].map(x => <button key={x} className={theme === x ? "active" : ""} onClick={() => setTheme(x)}>{x}</button>)}</div></div></section>
    <section className="settings-group"><small>STORAGE</small><div className="setting-row"><span><strong>Local GPX files</strong><small>12 tracks · 18.6 MB</small></span><Icon name="chevron" size={18}/></div><div className="storage-bar"><span/></div></section>
    <section className="settings-group"><div className="setting-row"><span><strong>About Trailmark</strong><small>Version 1.0.0</small></span><Icon name="chevron" size={18}/></div></section>
  </main>;
}

function ImportSheet({ close }: { close: () => void }) {
  const [state, setState] = useState<"idle" | "loading" | "success">("idle");
  const start = () => { setState("loading"); setTimeout(() => setState("success"), 900); };
  return <div className="scrim" onClick={close}><section className="import-sheet" onClick={e => e.stopPropagation()}><span className="grabber"/>
    {state === "idle" && <><div className="import-icon"><Icon name="upload" size={30}/></div><strong>Import a GPX track</strong><p>Choose a .gpx file from your device. Your track stays private and stored locally.</p><button className="primary wide" onClick={start}>Choose file</button><button className="text-button" onClick={close}>Cancel</button></>}
    {state === "loading" && <><div className="loader"/><strong>Reading your track…</strong><p>Parsing waypoints, elevation and route data.</p><div className="progress"><span/></div></>}
    {state === "success" && <><div className="success-mark">✓</div><strong>Track imported</strong><p>Cedar Ridge Loop is ready to explore.</p><button className="primary wide" onClick={close}>View track</button></>}
  </section></div>;
}

export default function App() {
  const [screen, setScreen] = useState<Screen>("home");
  const [importing, setImporting] = useState(false);
  const nav = screen === "details" ? "library" : screen;
  return <div className="app-shell">
    <div className="phone">
      {screen === "home" && <Home go={setScreen} importGpx={() => setImporting(true)}/>}
      {screen === "library" && <Library go={setScreen} importGpx={() => setImporting(true)}/>}
      {screen === "details" && <Details go={setScreen}/>}
      {screen === "map" && <MapScreen go={setScreen}/>}
      {screen === "settings" && <Settings/>}
      {screen !== "map" && <nav className="bottom-nav">
        {(["home","library","map","settings"] as Screen[]).map(item => <button key={item} className={nav === item ? "active" : ""} onClick={() => setScreen(item)}><span><Icon name={item === "library" ? "list" : item}/></span><small>{item === "library" ? "Tracks" : item[0].toUpperCase()+item.slice(1)}</small></button>)}
      </nav>}
      {importing && <ImportSheet close={() => setImporting(false)}/>}
    </div>
    <aside className="showcase-note"><span>TRAILMARK</span><strong>Your routes.<br/>Offline. Uncomplicated.</strong><p>A map-first GPX viewer designed for Android and built around local ownership.</p><div><i/><span>Material 3-inspired<br/><b>Light theme · 390 × 844</b></span></div></aside>
  </div>;
}
