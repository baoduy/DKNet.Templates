#!/usr/bin/env dotnet run
#:package Microsoft.CodeAnalysis.CSharp.Workspaces@5.3.0
#:package Microsoft.CodeAnalysis.Workspaces.MSBuild@5.3.0
#:package Microsoft.Build.Locator@1.11.2
#:package NFalkorDB@1.0.6
#:property DisableMSBuildAssemblyCopyCheck=true
#:property JsonSerializerIsReflectionEnabledByDefault=true

// DKNet.Templates — C# Roslyn-Based Metadata Graph Analyzer
//
// Scans all C# projects via Roslyn semantic analysis, extracts structural
// metadata (no source body text), and pushes the index into FalkorDB via Cypher.
//
// Run:
//   dotnet run graph/analyze.cs
//   dotnet run graph/analyze.cs -- --host=localhost --port=6379 --password=codegraph123
//   dotnet run graph/analyze.cs -- --dry-run

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NFalkorDB;
using StackExchange.Redis;
using Path = System.IO.Path;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

// ─────────────────────────────────────────────────────────────────────────────
// Main entry point (top-level statements)
// ─────────────────────────────────────────────────────────────────────────────
var config = CliConfig.Parse(args);
var ctx = new AnalyzerContext(null, config.GraphName, config.DryRun, config.RepoRoot, config.SrcRoot);

var runId = Guid.NewGuid().ToString();
var startedAt = DateTime.UtcNow.ToString("o");

Console.WriteLine("DKNet C# Roslyn Metadata Graph Analyzer (FalkorDB)");
Console.WriteLine($"Run ID : {runId}");
Console.WriteLine($"FalkorDB: {config.FalkorHost}:{config.FalkorPort}/{config.GraphName}{(config.DryRun ? " (DRY RUN — no writes)" : "")}");
Console.WriteLine($"Src    : {config.SrcRoot}");
Console.WriteLine();

if (!config.DryRun)
{
    Console.WriteLine("Connecting to FalkorDB...");
    var connStr = string.IsNullOrEmpty(config.FalkorPass)
        ? $"{config.FalkorHost}:{config.FalkorPort}"
        : $"{config.FalkorHost}:{config.FalkorPort},password={config.FalkorPass}";
    var db = new FalkorDB(connStr);
    ctx = ctx with { Graph = db.SelectGraph(config.GraphName) };
    Console.WriteLine("Connected.");
    Console.WriteLine();
    Console.WriteLine("Ensuring schema indexes...");
    CypherClient.EnsureSchema(ctx);
}

// ── 1. Discover projects from .slnx ─────────────────────────────────────────
var slnxPath = Path.Combine(ctx.SrcRoot, "Monxa.PaymentGateway.slnx");
Console.WriteLine("Discovering projects from .slnx...");
var csprojPaths = ProjectParser.ParseSlnx(slnxPath);
Console.WriteLine($"  Found {csprojPaths.Count} project(s)");

var projectInfos = csprojPaths.Select(ProjectParser.ParseCsproj).ToList();
GraphUpserter.UpsertProjects(ctx, projectInfos);

// ── 2. Load projects via MSBuild Workspace + Roslyn ──────────────────────────
Console.WriteLine();
Console.WriteLine("Registering MSBuild...");
MSBuildLocator.RegisterDefaults();

Console.WriteLine("Loading projects via Roslyn...");
var allResults = new List<FileAnalysisResult>();
int scannedFiles = 0, indexedClasses = 0, indexedMethods = 0, failedProjects = 0;

using (var workspace = MSBuildWorkspace.Create())
{
    workspace.RegisterWorkspaceFailedHandler(e =>
    {
        if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            Console.Error.WriteLine($"  [WARN] {e.Diagnostic.Message}");
    });

    foreach (var csprojPath in csprojPaths)
    {
        var projectName = Path.GetFileNameWithoutExtension(csprojPath);
        Console.Write($"  Loading {projectName}...");

        try
        {
            var existing = workspace.CurrentSolution.Projects
                .FirstOrDefault(p => p.Name == projectName);
            var project = existing ?? await workspace.OpenProjectAsync(csprojPath);
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                Console.WriteLine(" [SKIP: no compilation]");
                failedProjects++;
                continue;
            }

            int projClasses = 0, projMethods = 0;

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var filePath = syntaxTree.FilePath;
                if (string.IsNullOrEmpty(filePath)) continue;

                var relPath = StringHelpers.RepoRelative(filePath, ctx.RepoRoot);
                if (relPath.Contains("/bin/") || relPath.Contains("/obj/") ||
                    relPath.Contains("/Migrations/") ||
                    Path.GetFileName(filePath).Contains(".g.") ||
                    Path.GetFileName(filePath).Contains(".Designer."))
                    continue;

                if (!filePath.StartsWith(ctx.SrcRoot)) continue;

                scannedFiles++;
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var walker = new AnalyzerWalker(semanticModel, projectName, filePath, ctx.RepoRoot);

                try
                {
                    walker.Visit(await syntaxTree.GetRootAsync());
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"    [WARN] Failed to analyze {relPath}: {ex.Message}");
                    continue;
                }

                if (walker.Classes.Count == 0 && walker.Methods.Count == 0) continue;

                foreach (var cls in walker.Classes)
                    cls.DependsOn = cls.DependsOn.Distinct().ToList();

                var ns = walker.Classes.FirstOrDefault()?.Namespace;

                allResults.Add(new FileAnalysisResult
                {
                    RelPath = relPath,
                    FileName = Path.GetFileName(filePath),
                    ProjectName = projectName,
                    Namespace = ns,
                    Classes = walker.Classes,
                    Methods = walker.Methods,
                    Fields = walker.Fields,
                    Props = walker.Props,
                });

                projClasses += walker.Classes.Count;
                projMethods += walker.Methods.Count;
            }

            indexedClasses += projClasses;
            indexedMethods += projMethods;
            Console.WriteLine($" {projClasses} classes, {projMethods} methods");
        }
        catch (Exception ex)
        {
            Console.WriteLine($" [FAIL: {ex.Message}]");
            failedProjects++;
        }
    }
}

var allClassNames = new HashSet<string>(allResults.SelectMany(r => r.Classes.Select(c => c.Name)));
var specClassNames = new HashSet<string>(allResults.SelectMany(r => r.Classes)
    .Where(c => c.SpecTargetType != null || InferenceEngine.InferPattern(c.Name) == "Specification")
    .Select(c => c.Name));

Console.WriteLine();
Console.WriteLine($"  Scanned : {scannedFiles} files");
Console.WriteLine($"  Classes : {indexedClasses}");
Console.WriteLine($"  Methods : {indexedMethods}");
Console.WriteLine($"  Failed  : {failedProjects} project(s)");

if (config.DryRun)
{
    Console.WriteLine();
    Console.WriteLine("[DRY RUN] No data was written to FalkorDB.");
    return;
}

// ── 3. Upsert to FalkorDB ─────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("Upserting source files, namespaces, classes...");
foreach (var r in allResults)
{
    GraphUpserter.UpsertSourceFile(ctx, r.RelPath, r.FileName, r.ProjectName);
    GraphUpserter.UpsertNamespace(ctx, r.Namespace, r.ProjectName);
    GraphUpserter.UpsertClasses(ctx, r.Classes, allClassNames);
}

Console.WriteLine("Upserting methods, parameters, call references...");
foreach (var r in allResults)
    GraphUpserter.UpsertMethods(ctx, r.Methods, allClassNames, specClassNames);

Console.WriteLine("Upserting fields and properties...");
foreach (var r in allResults)
    GraphUpserter.UpsertFieldsAndProps(ctx, r.Fields, r.Props);

// ── 4. Record run metadata ──────────────────────────────────────────────────
Console.WriteLine("Recording index run...");
GraphUpserter.RecordIndexRun(ctx, runId, startedAt, scannedFiles, indexedClasses, indexedMethods, failedProjects);

// ── 5. Summary ──────────────────────────────────────────────────────────────
var counts = ctx.Graph!.ReadOnlyQuery("MATCH (n) RETURN labels(n)[0] AS label, count(n) AS cnt ORDER BY cnt DESC");
Console.WriteLine();
Console.WriteLine("Graph node counts:");
foreach (var record in counts)
{
    var label = record.Values[0]?.ToString() ?? "Unknown";
    var count = record.Values[1]?.ToString() ?? "0";
    Console.WriteLine($"  {label?.PadRight(22)} {count}");
}

Console.WriteLine();
Console.WriteLine($"Status : {(failedProjects > 0 ? "partial" : "success")}");
Console.WriteLine($"Run ID : {runId}");
Console.WriteLine($"Engine : Roslyn (semantic analysis) + FalkorDB");
Console.WriteLine($"\n  FalkorDB Browser -> http://localhost:3000");

// ═══════════════════════════════════════════════════════════════════════════════
// Type declarations (must appear after all top-level statements)
// ═══════════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────────
#region Context
// ─────────────────────────────────────────────────────────────────────────────

record AnalyzerContext(Graph? Graph, string GraphName, bool DryRun, string RepoRoot, string SrcRoot);

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region CLI Configuration
// ─────────────────────────────────────────────────────────────────────────────

static class CliConfig
{
    public record ParseResult(
        string FalkorHost, string FalkorPort, string FalkorPass,
        string GraphName, bool DryRun, string RepoRoot, string SrcRoot);

    public static ParseResult Parse(string[] args)
    {
        var cliArgs = args
            .Where(a => a.StartsWith("--"))
            .Select(a => a[2..].Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "true");

        string GetArg(string key, string envVar, string fallback) =>
            cliArgs.GetValueOrDefault(key) ?? Environment.GetEnvironmentVariable(envVar) ?? fallback;

        var falkorHost = GetArg("host", "FALKORDB_HOST", "localhost");
        var falkorPort = GetArg("port", "FALKORDB_PORT", "6379");
        var falkorPass = GetArg("password", "FALKORDB_PASSWORD", "codegraph123");
        var graphName  = GetArg("graph", "FALKORDB_GRAPH", "codegraph");
        var dryRun     = cliArgs.ContainsKey("dry-run");

        var explicitSrc = cliArgs.GetValueOrDefault("src");
        string repoRoot;
        if (explicitSrc != null)
        {
            var srcFull = Path.GetFullPath(explicitSrc);
            repoRoot = Path.GetDirectoryName(srcFull)!;
            if (!Directory.Exists(srcFull))
                repoRoot = srcFull;
        }
        else
        {
            var scriptDir = Path.GetDirectoryName(Path.GetFullPath(
                Environment.GetCommandLineArgs().FirstOrDefault(a => a.EndsWith("analyze.cs")) ?? "graph/analyze.cs"))!;
            repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
        }

        var srcRoot = Directory.Exists(Path.Combine(repoRoot, "src"))
            ? Path.Combine(repoRoot, "src")
            : repoRoot;

        return new ParseResult(falkorHost, falkorPort, falkorPass, graphName, dryRun, repoRoot, srcRoot);
    }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region Cypher Client
// ─────────────────────────────────────────────────────────────────────────────

static class CypherClient
{
    public static string Esc(string? val) => val?.Replace("\\", "\\\\").Replace("'", "\\'") ?? "";
    public static string Bool(bool val) => val ? "true" : "false";

    public static void Cypher(AnalyzerContext ctx, string statement)
    {
        if (ctx.DryRun || ctx.Graph == null) return;
        ctx.Graph.Query(statement);
    }

    public static void CypherBatch(AnalyzerContext ctx, List<string> statements)
    {
        if (ctx.DryRun || ctx.Graph == null) return;
        foreach (var s in statements)
            ctx.Graph.Query(s);
    }

    public static void EnsureSchema(AnalyzerContext ctx)
    {
        string[] indexes =
        [
            "CREATE INDEX FOR (n:SourceFile) ON (n.path)",
            "CREATE INDEX FOR (n:Classes) ON (n.classKey)",
            "CREATE INDEX FOR (n:Classes) ON (n.name)",
            "CREATE INDEX FOR (n:Methods) ON (n.methodKey)",
            "CREATE INDEX FOR (n:Methods) ON (n.name)",
            "CREATE INDEX FOR (n:Project) ON (n.name)",
            "CREATE INDEX FOR (n:Namespace) ON (n.nsKey)",
            "CREATE INDEX FOR (n:NugetPackage) ON (n.name)",
            "CREATE INDEX FOR (n:Endpoint) ON (n.endpointKey)",
            "CREATE INDEX FOR (n:Endpoint) ON (n.route)",
            "CREATE INDEX FOR (n:ArchitectureConcept) ON (n.conceptKey)",
            "CREATE INDEX FOR (n:ArchitectureConcept) ON (n.name)",
            "CREATE INDEX FOR (n:Layer) ON (n.name)",
            "CREATE INDEX FOR (n:IndexRun) ON (n.runId)",
            "CREATE INDEX FOR (n:TypeReference) ON (n.name)",
            "CREATE INDEX FOR (n:MethodReference) ON (n.name)",
            "CREATE INDEX FOR (n:MethodParameter) ON (n.paramKey)",
            "CREATE INDEX FOR (n:Field) ON (n.fieldKey)",
            "CREATE INDEX FOR (n:Property) ON (n.propKey)",
        ];
        foreach (var idx in indexes)
            try { Cypher(ctx, idx); } catch { /* index may already exist */ }
    }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region Project Parsing
// ─────────────────────────────────────────────────────────────────────────────

static class ProjectParser
{
    public static List<string> ParseSlnx(string slnxPath)
    {
        var doc = XDocument.Load(slnxPath);
        var slnxDir = Path.GetDirectoryName(slnxPath)!;
        return doc.Descendants("Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(p => p != null && p.EndsWith(".csproj"))
            .Select(p => Path.GetFullPath(Path.Combine(slnxDir, p!.Replace('\\', '/'))))
            .Where(File.Exists)
            .ToList();
    }

    public static ProjectInfo ParseCsproj(string csprojPath)
    {
        var text = File.ReadAllText(csprojPath);
        var name = Path.GetFileNameWithoutExtension(csprojPath);

        var projectRefs = Regex.Matches(text, @"<ProjectReference[^>]+Include=""([^""]+)""")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')))
            .ToList();

        var packageRefs = Regex.Matches(text, @"<PackageReference\s+Include=""([^""]+)""(?:[^/]*?Version=""([^""]*)""|[^/]*)")
            .Select(m => (Name: m.Groups[1].Value, Version: m.Groups.Count > 2 ? m.Groups[2].Value : ""))
            .ToList();

        var tf = Regex.Match(text, @"<TargetFramework[^>]*>([^<]+)</TargetFramework>").Groups[1].Value;

        var type = "Library";
        if (text.Contains("Microsoft.NET.Sdk.Web")) type = "Web";
        if (text.Contains("Aspire.AppHost.Sdk")) type = "AppHost";
        if (name.Contains("Tests")) type = "Test";

        return new ProjectInfo(name, type, tf, projectRefs, packageRefs, csprojPath);
    }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region Inference Engine
// ─────────────────────────────────────────────────────────────────────────────

static class InferenceEngine
{
    private static readonly (string Suffix, string Layer)[] LayerMap =
    [
        ("BDDTests", "Tests"), ("Tests", "Tests"), ("AppHost", "AppHost"),
        ("AppServices", "AppServices"), ("Domains", "Domains"),
        ("Infra", "Infra"), ("Share", "Share"), ("Api", "Api"),
    ];

    private static readonly (Regex Pattern, string Role)[] PatternRules =
    [
        (new(@"EventHandler$"), "EventHandler"), (new(@"Handler$"), "Handler"),
        (new(@"Validator$"), "Validator"), (new(@"Request$"), "Command"),
        (new(@"Dto$"), "DTO"), (new(@"Event$"), "DomainEvent"),
        (new(@"Endpoint$"), "Endpoint"), (new(@"Configs?$"), "EfConfig"),
        (new(@"StaticData$"), "DataSeed"), (new(@"^Spec\w+"), "Specification"),
        (new(@"AggregateRoot$"), "DomainBase"), (new(@"DbContext$"), "DbContext"),
        (new(@"Setup$"), "Setup"), (new(@"Repository$|Repo$"), "Repository"),
        (new(@"Service$"), "Service"),
    ];

    public static string InferLayer(string projectName) =>
        LayerMap.FirstOrDefault(x => projectName.EndsWith(x.Suffix)).Layer ?? "Libraries";

    public static string InferPattern(string className) =>
        PatternRules.FirstOrDefault(x => x.Pattern.IsMatch(className)).Role ?? "";

    public static string InferBoundedContext(string? namespaceName, string projectName)
    {
        var parts = (namespaceName ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries);
        var featIdx = Array.IndexOf(parts, "Features");
        if (featIdx >= 0 && featIdx + 1 < parts.Length) return parts[featIdx + 1];
        var apiIdx = Array.IndexOf(parts, "ApiEndpoints");
        if (apiIdx >= 0 && apiIdx + 1 < parts.Length) return parts[apiIdx + 1];
        var projParts = projectName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return projParts.Length > 0 ? projParts[^1] : "Shared";
    }

    public static List<ArchConcept> InferArchitectureConcepts(ClassInfo cls)
    {
        var concepts = new List<ArchConcept>();
        var name = cls.Name;
        var pattern = InferPattern(name);
        var layer = InferLayer(cls.Project);

        void Add(string n, string family) => concepts.Add(new(family + "::" + n, n, family));

        if (Regex.IsMatch(name, @"Request$|Command$")) Add("Command", "CQRS");
        if (name.EndsWith("Query")) Add("Query", "CQRS");
        if (name.EndsWith("Handler") || cls.IsHandler) Add("Handler", "CQRS");
        if (name.EndsWith("Validator")) Add("Validator", "CQRS");
        if (name.EndsWith("Dto")) Add("ReadModel", "CQRS");

        if (cls.InheritsNames.Any(x => x.EndsWith("AggregateRoot")) || name.EndsWith("Aggregate")) Add("Aggregate", "DDD");
        if (pattern == "DomainEvent" || name.EndsWith("Event")) Add("DomainEvent", "DDD");
        if (pattern == "Specification" || cls.SpecTargetType != null) Add("Specification", "DDD");
        if (pattern == "Repository") Add("Repository", "DDD");
        if (pattern == "Service" && layer == "AppServices") Add("ApplicationService", "DDD");
        if (pattern == "Service" && layer == "Domains") Add("DomainService", "DDD");
        if (pattern == "Endpoint") Add("Endpoint", "API");
        if (pattern == "DTO") Add("Dto", "API");

        Add(layer, "Layer");

        return concepts.DistinctBy(c => c.ConceptKey).ToList();
    }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region String Helpers
// ─────────────────────────────────────────────────────────────────────────────

static class StringHelpers
{
    public static string ShortTypeNameFromString(string typeName)
    {
        var cleaned = Regex.Replace(typeName, @"<.*>", "");
        var parts = cleaned.Split('.');
        return parts[^1].Trim().TrimEnd('?');
    }

    public static string RepoRelative(string absPath, string repoRoot) =>
        Path.GetRelativePath(repoRoot, absPath).Replace('\\', '/');

    public static List<string> ExtractGenericArgs(string typeExpression)
    {
        var start = typeExpression.IndexOf('<');
        var end = typeExpression.LastIndexOf('>');
        if (start < 0 || end <= start) return [];

        var inner = typeExpression[(start + 1)..end];
        var items = new List<string>();
        var current = new StringBuilder();
        int depth = 0;
        foreach (var ch in inner)
        {
            if (ch == '<') depth++;
            if (ch == '>') depth--;
            if (ch == ',' && depth == 0)
            {
                items.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0) items.Add(current.ToString().Trim());
        return items;
    }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region Graph Upserters
// ─────────────────────────────────────────────────────────────────────────────

static class GraphUpserter
{
    public static void UpsertProjects(AnalyzerContext ctx, List<ProjectInfo> projects)
    {
        foreach (var p in projects)
        {
            var layer = InferenceEngine.InferLayer(p.Name);
            var bc = InferenceEngine.InferBoundedContext("", p.Name);
            var stmts = new List<string>
            {
                $"MERGE (proj:Project {{name: '{E(p.Name)}'}}) SET proj.type = '{E(p.Type)}', proj.layer = '{E(layer)}', proj.targetFramework = '{E(p.TargetFramework)}', proj.pattern = '{E(InferenceEngine.InferPattern(p.Name))}', proj.boundedContext = '{E(bc)}'",
                $"MERGE (layer:Layer {{name: '{E(layer)}'}}) WITH layer MATCH (proj:Project {{name: '{E(p.Name)}'}}) MERGE (proj)-[:BELONGS_TO_LAYER]->(layer)",
            };
            foreach (var r in p.ProjectRefs)
                stmts.Add($"MERGE (a:Project {{name: '{E(p.Name)}'}}) MERGE (b:Project {{name: '{E(r)}'}}) MERGE (a)-[:DEPENDS_ON]->(b)");
            foreach (var pkg in p.PackageRefs)
                stmts.Add($"MERGE (pkg:NugetPackage {{name: '{E(pkg.Name)}'}}) SET pkg.version = '{E(pkg.Version)}' WITH pkg MATCH (proj:Project {{name: '{E(p.Name)}'}}) MERGE (proj)-[:USES_PACKAGE]->(pkg)");
            CypherClient.CypherBatch(ctx, stmts);
        }
    }

    public static void UpsertSourceFile(AnalyzerContext ctx, string relPath, string fileName, string projectName)
    {
        CypherClient.Cypher(ctx, $"MERGE (f:SourceFile {{path: '{E(relPath)}'}}) SET f.fileName = '{E(fileName)}', f.project = '{E(projectName)}' WITH f MATCH (proj:Project {{name: '{E(projectName)}'}}) MERGE (f)-[:IN_PROJECT]->(proj)");
    }

    public static void UpsertNamespace(AnalyzerContext ctx, string? ns, string projectName)
    {
        if (string.IsNullOrEmpty(ns)) return;
        var nsKey = $"{projectName}::{ns}";
        CypherClient.Cypher(ctx, $"MERGE (n:Namespace {{nsKey: '{E(nsKey)}'}}) SET n.name = '{E(ns)}', n.project = '{E(projectName)}'");
        CypherClient.Cypher(ctx, $"MATCH (n:Namespace {{nsKey: '{E(nsKey)}'}}), (p:Project {{name: '{E(projectName)}'}}) MERGE (n)-[:IN_PROJECT]->(p)");
    }

    public static void UpsertClasses(AnalyzerContext ctx, List<ClassInfo> classes, HashSet<string> allClassNames)
    {
        foreach (var cls in classes)
        {
            var classLayer = InferenceEngine.InferLayer(cls.Project);
            var bc = InferenceEngine.InferBoundedContext(cls.Namespace, cls.Project);
            var concepts = InferenceEngine.InferArchitectureConcepts(cls);

            CypherClient.Cypher(ctx, $"MERGE (c:Classes {{classKey: '{E(cls.ClassKey)}'}}) SET c.name = '{E(cls.Name)}', c.kind = '{E(cls.Kind)}', c.namespace = '{E(cls.Namespace)}', c.project = '{E(cls.Project)}', c.filePath = '{E(cls.FilePath)}', c.fileName = '{E(cls.FileName)}', c.genericSignature = '{E(cls.GenericSignature)}', c.isSealed = {B(cls.IsSealed)}, c.isAbstract = {B(cls.IsAbstract)}, c.isStatic = {B(cls.IsStatic)}, c.visibility = '{E(cls.Visibility)}', c.lineStart = {cls.LineStart}, c.layer = '{E(classLayer)}', c.pattern = '{E(InferenceEngine.InferPattern(cls.Name))}', c.boundedContext = '{E(bc)}'");

            var rels = new List<string>
            {
                $"MATCH (c:Classes {{classKey: '{E(cls.ClassKey)}'}}), (f:SourceFile {{path: '{E(cls.FilePath)}'}}) MERGE (c)-[:DECLARED_IN]->(f)",
                $"MATCH (c:Classes {{classKey: '{E(cls.ClassKey)}'}}), (p:Project {{name: '{E(cls.Project)}'}}) MERGE (c)-[:IN_PROJECT]->(p)",
                $"MERGE (layer:Layer {{name: '{E(classLayer)}'}}) WITH layer MATCH (c:Classes {{classKey: '{E(cls.ClassKey)}'}}) MERGE (c)-[:IN_LAYER]->(layer)",
            };

            foreach (var concept in concepts)
                rels.Add($"MERGE (k:ArchitectureConcept {{conceptKey: '{E(concept.ConceptKey)}'}}) SET k.name = '{E(concept.Name)}', k.family = '{E(concept.Family)}' WITH k MATCH (c:Classes {{classKey: '{E(cls.ClassKey)}'}}) MERGE (c)-[:HAS_CONCEPT]->(k)");

            if (!string.IsNullOrEmpty(cls.Namespace))
            {
                var nsKey = $"{cls.Project}::{cls.Namespace}";
                rels.Add($"MATCH (c:Classes {{classKey: '{E(cls.ClassKey)}'}}), (n:Namespace {{nsKey: '{E(nsKey)}'}}) MERGE (c)-[:IN_NAMESPACE]->(n)");
            }

            foreach (var b in cls.InheritsNames)
                rels.Add($"MATCH (child:Classes {{classKey: '{E(cls.ClassKey)}'}}), (parent:Classes {{name: '{E(b)}'}}) MERGE (child)-[:INHERITS]->(parent)");

            foreach (var iface in cls.ImplementsNames)
                rels.Add($"MATCH (cls:Classes {{classKey: '{E(cls.ClassKey)}'}}), (iface:Classes {{name: '{E(iface)}'}}) MERGE (cls)-[:IMPLEMENTS]->(iface)");

            foreach (var contract in cls.Contracts)
            {
                if (Regex.IsMatch(contract, @"I(?:Page)?Handler<"))
                {
                    var contractArgs = StringHelpers.ExtractGenericArgs(contract);
                    if (contractArgs.Count >= 2)
                    {
                        var handledType = StringHelpers.ShortTypeNameFromString(contractArgs[0]);
                        var returnedType = StringHelpers.ShortTypeNameFromString(contractArgs[1]);
                        rels.Add($"MERGE (t:TypeReference {{name: '{E(handledType)}'}}) WITH t MATCH (cls:Classes {{classKey: '{E(cls.ClassKey)}'}}) MERGE (cls)-[:HANDLES]->(t)");
                        rels.Add($"MERGE (t:TypeReference {{name: '{E(returnedType)}'}}) WITH t MATCH (cls:Classes {{classKey: '{E(cls.ClassKey)}'}}) MERGE (cls)-[:RETURNS]->(t)");
                    }
                    else if (contractArgs.Count == 1)
                    {
                        var eventType = StringHelpers.ShortTypeNameFromString(contractArgs[0]);
                        rels.Add($"MERGE (t:TypeReference {{name: '{E(eventType)}'}}) WITH t MATCH (cls:Classes {{classKey: '{E(cls.ClassKey)}'}}) MERGE (cls)-[:SUBSCRIBES_TO_EVENT]->(t)");
                    }
                }
            }

            if (cls.SpecTargetType != null)
                rels.Add($"MERGE (t:TypeReference {{name: '{E(cls.SpecTargetType)}'}}) WITH t MATCH (cls:Classes {{classKey: '{E(cls.ClassKey)}'}}) MERGE (cls)-[:SPECIALIZES_SPEC_FOR]->(t)");

            foreach (var dep in cls.DependsOn.Distinct())
            {
                if (allClassNames.Contains(dep))
                    rels.Add($"MATCH (cls:Classes {{classKey: '{E(cls.ClassKey)}'}}), (dep:Classes {{name: '{E(dep)}'}}) MERGE (cls)-[:DEPENDS_ON]->(dep)");
                else
                    rels.Add($"MERGE (t:TypeReference {{name: '{E(dep)}'}}) WITH t MATCH (cls:Classes {{classKey: '{E(cls.ClassKey)}'}}) MERGE (cls)-[:DEPENDS_ON_TYPE]->(t)");
            }

            CypherClient.CypherBatch(ctx, rels);
        }
    }

    public static void UpsertMethods(AnalyzerContext ctx, List<MethodInfo> methods, HashSet<string> allClassNames, HashSet<string> specClassNames)
    {
        foreach (var mth in methods)
        {
            var methodLayer = InferenceEngine.InferLayer(mth.Project);
            var bc = InferenceEngine.InferBoundedContext("", mth.Project);

            CypherClient.Cypher(ctx, $"MERGE (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) SET m.name = '{E(mth.Name)}', m.classKey = '{E(mth.ClassKey)}', m.className = '{E(mth.ClassName)}', m.project = '{E(mth.Project)}', m.filePath = '{E(mth.FilePath)}', m.visibility = '{E(mth.Visibility)}', m.returnType = '{E(mth.ReturnType)}', m.genericSignature = '{E(mth.GenericSignature)}', m.isStatic = {B(mth.IsStatic)}, m.isAsync = {B(mth.IsAsync)}, m.isAbstract = {B(mth.IsAbstract)}, m.isOverride = {B(mth.IsOverride)}, m.isConstructor = {B(mth.IsConstructor)}, m.lineStart = {mth.LineStart}, m.arity = {mth.Parameters.Count}, m.layer = '{E(methodLayer)}', m.boundedContext = '{E(bc)}'");

            var rels = new List<string>
            {
                $"MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}), (c:Classes {{classKey: '{E(mth.ClassKey)}'}}) MERGE (m)-[:BELONGS_TO]->(c)",
                $"MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}), (f:SourceFile {{path: '{E(mth.FilePath)}'}}) MERGE (m)-[:DECLARED_IN]->(f)",
                $"MERGE (layer:Layer {{name: '{E(methodLayer)}'}}) WITH layer MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:IN_LAYER]->(layer)",
            };

            if (mth.Dispatches.Count > 0)
                rels.Add($"MERGE (k:ArchitectureConcept {{conceptKey: 'CQRS::Dispatcher'}}) SET k.name = 'Dispatcher', k.family = 'CQRS' WITH k MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:HAS_CONCEPT]->(k)");

            if (mth.EndpointMappings.Count > 0)
                rels.Add($"MERGE (k:ArchitectureConcept {{conceptKey: 'API::EndpointMapper'}}) SET k.name = 'EndpointMapper', k.family = 'API' WITH k MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:HAS_CONCEPT]->(k)");

            foreach (var param in mth.Parameters)
            {
                var paramKey = $"{mth.MethodKey}::p{param.Position}";
                rels.Add($"MERGE (p:MethodParameter {{paramKey: '{E(paramKey)}'}}) SET p.name = '{E(param.Name)}', p.position = {param.Position}, p.typeName = '{E(param.TypeName)}', p.methodKey = '{E(mth.MethodKey)}' WITH p MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:HAS_PARAMETER]->(p)");

                var cleanType = StringHelpers.ShortTypeNameFromString(param.TypeName);
                if (!string.IsNullOrEmpty(cleanType) && char.IsUpper(cleanType[0]))
                {
                    if (allClassNames.Contains(cleanType))
                        rels.Add($"MATCH (p:MethodParameter {{paramKey: '{E(paramKey)}'}}), (t:Classes {{name: '{E(cleanType)}'}}) MERGE (p)-[:TYPE_REFERENCE]->(t)");
                    else
                        rels.Add($"MERGE (t:TypeReference {{name: '{E(cleanType)}'}}) WITH t MATCH (p:MethodParameter {{paramKey: '{E(paramKey)}'}}) MERGE (p)-[:TYPE_REFERENCE]->(t)");
                }
            }

            var seenCalls = new HashSet<string>();
            foreach (var call in mth.CallRefs)
            {
                var sig = $"{call.ReceiverHint}.{call.MethodName}:{call.ReceiverType ?? ""}";
                if (!seenCalls.Add(sig)) continue;

                rels.Add($"MERGE (ref:MethodReference {{name: '{E(call.MethodName)}', receiverHint: '{E(call.ReceiverHint)}'}}) WITH ref MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:CALLS_REFERENCE]->(ref)");

                if (call.ReceiverType != null)
                    rels.Add($"MERGE (t:TypeReference {{name: '{E(call.ReceiverType)}'}}) WITH t MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:CALLS_ON_TYPE]->(t)");
            }

            foreach (var ep in mth.EndpointMappings)
            {
                var epKey = $"{mth.MethodKey}::{ep.Verb}:{ep.Route}:{ep.RequestType}";
                rels.Add($"MERGE (e:Endpoint {{endpointKey: '{E(epKey)}'}}) SET e.httpVerb = '{E(ep.Verb)}', e.route = '{E(ep.Route)}', e.requestType = '{E(ep.RequestType)}', e.responseType = '{E(ep.ResponseType)}', e.filePath = '{E(mth.FilePath)}', e.className = '{E(mth.ClassName)}', e.methodName = '{E(mth.Name)}' WITH e MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:MAPS_ENDPOINT]->(e)");

                if (!string.IsNullOrEmpty(ep.RequestType))
                    rels.Add($"MERGE (t:TypeReference {{name: '{E(ep.RequestType)}'}}) WITH t MATCH (e:Endpoint {{endpointKey: '{E(epKey)}'}}) MERGE (e)-[:ACCEPTS]->(t)");

                if (!string.IsNullOrEmpty(ep.ResponseType))
                    rels.Add($"MERGE (t:TypeReference {{name: '{E(ep.ResponseType)}'}}) WITH t MATCH (e:Endpoint {{endpointKey: '{E(epKey)}'}}) MERGE (e)-[:RETURNS]->(t)");
            }

            foreach (var dispatch in mth.Dispatches)
                rels.Add($"MERGE (t:TypeReference {{name: '{E(dispatch.RequestType)}'}}) WITH t MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:DISPATCHES]->(t)");

            foreach (var eventType in mth.EmittedEvents.Distinct())
                rels.Add($"MERGE (t:TypeReference {{name: '{E(eventType)}'}}) WITH t MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:EMITS_EVENT]->(t)");

            foreach (var ctorType in mth.ConstructedTypes.Distinct())
            {
                rels.Add($"MERGE (t:TypeReference {{name: '{E(ctorType)}'}}) WITH t MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:CONSTRUCTS_TYPE]->(t)");

                if (specClassNames.Contains(ctorType))
                    rels.Add($"MERGE (t:TypeReference {{name: '{E(ctorType)}'}}) WITH t MATCH (m:Methods {{methodKey: '{E(mth.MethodKey)}'}}) MERGE (m)-[:USES_SPEC]->(t)");
            }

            CypherClient.CypherBatch(ctx, rels);
        }
    }

    public static void UpsertFieldsAndProps(AnalyzerContext ctx, List<FieldInfo> fields, List<PropInfo> props)
    {
        var stmts = new List<string>();

        foreach (var f in fields)
        {
            var fk = $"{f.ClassKey}::field::{f.Name}";
            stmts.Add($"MERGE (fd:Field {{fieldKey: '{E(fk)}'}}) SET fd.name = '{E(f.Name)}', fd.typeName = '{E(f.TypeName)}', fd.classKey = '{E(f.ClassKey)}', fd.project = '{E(f.Project)}', fd.filePath = '{E(f.FilePath)}', fd.visibility = '{E(f.Visibility)}', fd.isStatic = {B(f.IsStatic)}, fd.isReadonly = {B(f.IsReadonly)}, fd.isConst = {B(f.IsConst)}, fd.lineStart = {f.LineStart} WITH fd MATCH (c:Classes {{classKey: '{E(f.ClassKey)}'}}) MERGE (c)-[:HAS_FIELD]->(fd)");
        }

        foreach (var p in props)
        {
            var pk = $"{p.ClassKey}::prop::{p.Name}";
            stmts.Add($"MERGE (pr:Property {{propKey: '{E(pk)}'}}) SET pr.name = '{E(p.Name)}', pr.typeName = '{E(p.TypeName)}', pr.classKey = '{E(p.ClassKey)}', pr.project = '{E(p.Project)}', pr.filePath = '{E(p.FilePath)}', pr.visibility = '{E(p.Visibility)}', pr.isStatic = {B(p.IsStatic)}, pr.isReadonly = {B(p.IsReadonly)}, pr.lineStart = {p.LineStart} WITH pr MATCH (c:Classes {{classKey: '{E(p.ClassKey)}'}}) MERGE (c)-[:HAS_PROPERTY]->(pr)");
        }

        CypherClient.CypherBatch(ctx, stmts);
    }

    public static void RecordIndexRun(AnalyzerContext ctx, string runId, string startedAt, int scannedFiles, int indexedClasses, int indexedMethods, int failedFiles)
    {
        var status = failedFiles > 0 && indexedClasses == 0 ? "failed" : failedFiles > 0 ? "partial" : "success";
        CypherClient.Cypher(ctx, $"MERGE (r:IndexRun {{runId: '{E(runId)}'}}) SET r.startedAtUtc = '{E(startedAt)}', r.completedAtUtc = '{E(DateTime.UtcNow.ToString("o"))}', r.scannedFileCount = {scannedFiles}, r.indexedClassCount = {indexedClasses}, r.indexedMethodCount = {indexedMethods}, r.failedFileCount = {failedFiles}, r.status = '{E(status)}', r.engine = 'roslyn'");
    }

    // Short aliases for readability inside Cypher string interpolations
    private static string E(string? val) => CypherClient.Esc(val);
    private static string B(bool val) => CypherClient.Bool(val);
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region Data Classes
// ─────────────────────────────────────────────────────────────────────────────

record ArchConcept(string ConceptKey, string Name, string Family);
record ProjectInfo(string Name, string Type, string TargetFramework, List<string> ProjectRefs, List<(string Name, string Version)> PackageRefs, string FilePath);

class ClassInfo
{
    public string ClassKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Namespace { get; set; }
    public string Project { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string GenericSignature { get; set; } = "";
    public bool IsSealed { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
    public string Visibility { get; set; } = "internal";
    public int LineStart { get; set; }
    public List<string> InheritsNames { get; set; } = [];
    public List<string> ImplementsNames { get; set; } = [];
    public List<string> Contracts { get; set; } = [];
    public string? SpecTargetType { get; set; }
    public List<string> DependsOn { get; set; } = [];
    public bool IsHandler { get; set; }
}

class MethodInfo
{
    public string MethodKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ClassKey { get; set; } = "";
    public string Project { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Visibility { get; set; } = "private";
    public string ReturnType { get; set; } = "";
    public string GenericSignature { get; set; } = "";
    public bool IsStatic { get; set; }
    public bool IsAsync { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsOverride { get; set; }
    public bool IsConstructor { get; set; }
    public int LineStart { get; set; }
    public List<ParamInfo> Parameters { get; set; } = [];
    public List<CallRef> CallRefs { get; set; } = [];
    public List<EndpointMapping> EndpointMappings { get; set; } = [];
    public List<DispatchInfo> Dispatches { get; set; } = [];
    public List<string> EmittedEvents { get; set; } = [];
    public List<string> ConstructedTypes { get; set; } = [];
}

record ParamInfo(int Position, string Name, string TypeName);
record CallRef(string ReceiverHint, string MethodName, string? ReceiverType);
record EndpointMapping(string Verb, string RequestType, string ResponseType, string Route);
record DispatchInfo(string RequestType, string Source);

class PropInfo
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ClassKey { get; set; } = "";
    public string Project { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Visibility { get; set; } = "private";
    public bool IsStatic { get; set; }
    public bool IsReadonly { get; set; }
    public int LineStart { get; set; }
}

class FieldInfo
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ClassKey { get; set; } = "";
    public string Project { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Visibility { get; set; } = "private";
    public bool IsStatic { get; set; }
    public bool IsReadonly { get; set; }
    public bool IsConst { get; set; }
    public int LineStart { get; set; }
}

class FileAnalysisResult
{
    public string RelPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string? Namespace { get; set; }
    public List<ClassInfo> Classes { get; set; } = [];
    public List<MethodInfo> Methods { get; set; } = [];
    public List<FieldInfo> Fields { get; set; } = [];
    public List<PropInfo> Props { get; set; } = [];
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region Roslyn Analyzer Walker
// ─────────────────────────────────────────────────────────────────────────────

class AnalyzerWalker(SemanticModel semanticModel, string projectName, string filePath, string repoRoot) : CSharpSyntaxWalker
{
    public List<ClassInfo> Classes { get; } = [];
    public List<MethodInfo> Methods { get; } = [];
    public List<FieldInfo> Fields { get; } = [];
    public List<PropInfo> Props { get; } = [];

    private readonly Stack<ClassInfo> _classStack = new();
    private MethodInfo? _currentMethod;

    private string RelPath => Path.GetRelativePath(repoRoot, filePath).Replace('\\', '/');
    private string FileName => Path.GetFileName(filePath);
    private int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string AccessibilityToString(Accessibility a) => a switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Protected => "protected",
        Accessibility.Private => "private",
        Accessibility.ProtectedOrInternal => "internal",
        _ => "internal"
    };

    private string MakeClassKey(string project, string? ns, string name) =>
        $"{project}::{(string.IsNullOrEmpty(ns) ? "" : ns + ".")}{name}";

    private string MakeMethodKey(string classKey, string methodName, string[] paramTypes) =>
        $"{classKey}::{methodName}({string.Join(",", paramTypes)})";

    private static string ShortTypeName(ITypeSymbol? type)
    {
        if (type == null) return "";
        return type.Name;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node) => VisitTypeDeclaration(node, "class");
    public override void VisitRecordDeclaration(RecordDeclarationSyntax node) => VisitTypeDeclaration(node, node.ClassOrStructKeyword.Text == "struct" ? "record struct" : "record");
    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => VisitTypeDeclaration(node, "interface");
    public override void VisitStructDeclaration(StructDeclarationSyntax node) => VisitTypeDeclaration(node, "struct");

    private void VisitTypeDeclaration(TypeDeclarationSyntax node, string kind)
    {
        var symbol = semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) { base.DefaultVisit(node); return; }

        var ns = symbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : symbol.ContainingNamespace?.ToDisplayString();

        var classKey = MakeClassKey(projectName, ns, symbol.Name);

        var inheritsNames = new List<string>();
        var implementsNames = new List<string>();
        var contracts = new List<string>();

        if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            inheritsNames.Add(symbol.BaseType.Name);
            contracts.Add(symbol.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        foreach (var iface in symbol.Interfaces)
        {
            implementsNames.Add(iface.Name);
            contracts.Add(iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        var isHandler = symbol.AllInterfaces.Any(i =>
            i.Name is "IHandler" or "IPageHandler" && i.TypeArguments.Length > 0);

        string? specTargetType = null;
        var specBase = symbol.BaseType;
        while (specBase != null)
        {
            if (specBase.Name == "Specification" && specBase.TypeArguments.Length > 0)
            {
                specTargetType = ShortTypeName(specBase.TypeArguments[0]);
                break;
            }
            specBase = specBase.BaseType;
        }

        var generic = symbol.TypeParameters.Length > 0
            ? "<" + string.Join(", ", symbol.TypeParameters.Select(t => t.Name)) + ">"
            : "";

        var cls = new ClassInfo
        {
            ClassKey = classKey,
            Name = symbol.Name,
            Kind = kind,
            Namespace = ns,
            Project = projectName,
            FilePath = RelPath,
            FileName = FileName,
            GenericSignature = generic,
            IsSealed = symbol.IsSealed,
            IsAbstract = symbol.IsAbstract,
            IsStatic = symbol.IsStatic,
            Visibility = AccessibilityToString(symbol.DeclaredAccessibility),
            LineStart = LineOf(node),
            InheritsNames = inheritsNames,
            ImplementsNames = implementsNames,
            Contracts = contracts,
            SpecTargetType = specTargetType,
            IsHandler = isHandler,
        };
        Classes.Add(cls);

        _classStack.Push(cls);
        base.DefaultVisit(node);
        _classStack.Pop();
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (_classStack.Count == 0) return;
        var cls = _classStack.Peek();
        var symbol = semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var parameters = symbol.Parameters.Select((p, i) =>
            new ParamInfo(i, p.Name, ShortTypeName(p.Type))).ToList();

        foreach (var p in symbol.Parameters)
        {
            var typeName = ShortTypeName(p.Type);
            if (!string.IsNullOrEmpty(typeName) && char.IsUpper(typeName[0]))
                cls.DependsOn.Add(typeName);
        }

        var paramTypes = symbol.Parameters.Select(p => ShortTypeName(p.Type)).ToArray();
        var mthKey = MakeMethodKey(cls.ClassKey, symbol.Name, paramTypes);

        var mth = new MethodInfo
        {
            MethodKey = mthKey,
            Name = symbol.Name,
            ClassName = cls.Name,
            ClassKey = cls.ClassKey,
            Project = projectName,
            FilePath = RelPath,
            Visibility = AccessibilityToString(symbol.DeclaredAccessibility),
            ReturnType = "constructor",
            IsConstructor = true,
            LineStart = LineOf(node),
            Parameters = parameters,
        };

        _currentMethod = mth;
        if (node.Body != null) AnalyzeMethodBody(node.Body, mth);
        if (node.ExpressionBody != null) AnalyzeExpression(node.ExpressionBody.Expression, mth);
        _currentMethod = null;

        Methods.Add(mth);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (_classStack.Count == 0) return;
        var cls = _classStack.Peek();
        var symbol = semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var parameters = symbol.Parameters.Select((p, i) =>
            new ParamInfo(i, p.Name, ShortTypeName(p.Type))).ToList();

        var paramTypes = symbol.Parameters.Select(p => ShortTypeName(p.Type)).ToArray();
        var mthKey = MakeMethodKey(cls.ClassKey, symbol.Name, paramTypes);

        var generic = symbol.TypeParameters.Length > 0
            ? "<" + string.Join(", ", symbol.TypeParameters.Select(t => t.Name)) + ">"
            : "";

        var mth = new MethodInfo
        {
            MethodKey = mthKey,
            Name = symbol.Name,
            ClassName = cls.Name,
            ClassKey = cls.ClassKey,
            Project = projectName,
            FilePath = RelPath,
            Visibility = AccessibilityToString(symbol.DeclaredAccessibility),
            ReturnType = ShortTypeName(symbol.ReturnType),
            GenericSignature = generic,
            IsStatic = symbol.IsStatic,
            IsAsync = symbol.IsAsync,
            IsAbstract = symbol.IsAbstract,
            IsOverride = symbol.IsOverride,
            LineStart = LineOf(node),
            Parameters = parameters,
        };

        _currentMethod = mth;
        if (node.Body != null) AnalyzeMethodBody(node.Body, mth);
        if (node.ExpressionBody != null) AnalyzeExpression(node.ExpressionBody.Expression, mth);
        _currentMethod = null;

        Methods.Add(mth);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (_classStack.Count == 0) return;
        var cls = _classStack.Peek();
        var symbol = semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return;

        var typeName = ShortTypeName(symbol.Type);
        if (!string.IsNullOrEmpty(typeName) && char.IsUpper(typeName[0]))
            cls.DependsOn.Add(typeName);

        Props.Add(new PropInfo
        {
            Name = symbol.Name,
            TypeName = typeName,
            ClassName = cls.Name,
            ClassKey = cls.ClassKey,
            Project = projectName,
            FilePath = RelPath,
            Visibility = AccessibilityToString(symbol.DeclaredAccessibility),
            IsStatic = symbol.IsStatic,
            IsReadonly = symbol.SetMethod == null,
            LineStart = LineOf(node),
        });
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (_classStack.Count == 0) return;
        var cls = _classStack.Peek();

        foreach (var variable in node.Declaration.Variables)
        {
            var symbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
            if (symbol == null) continue;

            var typeName = ShortTypeName(symbol.Type);
            if (!string.IsNullOrEmpty(typeName) && char.IsUpper(typeName[0]))
                cls.DependsOn.Add(typeName);

            Fields.Add(new FieldInfo
            {
                Name = symbol.Name,
                TypeName = typeName,
                ClassName = cls.Name,
                ClassKey = cls.ClassKey,
                Project = projectName,
                FilePath = RelPath,
                Visibility = AccessibilityToString(symbol.DeclaredAccessibility),
                IsStatic = symbol.IsStatic,
                IsReadonly = symbol.IsReadOnly,
                IsConst = symbol.IsConst,
                LineStart = LineOf(node),
            });
        }
    }

    private void AnalyzeMethodBody(BlockSyntax body, MethodInfo mth)
    {
        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            AnalyzeInvocation(invocation, mth);

        foreach (var creation in body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            AnalyzeObjectCreation(creation, mth);

        foreach (var creation in body.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
        {
            var typeInfo = semanticModel.GetTypeInfo(creation);
            if (typeInfo.Type != null)
                mth.ConstructedTypes.Add(ShortTypeName(typeInfo.Type));
        }
    }

    private void AnalyzeExpression(ExpressionSyntax expr, MethodInfo mth)
    {
        foreach (var invocation in expr.DescendantNodes().OfType<InvocationExpressionSyntax>())
            AnalyzeInvocation(invocation, mth);

        foreach (var creation in expr.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            AnalyzeObjectCreation(creation, mth);
    }

    private void AnalyzeInvocation(InvocationExpressionSyntax invocation, MethodInfo mth)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (methodSymbol == null) return;

        var receiverType = methodSymbol.ContainingType != null ? ShortTypeName(methodSymbol.ContainingType) : null;
        var receiverHint = "";

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            receiverHint = memberAccess.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => memberAccess.Expression.ToString()
            };
        }

        if (!string.IsNullOrEmpty(receiverHint))
            mth.CallRefs.Add(new CallRef(receiverHint, methodSymbol.Name, receiverType));

        if (Regex.IsMatch(methodSymbol.Name, @"^Map(Get|Post|Put|Delete|Patch)$"))
        {
            var verb = methodSymbol.Name[3..];
            var typeArgs = methodSymbol.TypeArguments;
            var requestType = typeArgs.Length > 0 ? ShortTypeName(typeArgs[0]) : "";
            var responseType = typeArgs.Length > 1 ? ShortTypeName(typeArgs[1]) : "";

            var route = "/";
            if (invocation.ArgumentList.Arguments.Count > 0)
            {
                var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                if (firstArg is LiteralExpressionSyntax literal && literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                    route = literal.Token.ValueText;
            }

            mth.EndpointMappings.Add(new EndpointMapping(verb, requestType, responseType, route));
        }

        if (Regex.IsMatch(methodSymbol.Name, @"^Map(GetById|GetPage|GetList)$"))
        {
            var typeArgs = methodSymbol.TypeArguments;
            var requestType = typeArgs.Length > 0 ? ShortTypeName(typeArgs[0]) : "";
            var responseType = typeArgs.Length > 1 ? ShortTypeName(typeArgs[1]) : "";
            mth.EndpointMappings.Add(new EndpointMapping("Get", requestType, responseType, "/"));
        }

        if (methodSymbol.Name == "Send" && (receiverType == "IMessageBus" || receiverType == "MessageBus"))
        {
            if (invocation.ArgumentList.Arguments.Count > 0)
            {
                var argType = semanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type;
                if (argType != null)
                    mth.Dispatches.Add(new DispatchInfo(ShortTypeName(argType), "semantic"));
            }
        }

        if (methodSymbol.Name == "AddEvent")
        {
            if (methodSymbol.TypeArguments.Length > 0)
                mth.EmittedEvents.Add(ShortTypeName(methodSymbol.TypeArguments[0]));
            else if (invocation.ArgumentList.Arguments.Count > 0)
            {
                var argType = semanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type;
                if (argType != null)
                    mth.EmittedEvents.Add(ShortTypeName(argType));
            }
        }
    }

    private void AnalyzeObjectCreation(ObjectCreationExpressionSyntax creation, MethodInfo mth)
    {
        var typeInfo = semanticModel.GetTypeInfo(creation);
        if (typeInfo.Type != null)
            mth.ConstructedTypes.Add(ShortTypeName(typeInfo.Type));
    }
}

#endregion
