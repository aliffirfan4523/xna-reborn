# Adding a migration rule

1. Reproduce the problem with a small fixture project.
2. Add a rule to `rules/xna-to-monogame.json` with id, title, severity, classification, match, explanation, and resolution.
3. Prefer an existing `match.type`. Add a new match type only if no existing one fits.
4. Add a positive fixture test and a negative fixture test.
5. Update the snapshot reports and review the diff.
6. If the rule is AutoFixable, link it to the migrator spec task that implements the fix.
