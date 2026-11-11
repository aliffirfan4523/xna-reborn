import { Badge, Bar, Button, Count, Icon, Meter, Stage, States, Text, Toggle, Window, type Act } from '../kit'
import { ASSETS, HAND_EDITS, PROJECT, SCORE_AFTER, SCORE_BEFORE, STEPS } from './facts'
import './flow.css'

/**
 * The XNA Reborn flow as faux UI: Open, Report, Migration setup, Review changes, Build and run, Summary.
 * One scene state drives every screen, so a still is just the state at one step and the
 * cursor demo is a script that walks the same state through all six.
 */
export type S = {
  step: number
  /** Analysis progress on Open, 0 to 100 */
  analyzing: number
  /** The compatibility score shown on Report and Summary */
  score: number
  /** Migration progress on Migration setup, 0 to 100 */
  migrating: number
  /** Assets built so far on Build and run */
  built: number
  running: boolean
}

export const initial: S = { step: 0, analyzing: 0, score: 0, migrating: 0, built: 0, running: false }

/** The state each screen is shown in as a still: the moment the screen's story has landed. */
export const stills: S[] = [
  { ...initial, step: 0, analyzing: 100 },
  { ...initial, step: 1, score: SCORE_BEFORE },
  { ...initial, step: 2, score: SCORE_BEFORE },
  { ...initial, step: 3, score: SCORE_BEFORE },
  { ...initial, step: 4, score: SCORE_BEFORE, built: ASSETS, running: true },
  { ...initial, step: 5, score: SCORE_AFTER, built: ASSETS, running: true },
]

/** What each still says, for alt text. */
export const stillAlt = [
  `Open: ${PROJECT} analyzed, with a progress bar at 100 percent and a Recent projects list.`,
  `Report: compatibility score ${SCORE_BEFORE} out of 100, with a findings list and a Continue to migration setup button.`,
  'Migration setup: output folder, MonoGame target DesktopGL, options, and the migration plan with Preview and Migrate buttons.',
  'Review changes: a list of changed files beside a diff, with Continue to Build and run.',
  `Build and run: ${ASSETS} of ${ASSETS} assets built, a build log, and the game running as a faux placeholder.`,
  `Summary: compatibility score from ${SCORE_BEFORE} to ${SCORE_AFTER}, ${ASSETS} of ${ASSETS} assets built, ${HAND_EDITS} hand edits.`,
]

export const WIDTH = 1000
export const HEIGHT = 540


/** Contrast: the kit's default text grays are about 4:1 on white; these clear 4.5:1. */
const TOKENS = { '--ss-text': '#55555a', '--ss-text-faint': '#64646a' } as const

async function script(a: Act<S>) {
  // Open: browse, analyze
  await a.wait(800)
  await a.move('browse')
  await a.wait(300)
  await a.click()
  for (const analyzing of [25, 60, 100]) {
    await a.set({ analyzing })
    await a.wait(450)
  }
  await a.wait(400)

  // Report: the score lands
  await a.set({ step: 1 })
  await a.wait(200)
  await a.set({ score: SCORE_BEFORE })
  await a.wait(2200)
  await a.move('to-setup')
  await a.wait(300)
  await a.click()

  // Migration setup: migrate
  await a.set({ step: 2 })
  await a.wait(1600)
  await a.move('migrate')
  await a.wait(300)
  await a.click()
  for (const migrating of [35, 70, 100]) {
    await a.set({ migrating })
    await a.wait(450)
  }
  await a.wait(300)

  // Review changes
  await a.set({ step: 3 })
  await a.wait(2000)
  await a.move('to-build')
  await a.wait(300)
  await a.click()

  // Build and run: assets build, game starts
  await a.set({ step: 4 })
  await a.wait(1000)
  await a.move('build-run')
  await a.wait(300)
  await a.click()
  for (const built of [7, 14, ASSETS]) {
    await a.set({ built })
    await a.wait(500)
  }
  await a.set({ running: true })
  await a.wait(1800)
  await a.move('to-summary')
  await a.wait(300)
  await a.click()

  // Summary: before and after. The payoff holds.
  await a.set({ step: 5, score: SCORE_AFTER })
  await a.wait(4200)

  // Back to the start, with a visible cause: Start over.
  await a.move('start-over')
  await a.wait(300)
  await a.click()
  await a.set({ step: 0, analyzing: 0, score: 0, migrating: 0, built: 0, running: false })
  await a.move({ x: 760, y: 470 })
  await a.wait(500)
}

function Stepper({ step }: { step: number }) {
  return (
    <nav className="xr-steps" aria-label="Workflow steps">
      {STEPS.map((label, i) => (
        <div key={label} className="xr-step" data-state={i < step ? 'done' : i === step ? 'current' : 'todo'}>
          <span className="xr-step__n"><span className="xr-step__num">{i + 1}</span><span className="xr-step__tick"><Icon name="check" size={11} stroke={2.2} /></span></span>
          <span className="xr-step__label">{label}</span>
        </div>
      ))}
    </nav>
  )
}

function Gauge({ score, size = 132 }: { score: number; size?: number }) {
  const r = 52
  const c = 2 * Math.PI * r
  return (
    <div className="xr-gauge" style={{ width: size, height: size }}>
      <svg viewBox="0 0 120 120" width={size} height={size} aria-hidden="true">
        <circle cx="60" cy="60" r={r} className="xr-gauge__track" />
        <circle cx="60" cy="60" r={r} className="xr-gauge__arc" strokeDasharray={c} strokeDashoffset={c * (1 - score / 100)} transform="rotate(-90 60 60)" />
      </svg>
      <div className="xr-gauge__num" style={{ opacity: score > 0 ? 1 : 0 }}>
        <Count value={score} duration={900} />
        <span className="xr-gauge__of">/ 100</span>
      </div>
    </div>
  )
}

function Open({ s }: { s: S }) {
  return (
    <div className="xr-open">
      <div className="xr-drop">
        <Icon name="plus" size={22} />
        <Text className="xr-h2">Drop an XNA project folder, .sln or .csproj here</Text>
        <Text className="xr-faint">or</Text>
        <div className="xr-row">
          <Button data-target="browse" variant="primary">Browse folder...</Button>
          <Button>Browse .sln or .csproj...</Button>
        </div>
      </div>
      <div className="xr-progress" data-show={s.analyzing > 0}>
        <div className="xr-row xr-between">
          <Text className="xr-strong">{PROJECT}</Text>
          <Button size="small" variant="tertiary">Cancel</Button>
        </div>
        <Meter value={s.analyzing / 100} />
      </div>
      <div className="xr-recent">
        <Text className="xr-label">Recent projects</Text>
        <Text className="xr-faint">{PROJECT}</Text>
      </div>
    </div>
  )
}

const FINDING_TONES = ['danger', 'warning', 'warning', 'neutral', 'neutral'] as const
const FINDING_NAMES = ['Severity: error', 'Severity: warning', 'Severity: warning', 'Severity: info', 'Severity: info']

function Report({ s }: { s: S }) {
  return (
    <div className="xr-report">
      <aside className="xr-card xr-score">
        <Text className="xr-label">Compatibility score</Text>
        <Gauge score={s.score} />
        <div className="xr-tip">
          <Text className="xr-strong">Start here</Text>
          <Bar w="90%" h={6} tone={2} />
          <Bar w="70%" h={6} tone={2} />
        </div>
      </aside>
      <section className="xr-card xr-findings">
        <div className="xr-row xr-between">
          <Text className="xr-h2">Findings</Text>
          <div className="xr-row">
            <Button size="small">Export Markdown</Button>
            <Button size="small">Copy report</Button>
          </div>
        </div>
        <div className="xr-list">
          {FINDING_TONES.map((tone, i) => (
            <div key={i} className="xr-finding" data-selected={i === 0}>
              <span className="xr-dot" data-tone={tone} role="img" aria-label={FINDING_NAMES[i]} />
              <Bar w={`${58 - i * 7}%`} h={7} tone={i === 0 ? 4 : 2} />
              <Bar w={44} h={7} tone={1} />
            </div>
          ))}
        </div>
        <div className="xr-row xr-end">
          <Button data-target="to-setup" variant="primary" trailingIcon={<Icon name="chevron-right" />}>Continue to migration setup</Button>
        </div>
      </section>
    </div>
  )
}

function Setup({ s }: { s: S }) {
  const options = ['Add compatibility shims', 'Substitute fonts that only XNA installed', 'Let the window be resized']
  return (
    <div className="xr-setup">
      <section className="xr-card xr-stack">
        <Text className="xr-label">Output folder</Text>
        <div className="xr-row">
          <div className="xr-input"><Bar w="62%" h={7} tone={2} /></div>
          <Button size="small">Browse...</Button>
        </div>
        <Text className="xr-faint">The migrated copy goes here. Your original project is never changed.</Text>
        <Text className="xr-label">MonoGame target</Text>
        <div className="xr-radio" data-on><span />DesktopGL (OpenGL, works on Windows, Linux and macOS)</div>
        <div className="xr-radio"><span />WindowsDX (DirectX, Windows only)</div>
        <Text className="xr-label">Options</Text>
        {options.map((o) => (
          <div key={o} className="xr-row xr-between">
            <Text>{o}</Text>
            <Toggle on={false} aria-label={o} />
          </div>
        ))}
      </section>
      <section className="xr-card xr-stack">
        <Text className="xr-label">Migration plan</Text>
        <div className="xr-plan">
          {[0, 1, 2, 3, 4].map((i) => (
            <div key={i} className="xr-row">
              <Icon name="check" size={13} stroke={2} />
              <Bar w={`${70 - i * 9}%`} h={7} tone={2} />
            </div>
          ))}
        </div>
        <Meter value={s.migrating / 100} />
        <div className="xr-row xr-end">
          <Button data-target="preview">Preview</Button>
          <Button data-target="migrate" variant="primary">Migrate</Button>
        </div>
      </section>
    </div>
  )
}

function Diff() {
  return (
    <div className="xr-diff">
      <div className="xr-banner">These are the changes a migration would make. No file has been written yet.</div>
      <div className="xr-diff__body">
        <div className="xr-card xr-files" role="list" aria-label="Changed files">
          {[0, 1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="xr-file" data-selected={i === 1}>
              <Icon name="file" size={13} />
              <Bar w={`${74 - (i % 3) * 12}%`} h={7} tone={i === 1 ? 4 : 2} />
            </div>
          ))}
        </div>
        <div className="xr-card xr-code">
          {[
            ['', 62], ['', 48], ['del', 70], ['add', 66], ['add', 54], ['', 40], ['', 58], ['del', 44], ['add', 52], ['', 36],
          ].map(([kind, w], i) => (
            <div key={i} className="xr-codeline" data-kind={kind || undefined}>
              <span className="xr-gutter">{i + 1}</span>
              <Bar w={`${w}%`} h={6} tone={kind ? 4 : 2} />
            </div>
          ))}
        </div>
      </div>
      <div className="xr-row xr-between">
        <Button>Back to Migration setup</Button>
        <Button data-target="to-build" variant="primary" trailingIcon={<Icon name="chevron-right" />}>Continue to Build and run</Button>
      </div>
    </div>
  )
}

function BuildRun({ s }: { s: S }) {
  const done = s.built >= ASSETS
  return (
    <div className="xr-build">
      <section className="xr-card xr-stack">
        <div className="xr-row xr-between">
          <Text className="xr-label">Build log</Text>
          <div className="xr-row">
            <Button data-target="build-run" variant="primary" icon={<Icon name="play" />}>Build and Run</Button>
            <Button>Build</Button>
          </div>
        </div>
        <div className="xr-log">
          <Text>{s.built === 0 ? 'Ready.' : `${s.built} of ${ASSETS} assets built`}</Text>
          <Meter value={s.built / ASSETS} tone={done ? 'success' : 'accent'} />
          <Text className="xr-ok" data-show={done}>Build succeeded. 0 warnings, 0 errors.</Text>
        </div>
        <div className="xr-row xr-between">
          <Badge tone={s.running ? 'success' : 'neutral'}><States value={s.running ? 'on' : 'off'} states={{ on: 'Game running', off: 'Not running' }} /></Badge>
          <div className="xr-row">
            <Button size="small">Stop</Button>
            <Button data-target="to-summary" size="small" trailingIcon={<Icon name="chevron-right" />}>Go to Summary</Button>
          </div>
        </div>
      </section>
      <div className="xr-game" data-on={s.running} role="img" aria-label="Placeholder for the running game, drawn for this demo">
        <div className="xr-game__sea" />
        <div className="xr-game__island" />
        <div className="xr-game__boat" />
        <Text className="xr-game__tag">Placeholder art</Text>
      </div>
    </div>
  )
}

function Summary({ s }: { s: S }) {
  return (
    <div className="xr-summary">
      <section className="xr-card xr-ba">
        <Text className="xr-label">Compatibility score</Text>
        <div className="xr-row xr-center">
          <div className="xr-col"><Text className="xr-faint">Before</Text><Gauge score={SCORE_BEFORE} size={120} /></div>
          <Icon name="chevron-right" size={22} />
          <div className="xr-col"><Text className="xr-faint">After</Text><Gauge score={s.score} size={120} /></div>
        </div>
      </section>
      <section className="xr-card xr-stack">
        <div className="xr-fact"><Text className="xr-faint">Build</Text><Text className="xr-strong">Succeeded, 0 warnings, 0 errors</Text></div>
        <div className="xr-fact"><Text className="xr-faint">Assets built</Text><Text className="xr-strong">{`${ASSETS} of ${ASSETS}`}</Text></div>
        <div className="xr-fact"><Text className="xr-faint">Hand edits</Text><Text className="xr-strong">{String(HAND_EDITS)}</Text></div>
        <div className="xr-fact"><Text className="xr-faint">Manual work left</Text><Text className="xr-strong">None.</Text></div>
        <div className="xr-row">
          <Button>Open output folder</Button>
          <Button>Open manifest</Button>
          <Button data-target="start-over" variant="tertiary">Start over</Button>
        </div>
      </section>
    </div>
  )
}

function Screen({ s }: { s: S }) {
  // Every screen stays mounted in one cell, so leaving one fades it out instead of removing it.
  const screens = [<Open s={s} />, <Report s={s} />, <Setup s={s} />, <Diff />, <BuildRun s={s} />, <Summary s={s} />]
  return (
    <>
      {screens.map((screen, i) => (
        <div key={STEPS[i]} className="xr-screen" data-active={i === s.step} aria-hidden={i !== s.step}>
          {screen}
        </div>
      ))}
    </>
  )
}

/** One stage: the whole flow, as a cursor demo (`demo`) or as a still at `step`. */
export function Flow({ step, demo = false }: { step?: number; demo?: boolean }) {
  const start = demo ? initial : stills[step ?? 0]
  return (
    <Stage
      width={WIDTH}
      height={HEIGHT}
      initial={start}
      poster={demo ? stills[5] : undefined}
      script={demo ? script : undefined}
      cursor={demo ? { at: { x: 760, y: 470 }, style: 'arrow', ripple: true } : undefined}
      tokens={TOKENS}
      className="xr-stage"
    >
      {(s) => (
        <Window title="XNA Reborn" className="xr-window">
          <Stepper step={s.step} />
          <main className="xr-screens">
            <Screen s={s} />
          </main>
        </Window>
      )}
    </Stage>
  )
}
