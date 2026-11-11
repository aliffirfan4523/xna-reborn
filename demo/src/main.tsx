import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { StillContext } from '../kit/reduced-motion'
import '../kit/stageshow.css'
import { Flow, stillAlt } from './Flow'
import { STEPS } from './facts'
import './gallery.css'

/**
 * Routes by query string, so the film tools can open one scene at a time:
 *   /                   gallery of every scene
 *   /?scene=still-1     one still (1 to 6, one per screen)
 *   /?scene=demo        the scripted cursor demo of the whole flow
 *   &still=1            force the reduced-motion poster, as a viewer with that setting sees it
 */
const params = new URLSearchParams(location.search)
const scene = params.get('scene')
const forceStill = params.get('still') === '1'

function Scene({ name }: { name: string }) {
  if (name === 'demo') {
    return (
      <div role="img" aria-label="Cursor demo of the XNA Reborn flow on Pirate-Treasure-Hunter: Open, Report, Migration setup, Review changes, Build and run, Summary. Score 81 to 91, 21 of 21 assets built, 0 hand edits.">
        <Flow demo />
      </div>
    )
  }
  const n = Number(name.replace('still-', ''))
  if (!Number.isInteger(n) || n < 1 || n > STEPS.length) throw new Error(`Unknown scene: ${name}`)
  return (
    <div role="img" aria-label={stillAlt[n - 1]}>
      <Flow step={n - 1} />
    </div>
  )
}

function Gallery() {
  return (
    <main className="gallery">
      <h1>XNA Reborn demo scenes</h1>
      <p>Faux UI drawn with Stageshow. Placeholder art only. Not the app.</p>
      <h2>Cursor demo</h2>
      <section><Scene name="demo" /></section>
      <h2>One still per screen</h2>
      {STEPS.map((label, i) => (
        <section key={label}>
          <h3>{i + 1}. {label}</h3>
          <Scene name={`still-${i + 1}`} />
        </section>
      ))}
    </main>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <StillContext.Provider value={forceStill}>
      <div id="example">{scene ? <Scene name={scene} /> : <Gallery />}</div>
    </StillContext.Provider>
  </StrictMode>,
)
