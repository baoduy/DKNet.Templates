#!/usr/bin/env node
// Copies package.json's version into plugin/.claude-plugin/plugin.json and plugin.json. Runs automatically as the
// "version" npm script, so `npm version <x.y.z> --no-git-tag-version` stamps all three files. The repository
// keeps the placeholder 0.0.0: the real version is the template release version, injected by the publish-npm
// job in .github/workflows/publish-nuget-github.yml right before `npm publish` — the same way the nuspec
// <version> is stamped for the NuGet template package.
import { readFileSync, writeFileSync } from 'node:fs'

const version = JSON.parse(readFileSync('package.json', 'utf8')).version
for (const file of ['plugin/.claude-plugin/plugin.json', 'plugin.json']) {
  const manifest = JSON.parse(readFileSync(file, 'utf8'))
  manifest.version = version
  writeFileSync(file, JSON.stringify(manifest, null, 2) + '\n')
}
console.log(version)
