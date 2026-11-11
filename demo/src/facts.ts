/**
 * Every number and label the scenes show. Nothing else is invented.
 *
 * Screen names and button labels come from src/XnaReborn.App (MainViewModel steps and the *View.axaml files).
 * The Pirate-Treasure-Hunter numbers come from the work-item brief and .kiro/specs/content-conversion/tasks.md
 * (21 assets built, 0 warnings, 0 errors) and .kiro/specs/project-migrator/tasks.md.
 */
export const PROJECT = 'Pirate-Treasure-Hunter'

export const STEPS = ['Open', 'Report', 'Migration setup', 'Review changes', 'Build and run', 'Summary'] as const

export const SCORE_BEFORE = 81
export const SCORE_AFTER = 91 // projected after automatic fixes
export const ASSETS = 21
export const HAND_EDITS = 0
