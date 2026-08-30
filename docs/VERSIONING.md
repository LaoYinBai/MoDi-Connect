# Version management

`version.json` at the repository root is the only manually maintained product version source.

- `version`: user-facing `major.minor.patch`
- `build`: monotonic Android `versionCode` and Windows build identity
- `channel`: reserved `stable`, `beta`, or `dev`
- commit: resolved from Git automatically at build time; development builds fall back to `unknown`

Commands:

```powershell
pwsh -File scripts/version.ps1 show
pwsh -File scripts/version.ps1 verify
pwsh -File scripts/version.ps1 bump-build
pwsh -File scripts/version.ps1 set-version 1.0.1
```

Debug builds never modify the Build. Before a release, finish the source changes, run `bump-build`, commit the new `version.json` last, and run `verify -RequireGit`. Formal verification rejects a dirty tree or a commit that reuses its parent's Build.
