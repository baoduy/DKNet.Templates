#!/usr/bin/env node
/**
 * DKNet.Templates — Code Graph Analyzer
 *
 * Zero dependencies — uses only Node.js built-ins + Neo4j HTTP API (fetch).
 * Requires Node.js >= 18.
 *
 * Run:
 *   node graph/analyze.mjs
 *   node graph/analyze.mjs --url http://localhost:7474 --password mypass
 *
 * Or via npx (no install):
 *   npx --yes tsx graph/analyze.mjs
 */

import { readFile, readdir } from 'node:fs/promises';
import { join, basename, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

// ---------------------------------------------------------------------------
// Config — override with env vars or CLI flags
// ---------------------------------------------------------------------------
const args = Object.fromEntries(
  process.argv.slice(2)
    .map(a => a.split('='))
    .filter(a => a[0].startsWith('--'))
    .map(([k, v]) => [k.slice(2), v])
);

const NEO4J_URL  = args.url      ?? process.env.NEO4J_URL      ?? 'http://localhost:7474';
const NEO4J_USER = args.user     ?? process.env.NEO4J_USER     ?? 'neo4j';
const NEO4J_PASS = args.password ?? process.env.NEO4J_PASS     ?? 'codegraph123';

const __dirname = dirname(fileURLToPath(import.meta.url));
const SRC_ROOT  = join(__dirname, '..', 'src', 'ApiEndpoints');

// ---------------------------------------------------------------------------
// Neo4j HTTP API  (no driver needed — just fetch)
// ---------------------------------------------------------------------------
const AUTH_HEADER = `Basic ${Buffer.from(`${NEO4J_USER}:${NEO4J_PASS}`).toString('base64')}`;

async function cypher(statements) {
  const payload = {
    statements: [statements].flat().map(s =>
      typeof s === 'string' ? { statement: s } : s
    ),
  };

  const res = await fetch(`${NEO4J_URL}/db/neo4j/tx/commit`, {
    method:  'POST',
    headers: {
      Authorization:  AUTH_HEADER,
      'Content-Type': 'application/json',
      Accept:         'application/json',
    },
    body: JSON.stringify(payload),
  });

  if (!res.ok) throw new Error(`Neo4j HTTP ${res.status}: ${await res.text()}`);

  const json = await res.json();
  if (json.errors?.length) throw new Error(`Cypher error: ${JSON.stringify(json.errors)}`);
  return json.results;
}

// ---------------------------------------------------------------------------
// Filesystem walker (skips bin / obj / Migrations / generated files)
// ---------------------------------------------------------------------------
const SKIP_DIRS = new Set(['bin', 'obj', 'TestResults', '.git', 'node_modules']);

async function* walk(dir, ext) {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory() && !SKIP_DIRS.has(entry.name)) {
      yield* walk(full, ext);
    } else if (entry.isFile() && entry.name.endsWith(ext) && !entry.name.includes('.g.')) {
      yield full;
    }
  }
}

// ---------------------------------------------------------------------------
// .csproj parser
// ---------------------------------------------------------------------------
async function parseCsproj(path) {
  const text  = await readFile(path, 'utf8');
  const name  = basename(path, '.csproj');

  const projectRefs = [...text.matchAll(/<ProjectReference[^>]+Include="([^"]+)"/g)]
    .map(m => basename(m[1].replace(/\\/g, '/'), '.csproj'));

  const packageRefs = [...text.matchAll(/<PackageReference\s+Include="([^"]+)"(?:[^/]*?Version="([^"]*)")?/g)]
    .map(m => ({ name: m[1], version: m[2] ?? '' }));

  let type = 'Library';
  if (text.includes('Microsoft.NET.Sdk.Web'))  type = 'Web';
  if (text.includes('Aspire.AppHost.Sdk'))      type = 'AppHost';
  if (name.includes('Tests'))                   type = 'Test';

  return { name, type, layer: inferLayer(name), projectRefs, packageRefs };
}

// ---------------------------------------------------------------------------
// .cs parser
// ---------------------------------------------------------------------------
// Matches: [modifiers] class|record|interface|struct Name [<generics>] [: bases]
const DECL_RE = /(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly)\s+)+(?<kind>class|record|interface|struct)\s+(?<name>\w+)(?:\s*<[^>]+>)?(?:\s*:\s*(?<bases>[^\n{]+))?/gm;

async function parseCsFile(path, projectName) {
  const text = await readFile(path, 'utf8');
  if (text.includes('Migrations/') || path.includes('/Migrations/')) return [];

  const ns = text.match(/namespace\s+([\w.]+)/)?.[1] ?? '';
  const classes = [];

  for (const m of text.matchAll(DECL_RE)) {
    const { kind, name, bases: rawBases } = m.groups;
    if (name.length < 3) continue;

    const mods  = m[0].toLowerCase();
    const bases = (rawBases ?? '').split(',')
      .map(b => b.trim().replace(/<.*/, '').trim())
      .filter(b => /^\w/.test(b) && b.length > 1);

    classes.push({
      name,
      kind,
      namespace:  ns,
      project:    projectName,
      layer:      inferLayer(projectName),
      pattern:    inferPattern(name),
      isSealed:   mods.includes('sealed'),
      isAbstract: mods.includes('abstract'),
      isStatic:   mods.includes('static'),
      bases,
    });
  }
  return classes;
}

// ---------------------------------------------------------------------------
// Inference helpers
// ---------------------------------------------------------------------------
const LAYER_MAP = [
  ['BDDTests',    'Tests'],
  ['Tests',       'Tests'],
  ['AppHost',     'AppHost'],
  ['AppServices', 'AppServices'],
  ['Domains',     'Domains'],
  ['Infra',       'Infra'],
  ['Share',       'Share'],
  ['Api',         'Api'],
];

function inferLayer(projectName) {
  return LAYER_MAP.find(([s]) => projectName.endsWith(s))?.[1] ?? 'Unknown';
}

const PATTERN_RULES = [
  [/EventHandler$/,         'EventHandler'],
  [/Handler$/,              'Handler'],
  [/Validator$/,            'Validator'],
  [/Request$/,              'Command'],
  [/Dto$/,                  'DTO'],
  [/Event$/,                'DomainEvent'],
  [/Endpoint$/,             'Endpoint'],
  [/Configs?$/,             'EfConfig'],
  [/StaticData$/,           'DataSeed'],
  [/^Spec\w+/,              'Specification'],
  [/AggregateRoot$/,        'DomainBase'],
  [/DbContext$/,            'DbContext'],
  [/Setup$/,                'Setup'],
  [/Repository$|Repo$/,     'Repository'],
  [/Service$/,              'Service'],
];

function inferPattern(name) {
  return PATTERN_RULES.find(([re]) => re.test(name))?.[1] ?? '';
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------
async function main() {
  console.log(`Connecting to Neo4j at ${NEO4J_URL} ...`);
  await cypher('RETURN 1 AS ok');
  console.log('Connected.\n');

  // ── Collect ────────────────────────────────────────────────────────────────
  const projects   = [];
  const allClasses = [];

  for await (const csproj of walk(SRC_ROOT, '.csproj')) {
    const proj = await parseCsproj(csproj);
    projects.push(proj);

    for await (const csFile of walk(dirname(csproj), '.cs')) {
      const classes = await parseCsFile(csFile, proj.name);
      allClasses.push(...classes);
    }
  }

  console.log(`Discovered: ${projects.length} projects | ${allClasses.length} classes/records/interfaces\n`);

  // ── Layers ─────────────────────────────────────────────────────────────────
  console.log('Upserting layers...');
  await cypher([
    { statement: "MERGE (:Layer {name: 'Api',         description: 'Entry point, HTTP endpoints, auth, OpenAPI'})" },
    { statement: "MERGE (:Layer {name: 'AppServices', description: 'CQRS handlers, validators, DTOs, events'})" },
    { statement: "MERGE (:Layer {name: 'Domains',     description: 'Entities, aggregate roots, value objects'})" },
    { statement: "MERGE (:Layer {name: 'Infra',       description: 'EF Core, repos, event publisher, service bus'})" },
    { statement: "MERGE (:Layer {name: 'Share',       description: 'Shared constants, options, base types'})" },
    { statement: "MERGE (:Layer {name: 'AppHost',     description: 'Aspire orchestration only'})" },
    { statement: "MERGE (:Layer {name: 'Tests',       description: 'Unit and integration tests'})" },
  ]);

  // ── Projects ───────────────────────────────────────────────────────────────
  console.log('Upserting projects and dependencies...');
  for (const p of projects) {
    await cypher([
      {
        statement: 'MERGE (p:Project {name: $name}) SET p.type = $type, p.layer = $layer',
        parameters: { name: p.name, type: p.type, layer: p.layer },
      },
      {
        statement: 'MATCH (p:Project {name: $name}), (l:Layer {name: $layer}) MERGE (p)-[:IN_LAYER]->(l)',
        parameters: { name: p.name, layer: p.layer },
      },
      ...p.projectRefs.map(ref => ({
        statement: 'MERGE (a:Project {name: $a}) MERGE (b:Project {name: $b}) MERGE (a)-[:DEPENDS_ON]->(b)',
        parameters: { a: p.name, b: ref },
      })),
      ...p.packageRefs.map(pkg => ({
        statement: `
          MERGE (pkg:NugetPackage {name: $pkg})
            SET pkg.version = $ver
          WITH pkg
          MATCH (proj:Project {name: $proj})
          MERGE (proj)-[:USES_PACKAGE]->(pkg)
        `,
        parameters: { pkg: pkg.name, ver: pkg.version, proj: p.name },
      })),
    ]);
  }

  // ── Classes ────────────────────────────────────────────────────────────────
  console.log('Upserting classes...');
  for (const cls of allClasses) {
    await cypher([
      {
        statement: `
          MERGE (c:Class {name: $name, project: $project})
          SET c.kind      = $kind,
              c.namespace = $namespace,
              c.layer     = $layer,
              c.pattern   = $pattern,
              c.isSealed   = $isSealed,
              c.isAbstract = $isAbstract,
              c.isStatic   = $isStatic
        `,
        parameters: {
          name: cls.name,  project: cls.project, kind: cls.kind,
          namespace: cls.namespace, layer: cls.layer, pattern: cls.pattern,
          isSealed: cls.isSealed, isAbstract: cls.isAbstract, isStatic: cls.isStatic,
        },
      },
      {
        statement: `
          MATCH (c:Class {name: $name, project: $project}), (p:Project {name: $project})
          MERGE (c)-[:IN_PROJECT]->(p)
        `,
        parameters: { name: cls.name, project: cls.project },
      },
      ...cls.bases.map(base => ({
        statement: `
          MATCH (child:Class  {name: $child, project: $project})
          MATCH (parent:Class {name: $parent})
          MERGE (child)-[:INHERITS]->(parent)
        `,
        parameters: { child: cls.name, project: cls.project, parent: base },
      })),
    ]);
  }

  // ── Feature tagging ────────────────────────────────────────────────────────
  console.log('Tagging feature membership...');
  await cypher(`
    MATCH (c:Class), (f:Feature)
    WHERE c.namespace CONTAINS f.name
       OR (c.namespace CONTAINS 'Profiles' AND f.name = 'CustomerProfiles')
    MERGE (c)-[:PART_OF_FEATURE]->(f)
  `);

  // ── Summary ────────────────────────────────────────────────────────────────
  const counts = await cypher('MATCH (n) RETURN labels(n)[0] AS label, count(n) AS count ORDER BY count DESC');
  console.log('\nGraph node counts:');
  for (const row of counts[0]?.data ?? []) {
    console.log(`  ${row.row[0].padEnd(16)} ${row.row[1]}`);
  }
  console.log('\nDone.');
  console.log('  Browser  → http://localhost:7474');
  console.log('  NeoDash  → http://localhost:5005');
}

main().catch(err => { console.error(err.message); process.exit(1); });
