#!/usr/bin/env node
/**
 * DKNet.Templates — C# Metadata Graph Analyzer
 *
 * Scans all .cs and .md files under src/, extracts structural metadata only
 * (no source body text), and pushes the index into Neo4j via HTTP.
 * All logic lives in this single file; nothing is written to disk.
 *
 * Zero npm dependencies — Node.js built-ins + Neo4j HTTP API only.
 * Requires Node.js >= 18.
 *
 * Run:
 *   node graph/analyze.mjs
 *   node graph/analyze.mjs --url=http://localhost:7474 --password=mypass
 *   node graph/analyze.mjs --dry-run          # parse only, no Neo4j writes
 */

import { readFile, readdir, stat } from 'node:fs/promises';
import { join, basename, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';

// ─────────────────────────────────────────────────────────────────────────────
// CLI / env config
// ─────────────────────────────────────────────────────────────────────────────
const cliArgs = Object.fromEntries(
  process.argv.slice(2)
    .filter(a => a.startsWith('--'))
    .map(a => { const [k, ...rest] = a.slice(2).split('='); return [k, rest.join('=') || true]; })
);

const NEO4J_URL  = cliArgs.url      ?? process.env.NEO4J_URL      ?? 'http://localhost:7474';
const NEO4J_USER = cliArgs.user     ?? process.env.NEO4J_USER     ?? 'neo4j';
const NEO4J_PASS = cliArgs.password ?? process.env.NEO4J_PASS     ?? 'codegraph123';
const DRY_RUN    = cliArgs['dry-run'] === true;

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT  = join(__dirname, '..');
const SRC_ROOT   = join(REPO_ROOT, 'src');

// ─────────────────────────────────────────────────────────────────────────────
// Neo4j HTTP client  (no driver — plain fetch)
// ─────────────────────────────────────────────────────────────────────────────
const AUTH_HEADER = `Basic ${Buffer.from(`${NEO4J_USER}:${NEO4J_PASS}`).toString('base64')}`;

async function cypher(statements) {
  if (DRY_RUN) return [];
  const stmtArray = [statements].flat().map(s =>
    typeof s === 'string' ? { statement: s } : s
  );
  if (stmtArray.length === 0) return [];

  const res = await fetch(`${NEO4J_URL}/db/neo4j/tx/commit`, {
    method:  'POST',
    headers: { Authorization: AUTH_HEADER, 'Content-Type': 'application/json', Accept: 'application/json' },
    body:    JSON.stringify({ statements: stmtArray }),
  });
  if (!res.ok) throw new Error(`Neo4j HTTP ${res.status}: ${await res.text()}`);
  const json = await res.json();
  if (json.errors?.length) throw new Error(`Cypher error: ${JSON.stringify(json.errors)}`);
  return json.results;
}

// Batch an array of statement objects into one transaction
async function cypherBatch(statements) {
  if (!statements.length) return;
  const BATCH = 50;
  for (let i = 0; i < statements.length; i += BATCH) {
    await cypher(statements.slice(i, i + BATCH));
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Schema / indexes  (idempotent)
// ─────────────────────────────────────────────────────────────────────────────
async function ensureSchema() {
  const constraints = [
    `CREATE CONSTRAINT sf_path_unique   IF NOT EXISTS FOR (n:SourceFile)   REQUIRE n.path     IS UNIQUE`,
    `CREATE CONSTRAINT cls_key_unique   IF NOT EXISTS FOR (n:Classes)  REQUIRE n.classKey IS UNIQUE`,
    `CREATE CONSTRAINT mth_key_unique   IF NOT EXISTS FOR (n:Methods) REQUIRE n.methodKey IS UNIQUE`,
    `CREATE CONSTRAINT run_id_unique    IF NOT EXISTS FOR (n:IndexRun)     REQUIRE n.runId    IS UNIQUE`,
    `CREATE CONSTRAINT pkg_name_unique  IF NOT EXISTS FOR (n:NugetPackage) REQUIRE n.name     IS UNIQUE`,
    `CREATE CONSTRAINT proj_name_unique IF NOT EXISTS FOR (n:Project)      REQUIRE n.name     IS UNIQUE`,
    `CREATE CONSTRAINT ns_key_unique    IF NOT EXISTS FOR (n:Namespace)    REQUIRE n.nsKey    IS UNIQUE`,
  ];
  try {
    for (const s of constraints) await cypher(s);
  } catch (e) {
    // Older Neo4j versions: fall back to indexes
    const indexes = [
      `CREATE INDEX sf_path    IF NOT EXISTS FOR (n:SourceFile)   ON (n.path)`,
      `CREATE INDEX cls_key    IF NOT EXISTS FOR (n:Classes)  ON (n.classKey)`,
      `CREATE INDEX mth_key    IF NOT EXISTS FOR (n:Methods) ON (n.methodKey)`,
      `CREATE INDEX cls_name   IF NOT EXISTS FOR (n:Classes)  ON (n.name)`,
      `CREATE INDEX mth_name   IF NOT EXISTS FOR (n:Methods) ON (n.name)`,
    ];
    for (const s of indexes) { try { await cypher(s); } catch { /* skip */ } }
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Filesystem walker
// ─────────────────────────────────────────────────────────────────────────────
const SKIP_DIRS = new Set(['bin', 'obj', 'TestResults', '.git', 'node_modules', 'Migrations']);

async function* walkFiles(dir, exts) {
  const extSet = exts instanceof Set ? exts : new Set(Array.isArray(exts) ? exts : [exts]);
  let entries;
  try { entries = await readdir(dir, { withFileTypes: true }); } catch { return; }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory() && !SKIP_DIRS.has(entry.name)) {
      yield* walkFiles(full, extSet);
    } else if (entry.isFile() && extSet.has(entry.name.slice(entry.name.lastIndexOf('.')))) {
      if (!entry.name.includes('.g.') && !entry.name.includes('.Designer.')) yield full;
    }
  }
}

function repoRelative(absPath) {
  return relative(REPO_ROOT, absPath).replaceAll('\\', '/');
}

// ─────────────────────────────────────────────────────────────────────────────
// Stable identity keys  (no body included)
// ─────────────────────────────────────────────────────────────────────────────
function classKey(projectName, namespace, className) {
  return `${projectName}::${namespace ? namespace + '.' : ''}${className}`;
}

function methodKey(cKey, methodName, paramTypes = []) {
  return `${cKey}::${methodName}(${paramTypes.join(',')})`;
}

// ─────────────────────────────────────────────────────────────────────────────
// .csproj parser
// ─────────────────────────────────────────────────────────────────────────────
async function parseCsproj(filePath) {
  const text = await readFile(filePath, 'utf8');
  const name = basename(filePath, '.csproj');

  const projectRefs = [...text.matchAll(/<ProjectReference[^>]+Include="([^"]+)"/g)]
    .map(m => basename(m[1].replace(/\\/g, '/'), '.csproj'));

  const packageRefs = [...text.matchAll(/<PackageReference\s+Include="([^"]+)"(?:[^/]*?Version="([^"]*)")?/g)]
    .map(m => ({ name: m[1], version: m[2] ?? '' }));

  const targetFramework = text.match(/<TargetFramework[^>]*>([^<]+)<\/TargetFramework>/)?.[1] ?? '';

  let type = 'Library';
  if (text.includes('Microsoft.NET.Sdk.Web')) type = 'Web';
  if (text.includes('Aspire.AppHost.Sdk'))    type = 'AppHost';
  if (name.includes('Tests'))                 type = 'Test';

  return { name, type, targetFramework, projectRefs, packageRefs, filePath };
}

// ─────────────────────────────────────────────────────────────────────────────
// C# regex patterns  (declaration-level only — never captures method bodies)
// ─────────────────────────────────────────────────────────────────────────────

// Namespace: "namespace Foo.Bar" or "namespace Foo.Bar;"
const NS_RE = /^\s*namespace\s+([\w.]+)\s*[;{]?/m;

// Class / record / interface / struct declaration line
const DECL_RE =
  /^(?<indent>[ \t]*)(?<mods>(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly)\s+)*)(?<kind>class|record|interface|struct)\s+(?<name>\w+)(?:\s*<(?<generics>[^>]+)>)?(?:\s*:\s*(?<bases>[^{\n]+))?/gm;

// Method signature line (stops before opening brace or body)
// Captures: modifiers, return type, name, generic, params list header
const METHOD_RE =
  /^(?<indent>[ \t]{4,})(?<mods>(?:(?:public|protected|private|internal|static|virtual|override|abstract|async|sealed|new|readonly|extern)\s+)+)(?<ret>[\w<>\[\]?,. ]+?)\s+(?<name>[A-Z]\w*)\s*(?:<(?<generics>[^>]+)>)?\s*\((?<params>[^)]*)\)\s*(?:where\s+[\w:, ]+\s*)?(?:[{;=]|=>)/gm;

// Property declaration
const PROP_RE =
  /^(?<indent>[ \t]{4,})(?<mods>(?:(?:public|protected|private|internal|static|virtual|override|abstract|new|readonly)\s+)*)(?<type>[\w<>\[\]?,. ]+?)\s+(?<name>[A-Z]\w+)\s*\{/gm;

// Field declaration
const FIELD_RE =
  /^(?<indent>[ \t]{4,})(?<mods>(?:(?:public|protected|private|internal|static|readonly|const|volatile)\s+)*)(?<type>[\w<>\[\]?,. ]+?)\s+_?(?<name>[a-z]\w*)\s*(?:=|;)/gm;

// Method call reference (method invocations inside method body — name-only, no body stored)
const CALL_RE = /\b(?<recv>[A-Z]\w+)\.(?<mname>[a-z]\w+)\s*(?:<[^>]+>)?\s*\(/g;

// Constructor: ctor-like method named same as class
function isConstructorDecl(methodName, className) {
  return methodName === className;
}

// ─────────────────────────────────────────────────────────────────────────────
// Parse a single .cs file — metadata extraction only, no body text stored
// ─────────────────────────────────────────────────────────────────────────────
async function parseCsFile(absPath, projectName) {
  let text;
  try { text = await readFile(absPath, 'utf8'); } catch { return null; }

  const relPath  = repoRelative(absPath);
  const fileName = basename(absPath);
  const ns       = text.match(NS_RE)?.[1] ?? '';

  // ── Strip string literals and comments to reduce false-positive regex hits ──
  const stripped = text
    .replace(/\/\/[^\n]*/g, ' ')               // single-line comments
    .replace(/\/\*[\s\S]*?\*\//g, ' ')         // block comments
    .replace(/"(?:[^"\\]|\\.)*"/g, '""')       // string literals
    .replace(/@"(?:[^"]|"")*"/g, '""');        // verbatim strings

  // ── Extract line-start offsets for line-number reporting ──
  const lineStarts = [0];
  for (let i = 0; i < text.length; i++) {
    if (text[i] === '\n') lineStarts.push(i + 1);
  }
  const lineOf = (offset) => lineStarts.filter(s => s <= offset).length;

  const classes   = [];
  const methods   = [];
  const fields    = [];
  const props     = [];

  // ── Classes ──────────────────────────────────────────────────────────────
  for (const m of stripped.matchAll(DECL_RE)) {
    const { kind, name, mods, generics, bases: rawBases } = m.groups;
    if (name.length < 2) continue;

    const modsLc = mods.toLowerCase();
    const cKey   = classKey(projectName, ns, name);

    const bases   = (rawBases ?? '').split(',')
      .map(b => b.trim().replace(/<.*/, '').trim())
      .filter(b => /^[A-Z]/.test(b) && b.length > 1);

    const inherits  = kind === 'interface' ? [] : bases.filter(b => !b.startsWith('I') || b.length < 3);
    const implementedInterfaces = bases.filter(b => b.startsWith('I') && b.length > 2 && b !== name);

    classes.push({
      classKey:    cKey,
      name,
      kind,
      namespace:   ns,
      project:     projectName,
      filePath:    relPath,
      fileName,
      genericSignature: generics ? `<${generics}>` : '',
      isSealed:    modsLc.includes('sealed'),
      isAbstract:  modsLc.includes('abstract'),
      isStatic:    modsLc.includes('static'),
      visibility:  modsLc.match(/public|internal|private|protected/)?.[0] ?? 'internal',
      lineStart:   lineOf(m.index),
      inherits,
      implements: implementedInterfaces,
      dependsOn:   [],   // filled below from props/fields/ctor
    });
  }

  if (classes.length === 0) return { relPath, fileName, projectName, ns, classes, methods, fields, props };

  // For now associate all methods/props/fields with the first class in the file
  // (handles simple single-class-per-file cases correctly, partial classes gracefully)
  const primaryClass = classes[0];

  // ── Properties ───────────────────────────────────────────────────────────
  for (const m of stripped.matchAll(PROP_RE)) {
    const { mods, type, name } = m.groups;
    const modsLc = mods.toLowerCase();
    props.push({
      name,
      typeName:    type.trim(),
      className:   primaryClass.name,
      classKey:    primaryClass.classKey,
      project:     projectName,
      filePath:    relPath,
      visibility:  modsLc.match(/public|internal|private|protected/)?.[0] ?? 'private',
      isStatic:    modsLc.includes('static'),
      isReadonly:  modsLc.includes('readonly'),
      lineStart:   lineOf(m.index),
    });
    // track type dependency
    const cleanType = type.trim().replace(/<.*/, '').split('.').pop().trim();
    if (/^[A-Z]/.test(cleanType)) primaryClass.dependsOn.push(cleanType);
  }

  // ── Fields ───────────────────────────────────────────────────────────────
  for (const m of stripped.matchAll(FIELD_RE)) {
    const { mods, type, name } = m.groups;
    const modsLc = mods.toLowerCase();
    fields.push({
      name,
      typeName:    type.trim(),
      className:   primaryClass.name,
      classKey:    primaryClass.classKey,
      project:     projectName,
      filePath:    relPath,
      visibility:  modsLc.match(/public|internal|private|protected/)?.[0] ?? 'private',
      isStatic:    modsLc.includes('static'),
      isReadonly:  modsLc.includes('readonly'),
      isConst:     modsLc.includes('const'),
      lineStart:   lineOf(m.index),
    });
    const cleanType = type.trim().replace(/<.*/, '').split('.').pop().trim();
    if (/^[A-Z]/.test(cleanType)) primaryClass.dependsOn.push(cleanType);
  }

  // ── Methods ───────────────────────────────────────────────────────────────
  for (const m of stripped.matchAll(METHOD_RE)) {
    const { mods, ret, name, generics, params: rawParams } = m.groups;
    const modsLc = mods.toLowerCase();
    const isCtor = isConstructorDecl(name, primaryClass.name);

    // Parse parameter list — names and types only, no defaults/bodies
    const parameters = rawParams.trim().split(',')
      .map((p, i) => {
        const parts = p.trim().split(/\s+/);
        if (parts.length < 2) return null;
        const typePart = parts.slice(0, -1).join(' ').replace(/^(?:ref|out|in|params)\s+/, '');
        const paramName = parts[parts.length - 1].replace(/^_/, '');
        return { position: i, name: paramName, typeName: typePart.trim() };
      })
      .filter(Boolean);

    // Constructor parameter types feed dependsOn
    if (isCtor) {
      for (const p of parameters) {
        const clean = p.typeName.replace(/<.*/, '').split('.').pop().trim();
        if (/^[A-Z]/.test(clean)) primaryClass.dependsOn.push(clean);
      }
    }

    // Extract call references from the raw (non-stripped) text — store only names
    const mthKey = methodKey(primaryClass.classKey, name, parameters.map(p => p.typeName));
    const callRefs = [];
    // Find the block for this method to limit scan scope
    const bodyStart = text.indexOf('{', m.index + m[0].length);
    let depth = 0, bodyEnd = bodyStart;
    if (bodyStart !== -1) {
      for (let i = bodyStart; i < text.length && bodyEnd === bodyStart; i++) {
        if (text[i] === '{') depth++;
        else if (text[i] === '}') { depth--; if (depth === 0) bodyEnd = i; }
      }
      const body = stripped.slice(bodyStart, bodyEnd);
      for (const c of body.matchAll(CALL_RE)) {
        callRefs.push({ receiverHint: c.groups.recv, methodName: c.groups.mname });
      }
    }

    methods.push({
      methodKey:       mthKey,
      name,
      className:       primaryClass.name,
      classKey:        primaryClass.classKey,
      project:         projectName,
      filePath:        relPath,
      visibility:      modsLc.match(/public|protected|private|internal/)?.[0] ?? 'private',
      returnType:      isCtor ? 'constructor' : ret.trim(),
      genericSignature: generics ? `<${generics}>` : '',
      isStatic:        modsLc.includes('static'),
      isAsync:         modsLc.includes('async'),
      isAbstract:      modsLc.includes('abstract'),
      isOverride:      modsLc.includes('override'),
      isConstructor:   isCtor,
      lineStart:       lineOf(m.index),
      parameters,
      callRefs,
    });
  }

  // Deduplicate class dependencies
  for (const cls of classes) {
    cls.dependsOn = [...new Set(cls.dependsOn)];
  }

  return { relPath, fileName, projectName, ns, classes, methods, fields, props };
}

// ─────────────────────────────────────────────────────────────────────────────
// Inference helpers
// ─────────────────────────────────────────────────────────────────────────────
const LAYER_MAP = [
  ['BDDTests', 'Tests'], ['Tests', 'Tests'], ['AppHost', 'AppHost'],
  ['AppServices', 'AppServices'], ['Domains', 'Domains'],
  ['Infra', 'Infra'], ['Share', 'Share'], ['Api', 'Api'],
];
function inferLayer(name) {
  return LAYER_MAP.find(([s]) => name.endsWith(s))?.[1] ?? 'Unknown';
}

const PATTERN_RULES = [
  [/EventHandler$/, 'EventHandler'], [/Handler$/, 'Handler'], [/Validator$/, 'Validator'],
  [/Request$/, 'Command'], [/Dto$/, 'DTO'], [/Event$/, 'DomainEvent'],
  [/Endpoint$/, 'Endpoint'], [/Configs?$/, 'EfConfig'], [/StaticData$/, 'DataSeed'],
  [/^Spec\w+/, 'Specification'], [/AggregateRoot$/, 'DomainBase'],
  [/DbContext$/, 'DbContext'], [/Setup$/, 'Setup'],
  [/Repository$|Repo$/, 'Repository'], [/Service$/, 'Service'],
];
function inferPattern(name) {
  return PATTERN_RULES.find(([re]) => re.test(name))?.[1] ?? '';
}

// ─────────────────────────────────────────────────────────────────────────────
// Neo4j upsert helpers
// ─────────────────────────────────────────────────────────────────────────────
async function upsertProjects(projects) {
  for (const p of projects) {
    await cypher([
      {
        statement: `
          MERGE (proj:Project {name: $name})
          SET proj.type = $type, proj.layer = $layer,
              proj.targetFramework = $tf, proj.pattern = $pattern
        `,
        parameters: { name: p.name, type: p.type, layer: inferLayer(p.name), tf: p.targetFramework, pattern: inferPattern(p.name) },
      },
      ...p.projectRefs.map(ref => ({
        statement: `MERGE (a:Project {name: $a}) MERGE (b:Project {name: $b}) MERGE (a)-[:DEPENDS_ON]->(b)`,
        parameters: { a: p.name, b: ref },
      })),
      ...p.packageRefs.map(pkg => ({
        statement: `
          MERGE (pkg:NugetPackage {name: $pkg}) SET pkg.version = $ver
          WITH pkg MATCH (proj:Project {name: $proj})
          MERGE (proj)-[:USES_PACKAGE]->(pkg)
        `,
        parameters: { pkg: pkg.name, ver: pkg.version, proj: p.name },
      })),
    ]);
  }
}

async function upsertSourceFile(relPath, fileName, projectName) {
  await cypher({
    statement: `
      MERGE (f:SourceFile {path: $path})
      SET f.fileName = $fileName, f.project = $project
      WITH f
      MATCH (proj:Project {name: $project})
      MERGE (f)-[:IN_PROJECT]->(proj)
    `,
    parameters: { path: relPath, fileName, project: projectName },
  });
}

async function upsertNamespace(ns, projectName) {
  if (!ns) return;
  const nsKey = `${projectName}::${ns}`;
  await cypher([
    {
      statement: `
        MERGE (n:Namespace {nsKey: $nsKey})
        SET n.name = $ns, n.project = $projectName
      `,
      parameters: { nsKey, ns, projectName },
    },
    {
      statement: `
        MATCH (n:Namespace {nsKey: $nsKey}), (p:Project {name: $projectName})
        MERGE (n)-[:IN_PROJECT]->(p)
      `,
      parameters: { nsKey, projectName },
    },
  ]);
}

async function upsertClasses(classes, allClassKeys) {
  for (const cls of classes) {
    // Node upsert — metadata only, no body
    await cypher({
      statement: `
        MERGE (c:Classes {classKey: $classKey})
        SET c.name = $name, c.kind = $kind, c.namespace = $ns,
            c.project = $project, c.filePath = $filePath, c.fileName = $fileName,
            c.genericSignature = $generic, c.isSealed = $isSealed,
            c.isAbstract = $isAbstract, c.isStatic = $isStatic,
            c.visibility = $visibility, c.lineStart = $lineStart,
            c.layer = $layer, c.pattern = $pattern
      `,
      parameters: {
        classKey: cls.classKey, name: cls.name, kind: cls.kind,
        ns: cls.namespace, project: cls.project, filePath: cls.filePath,
        fileName: cls.fileName, generic: cls.genericSignature,
        isSealed: cls.isSealed, isAbstract: cls.isAbstract, isStatic: cls.isStatic,
        visibility: cls.visibility, lineStart: cls.lineStart,
        layer: inferLayer(cls.project), pattern: inferPattern(cls.name),
      },
    });

    // Relationships
    const rels = [];

    // Class -> SourceFile
    rels.push({
      statement: `
        MATCH (c:Classes {classKey: $ck}), (f:SourceFile {path: $fp})
        MERGE (c)-[:DECLARED_IN]->(f)
      `,
      parameters: { ck: cls.classKey, fp: cls.filePath },
    });

    // Class -> Project
    rels.push({
      statement: `
        MATCH (c:Classes {classKey: $ck}), (p:Project {name: $proj})
        MERGE (c)-[:IN_PROJECT]->(p)
      `,
      parameters: { ck: cls.classKey, proj: cls.project },
    });

    // Class -> Namespace
    if (cls.namespace) {
      const nsKey = `${cls.project}::${cls.namespace}`;
      rels.push({
        statement: `
          MATCH (c:Classes {classKey: $ck}), (n:Namespace {nsKey: $nsKey})
          MERGE (c)-[:IN_NAMESPACE]->(n)
        `,
        parameters: { ck: cls.classKey, nsKey },
      });
    }

    // Inheritance / Implements
    for (const base of cls.inherits) {
      rels.push({
        statement: `
          MATCH (child:Classes {classKey: $ck})
          MATCH (parent:Classes {name: $base})
          MERGE (child)-[:INHERITS]->(parent)
        `,
        parameters: { ck: cls.classKey, base },
      });
    }
    for (const iface of cls.implements) {
      rels.push({
        statement: `
          MATCH (cls:Classes {classKey: $ck})
          MATCH (iface:Classes {name: $iface})
          MERGE (cls)-[:IMPLEMENTS]->(iface)
        `,
        parameters: { ck: cls.classKey, iface },
      });
    }

    // DEPENDS_ON (local) or DEPENDS_ON_TYPE (unresolved)
    for (const dep of cls.dependsOn) {
      if (allClassKeys.has(dep)) {
        rels.push({
          statement: `
            MATCH (cls:Classes {classKey: $ck})
            MATCH (dep:Classes {name: $dep})
            MERGE (cls)-[:DEPENDS_ON]->(dep)
          `,
          parameters: { ck: cls.classKey, dep },
        });
      } else {
        rels.push({
          statement: `
            MERGE (t:TypeReference {name: $dep})
            WITH t MATCH (cls:Classes {classKey: $ck})
            MERGE (cls)-[:DEPENDS_ON_TYPE]->(t)
          `,
          parameters: { ck: cls.classKey, dep },
        });
      }
    }

    await cypherBatch(rels);
  }
}

async function upsertMethods(methods, allClassKeys) {
  for (const mth of methods) {
    // Method node — metadata only
    await cypher({
      statement: `
        MERGE (m:Methods {methodKey: $methodKey})
        SET m.name = $name, m.classKey = $classKey, m.className = $className,
            m.project = $project, m.filePath = $filePath,
            m.visibility = $vis, m.returnType = $ret,
            m.genericSignature = $generic, m.isStatic = $isStatic,
            m.isAsync = $isAsync, m.isAbstract = $isAbstract,
            m.isOverride = $isOverride, m.isConstructor = $isCtor,
            m.lineStart = $lineStart, m.arity = $arity
      `,
      parameters: {
        methodKey: mth.methodKey, name: mth.name, classKey: mth.classKey,
        className: mth.className, project: mth.project, filePath: mth.filePath,
        vis: mth.visibility, ret: mth.returnType, generic: mth.genericSignature,
        isStatic: mth.isStatic, isAsync: mth.isAsync, isAbstract: mth.isAbstract,
        isOverride: mth.isOverride, isCtor: mth.isConstructor,
        lineStart: mth.lineStart, arity: mth.parameters.length,
      },
    });

    const rels = [];

    // Method -> Class
    rels.push({
      statement: `
        MATCH (m:Methods {methodKey: $mk}), (c:Classes {classKey: $ck})
        MERGE (m)-[:BELONGS_TO]->(c)
      `,
      parameters: { mk: mth.methodKey, ck: mth.classKey },
    });

    // Method -> SourceFile
    rels.push({
      statement: `
        MATCH (m:Methods {methodKey: $mk}), (f:SourceFile {path: $fp})
        MERGE (m)-[:DECLARED_IN]->(f)
      `,
      parameters: { mk: mth.methodKey, fp: mth.filePath },
    });

    // Parameters -> HAS_PARAMETER -> MethodParameter -> TYPE_REFERENCE
    for (const param of mth.parameters) {
      const paramKey = `${mth.methodKey}::p${param.position}`;
      rels.push({
        statement: `
          MERGE (p:MethodParameter {paramKey: $pk})
          SET p.name = $name, p.position = $pos, p.typeName = $type,
              p.methodKey = $mk
          WITH p MATCH (m:Methods {methodKey: $mk})
          MERGE (m)-[:HAS_PARAMETER]->(p)
        `,
        parameters: { pk: paramKey, name: param.name, pos: param.position, type: param.typeName, mk: mth.methodKey },
      });

      // Parameter type -> local class or unresolved TypeReference
      const cleanType = param.typeName.replace(/<.*/, '').split('.').pop().trim();
      if (/^[A-Z]/.test(cleanType)) {
        if (allClassKeys.has(cleanType)) {
          rels.push({
            statement: `
              MATCH (p:MethodParameter {paramKey: $pk}), (t:Classes {name: $tn})
              MERGE (p)-[:TYPE_REFERENCE]->(t)
            `,
            parameters: { pk: paramKey, tn: cleanType },
          });
        } else {
          rels.push({
            statement: `
              MERGE (t:TypeReference {name: $tn})
              WITH t MATCH (p:MethodParameter {paramKey: $pk})
              MERGE (p)-[:TYPE_REFERENCE]->(t)
            `,
            parameters: { pk: paramKey, tn: cleanType },
          });
        }
      }
    }

    // Outbound call references (CALLS or CALLS_REFERENCE)
    const seenCalls = new Set();
    for (const call of mth.callRefs) {
      const callSig = `${call.receiverHint}.${call.methodName}`;
      if (seenCalls.has(callSig)) continue;
      seenCalls.add(callSig);

      rels.push({
        statement: `
          MERGE (ref:MethodReference {name: $mname, receiverHint: $recv})
          WITH ref MATCH (m:Methods {methodKey: $mk})
          MERGE (m)-[:CALLS_REFERENCE]->(ref)
        `,
        parameters: { mname: call.methodName, recv: call.receiverHint, mk: mth.methodKey },
      });
    }

    await cypherBatch(rels);
  }
}

async function upsertFieldsAndProps(fields, props) {
  const stmts = [];

  for (const f of fields) {
    stmts.push({
      statement: `
        MERGE (fd:Field {fieldKey: $fk})
        SET fd.name = $name, fd.typeName = $type, fd.classKey = $ck,
            fd.project = $proj, fd.filePath = $fp,
            fd.visibility = $vis, fd.isStatic = $isStatic,
            fd.isReadonly = $isReadonly, fd.isConst = $isConst, fd.lineStart = $ls
        WITH fd MATCH (c:Classes {classKey: $ck})
        MERGE (c)-[:HAS_FIELD]->(fd)
      `,
      parameters: {
        fk: `${f.classKey}::field::${f.name}`, name: f.name, type: f.typeName,
        ck: f.classKey, proj: f.project, fp: f.filePath,
        vis: f.visibility, isStatic: f.isStatic, isReadonly: f.isReadonly,
        isConst: f.isConst, ls: f.lineStart,
      },
    });
  }

  for (const p of props) {
    stmts.push({
      statement: `
        MERGE (pr:Property {propKey: $pk})
        SET pr.name = $name, pr.typeName = $type, pr.classKey = $ck,
            pr.project = $proj, pr.filePath = $fp,
            pr.visibility = $vis, pr.isStatic = $isStatic,
            pr.isReadonly = $isReadonly, pr.lineStart = $ls
        WITH pr MATCH (c:Classes {classKey: $ck})
        MERGE (c)-[:HAS_PROPERTY]->(pr)
      `,
      parameters: {
        pk: `${p.classKey}::prop::${p.name}`, name: p.name, type: p.typeName,
        ck: p.classKey, proj: p.project, fp: p.filePath,
        vis: p.visibility, isStatic: p.isStatic, isReadonly: p.isReadonly,
        ls: p.lineStart,
      },
    });
  }

  await cypherBatch(stmts);
}

// ─────────────────────────────────────────────────────────────────────────────
// IndexRun tracking
// ─────────────────────────────────────────────────────────────────────────────
async function recordIndexRun(runId, startedAt, stats) {
  const status = stats.failedFiles > 0 && stats.indexedClasses === 0 ? 'failed'
               : stats.failedFiles > 0 ? 'partial' : 'success';
  await cypher({
    statement: `
      MERGE (r:IndexRun {runId: $runId})
      SET r.startedAtUtc = $started, r.completedAtUtc = $completed,
          r.scannedFileCount = $scanned, r.indexedClassCount = $classes,
          r.indexedMethodCount = $methods, r.failedFileCount = $failed,
          r.status = $status
    `,
    parameters: {
      runId, started: startedAt, completed: new Date().toISOString(),
      scanned: stats.scannedFiles, classes: stats.indexedClasses,
      methods: stats.indexedMethods, failed: stats.failedFiles, status,
    },
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Main
// ─────────────────────────────────────────────────────────────────────────────
async function main() {
  const runId     = randomUUID();
  const startedAt = new Date().toISOString();

  console.log(`DKNet C# Metadata Graph Analyzer`);
  console.log(`Run ID : ${runId}`);
  console.log(`Neo4j  : ${NEO4J_URL}${DRY_RUN ? ' (DRY RUN — no writes)' : ''}`);
  console.log(`Src    : ${SRC_ROOT}\n`);

  if (!DRY_RUN) {
    console.log('Connecting to Neo4j...');
    await cypher('RETURN 1 AS ok');
    console.log('Connected.\n');
    console.log('Ensuring schema constraints and indexes...');
    await ensureSchema();
  }

  // ── 1. Discover and parse .csproj files ────────────────────────────────────
  console.log('Discovering projects...');
  const projects = [];
  for await (const csproj of walkFiles(SRC_ROOT, '.csproj')) {
    try { projects.push(await parseCsproj(csproj)); } catch { /* skip */ }
  }
  console.log(`  Found ${projects.length} project(s)`);

  if (!DRY_RUN) await upsertProjects(projects);

  // Map project name -> directory for file association
  const projDirMap = new Map(projects.map(p => [p.name, dirname(p.filePath)]));

  // ── 2. Discover and parse .cs files ────────────────────────────────────────
  console.log('\nParsing C# source files...');
  const allParsed = [];
  const stats = { scannedFiles: 0, indexedClasses: 0, indexedMethods: 0, failedFiles: 0 };
  const fileResults = [];

  for await (const csFile of walkFiles(SRC_ROOT, '.cs')) {
    stats.scannedFiles++;
    // Determine owning project
    const projectName = projects.find(p => csFile.startsWith(dirname(p.filePath)))?.name ?? 'Unknown';
    try {
      const result = await parseCsFile(csFile, projectName);
      if (result) {
        fileResults.push(result);
        stats.indexedClasses  += result.classes.length;
        stats.indexedMethods  += result.methods.length;
        allParsed.push(result);
      }
    } catch {
      stats.failedFiles++;
    }
  }

  // Scan .md files (file metadata only — no content stored)
  for await (const mdFile of walkFiles(SRC_ROOT, '.md')) {
    stats.scannedFiles++;
    const relPath  = repoRelative(mdFile);
    const fileName = basename(mdFile);
    const projName = projects.find(p => mdFile.startsWith(dirname(p.filePath)))?.name ?? '';
    if (!DRY_RUN) await upsertSourceFile(relPath, fileName, projName);
  }

  // Build global class name set for dependency resolution
  const allClassKeys = new Set(allParsed.flatMap(r => r.classes.map(c => c.name)));

  console.log(`  Scanned : ${stats.scannedFiles} files`);
  console.log(`  Classes : ${stats.indexedClasses}`);
  console.log(`  Methods : ${stats.indexedMethods}`);
  console.log(`  Failed  : ${stats.failedFiles}`);

  if (DRY_RUN) {
    console.log('\n[DRY RUN] No data was written to Neo4j.');
    return;
  }

  // ── 3. Upsert to Neo4j ─────────────────────────────────────────────────────
  console.log('\nUpserting source files, namespaces, classes...');
  for (const r of allParsed) {
    await upsertSourceFile(r.relPath, r.fileName, r.projectName);
    await upsertNamespace(r.ns, r.projectName);
    await upsertClasses(r.classes, allClassKeys);
  }

  console.log('Upserting methods, parameters, call references...');
  for (const r of allParsed) {
    await upsertMethods(r.methods, allClassKeys);
  }

  console.log('Upserting fields and properties...');
  for (const r of allParsed) {
    await upsertFieldsAndProps(r.fields, r.props);
  }

  // ── 4. Record run metadata ─────────────────────────────────────────────────
  console.log('Recording index run...');
  await recordIndexRun(runId, startedAt, stats);

  // ── 5. Summary ────────────────────────────────────────────────────────────
  const counts = await cypher(
    'MATCH (n) RETURN labels(n)[0] AS label, count(n) AS cnt ORDER BY cnt DESC'
  );
  console.log('\nGraph node counts:');
  for (const row of counts[0]?.data ?? []) {
    const [label, cnt] = row.row;
    if (label) console.log(`  ${String(label).padEnd(18)} ${cnt}`);
  }

  console.log(`\nStatus : ${stats.failedFiles > 0 ? 'partial' : 'success'}`);
  console.log(`Run ID : ${runId}`);
  console.log(`\n  Neo4j Browser → ${NEO4J_URL}`);
}

main().catch(err => { console.error(err.message); process.exit(1); });
