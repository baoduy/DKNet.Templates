#!/usr/bin/env dotnet run
#:package Qdrant.Client@1.12.0
#:package AllMiniLmL6V2Sharp@0.0.3

// DKNet.Templates — Markdown Vector Indexer
//
// Discovers all .md files in the repository, chunks them by heading,
// embeds via all-MiniLM-L6-v2 (ONNX Runtime, local — no external service),
// and upserts into Qdrant.
//
// Incremental: tracks file_hash per chunk. On re-run, unchanged files
// are skipped and stale points (deleted files/headings) are removed.
//
// Performance: pipelines embedding (CPU) with Qdrant upsert (network I/O)
// so the next batch embeds while the previous batch uploads.
//
// First run downloads model.onnx + vocab.txt from Hugging Face (~90MB).
//
// Run:
//   dotnet run graph/vector.cs
//   dotnet run graph/vector.cs -- --host=localhost --port=6333
//   dotnet run graph/vector.cs -- --dry-run
//   dotnet run graph/vector.cs -- --purge

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AllMiniLmL6V2Sharp;
using AllMiniLmL6V2Sharp.Tokenizer;
using Qdrant.Client;
using Qdrant.Client.Grpc;

// ─────────────────────────────────────────────────────────────────────────────
// Main entry point
// ─────────────────────────────────────────────────────────────────────────────
var config = VectorCliConfig.Parse(args);

var isIncremental = config.ChangedFiles != null;
Console.WriteLine("DKNet Markdown Vector Indexer (Qdrant + ONNX all-MiniLM-L6-v2)");
Console.WriteLine($"Qdrant : {config.QdrantHost}:{config.QdrantPort} (collection: {config.Collection}){(config.DryRun ? " (DRY RUN)" : "")}");
Console.WriteLine($"Mode   : {(isIncremental ? $"incremental ({config.ChangedFiles!.Count} file(s))" : "full scan")}");
Console.WriteLine($"Model  : {config.ModelDir}");
Console.WriteLine($"Source : {config.RepoRoot}");
Console.WriteLine();

// ── 1. Ensure ONNX model is available ───────────────────────────────────────
await OnnxModelManager.EnsureModelAsync(config.ModelDir);

// ── 2. Discover markdown files ──────────────────────────────────────────────
Console.WriteLine("Discovering .md files...");
List<string> mdFiles;
if (isIncremental)
{
    // Only process the specific changed files (filter to existing .md files)
    mdFiles = config.ChangedFiles!
        .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        .Select(f => Path.GetFullPath(Path.Combine(config.RepoRoot, f)))
        .Where(File.Exists)
        .ToList();
    Console.WriteLine($"  Incremental: {mdFiles.Count} changed .md file(s)");
}
else
{
    mdFiles = MarkdownDiscovery.FindAll(config.RepoRoot);
    Console.WriteLine($"  Found {mdFiles.Count} markdown file(s)");
}

// ── 3. Compute file hashes and chunk (parallel file I/O) ───────────────────
Console.WriteLine("Chunking by headings...");
var fileChunksBag = new ConcurrentDictionary<string, (string Hash, List<MarkdownChunk> Chunks)>();
Parallel.ForEach(mdFiles, filePath =>
{
    var relPath = Path.GetRelativePath(config.RepoRoot, filePath).Replace('\\', '/');
    var category = MarkdownDiscovery.CategorizeFile(relPath);
    var content = File.ReadAllText(filePath);
    var fileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    var chunks = MarkdownChunker.Chunk(content, relPath, category);
    if (chunks.Count > 0)
        fileChunksBag[relPath] = (fileHash, chunks);
});
var fileChunks = fileChunksBag.ToDictionary(kv => kv.Key, kv => kv.Value);
var allChunks = fileChunks.Values.SelectMany(fc => fc.Chunks).ToList();
Console.WriteLine($"  Generated {allChunks.Count} chunk(s) from {fileChunks.Count} file(s)");

if (config.DryRun)
{
    Console.WriteLine();
    Console.WriteLine("DRY RUN — would upsert the following chunks:");
    foreach (var c in allChunks.Take(20))
        Console.WriteLine($"  [{c.Category}] {c.FilePath} # {c.Heading} ({c.Content.Length} chars)");
    if (allChunks.Count > 20)
        Console.WriteLine($"  ... and {allChunks.Count - 20} more");
    return;
}

// ── 4. Initialize ONNX embedder ─────────────────────────────────────────────
Console.WriteLine("Loading ONNX embedding model...");
var modelPath = Path.Combine(config.ModelDir, "model.onnx");
var vocabPath = Path.Combine(config.ModelDir, "vocab.txt");
using var embedder = new AllMiniLmL6V2Embedder(modelPath: modelPath,
    tokenizer: new BertTokenizer(vocabPath), truncate: true);
Console.WriteLine("  Model loaded.");

// ── 5. Connect to Qdrant ────────────────────────────────────────────────────
Console.WriteLine("Connecting to Qdrant...");
var qdrantClient = new QdrantClient(config.QdrantHost, config.QdrantPort);

// ── 6. Purge if requested ───────────────────────────────────────────────────
if (config.Purge)
{
    Console.WriteLine($"Purging collection '{config.Collection}'...");
    try { await qdrantClient.DeleteCollectionAsync(config.Collection); }
    catch { /* collection may not exist */ }
    Console.WriteLine("  Purge complete.");
}

// ── 7. Ensure collection exists ─────────────────────────────────────────────
Console.WriteLine("Ensuring collection exists...");
const ulong vectorSize = 384; // all-MiniLM-L6-v2 output dimension
var collections = await qdrantClient.ListCollectionsAsync();
bool collectionIsNew = !collections.Any(c => c == config.Collection);
if (collectionIsNew)
{
    await qdrantClient.CreateCollectionAsync(config.Collection,
        new VectorParams { Size = vectorSize, Distance = Distance.Cosine });
    Console.WriteLine($"  Created collection '{config.Collection}' ({vectorSize}d, cosine)");

    // Create payload indexes for efficient filtering
    await Task.WhenAll(
        qdrantClient.CreatePayloadIndexAsync(config.Collection, "file_path", PayloadSchemaType.Keyword),
        qdrantClient.CreatePayloadIndexAsync(config.Collection, "file_hash", PayloadSchemaType.Keyword),
        qdrantClient.CreatePayloadIndexAsync(config.Collection, "category", PayloadSchemaType.Keyword)
    );
    Console.WriteLine("  Created payload indexes (file_path, file_hash, category).");
}
else
{
    Console.WriteLine($"  Collection '{config.Collection}' already exists.");
}

// ── 8. Incremental: query existing file hashes to skip unchanged ────────────
var existingFileHashes = new Dictionary<string, string>(); // file_path → file_hash
var existingPointIds = new Dictionary<string, List<string>>(); // file_path → [point UUIDs]

if (!collectionIsNew && !config.Purge)
{
    if (isIncremental)
    {
        // Fast-path: only query points for the specific changed/deleted files
        Console.WriteLine("Querying index for changed files only (incremental fast-path)...");
        var allChangedPaths = config.ChangedFiles!.Select(f => f.Replace('\\', '/')).ToList();
        foreach (var fp in allChangedPaths)
        {
            try
            {
                var scrollResult = await qdrantClient.ScrollAsync(config.Collection, limit: 500,
                    filter: new Filter
                    {
                        Must = { new Condition { Field = new FieldCondition
                        {
                            Key = "file_path",
                            Match = new Match { Keyword = fp }
                        }}}
                    },
                    payloadSelector: new WithPayloadSelector
                    {
                        Include = new PayloadIncludeSelector { Fields = { "file_path", "file_hash" } }
                    });
                foreach (var point in scrollResult.Result)
                {
                    var fh = point.Payload.GetValueOrDefault("file_hash")?.StringValue ?? "";
                    var pid = point.Id.Uuid;
                    existingFileHashes[fp] = fh;
                    if (!existingPointIds.ContainsKey(fp))
                        existingPointIds[fp] = [];
                    existingPointIds[fp].Add(pid);
                }
            }
            catch { /* file may not exist in index yet */ }
        }
        Console.WriteLine($"  Found {existingPointIds.Values.Sum(v => v.Count)} existing point(s) for {allChangedPaths.Count} file(s).");
    }
    else
    {
        Console.WriteLine("Checking existing index for incremental update...");

        // Full scroll to build complete hash map
        PointId? nextOffset = null;
        bool first = true;
        do
        {
            var scrollResult = first
                ? await qdrantClient.ScrollAsync(config.Collection, limit: 100,
                    payloadSelector: new WithPayloadSelector
                    {
                        Include = new PayloadIncludeSelector { Fields = { "file_path", "file_hash" } }
                    })
                : await qdrantClient.ScrollAsync(config.Collection, limit: 100,
                    offset: nextOffset,
                    payloadSelector: new WithPayloadSelector
                    {
                        Include = new PayloadIncludeSelector { Fields = { "file_path", "file_hash" } }
                    });
            first = false;

            foreach (var point in scrollResult.Result)
            {
                var fp = point.Payload.GetValueOrDefault("file_path")?.StringValue ?? "";
                var fh = point.Payload.GetValueOrDefault("file_hash")?.StringValue ?? "";
                var pid = point.Id.Uuid;

                if (!string.IsNullOrEmpty(fp))
                {
                    existingFileHashes[fp] = fh;
                    if (!existingPointIds.ContainsKey(fp))
                        existingPointIds[fp] = [];
                    existingPointIds[fp].Add(pid);
                }
            }

            nextOffset = scrollResult.NextPageOffset;
        } while (nextOffset != null);

        Console.WriteLine($"  Found {existingFileHashes.Count} indexed file(s), {existingPointIds.Values.Sum(v => v.Count)} point(s).");
    }
}

// ── 9. Determine which files need processing ────────────────────────────────
var filesToProcess = new List<string>();
var filesToSkip = 0;

foreach (var (relPath, (hash, chunks)) in fileChunks)
{
    if (existingFileHashes.TryGetValue(relPath, out var existingHash) && existingHash == hash)
        filesToSkip++;
    else
        filesToProcess.Add(relPath);
}

// Detect deleted files: in changed-files list but not on disk
var staleFiles = new List<string>();
if (isIncremental)
{
    var deletedFiles = config.ChangedFiles!
        .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        .Select(f => f.Replace('\\', '/'))
        .Where(f => !File.Exists(Path.GetFullPath(Path.Combine(config.RepoRoot, f))))
        .Where(f => existingPointIds.ContainsKey(f))
        .ToList();
    staleFiles.AddRange(deletedFiles);
}
else
{
    // Full mode: files in index but no longer on disk
    staleFiles.AddRange(existingPointIds.Keys.Where(fp => !fileChunks.ContainsKey(fp)));
}

Console.WriteLine($"  Files: {filesToProcess.Count} changed, {filesToSkip} unchanged, {staleFiles.Count} stale");

// ── 10. Delete stale + changed points (parallel) ───────────────────────────
var filesToDelete = staleFiles.Concat(filesToProcess)
    .Where(fp => existingPointIds.ContainsKey(fp) && existingPointIds[fp].Count > 0)
    .ToList();

if (filesToDelete.Count > 0)
{
    var staleCount = staleFiles.Sum(f => existingPointIds.GetValueOrDefault(f)?.Count ?? 0);
    var changedCount = filesToProcess.Sum(f => existingPointIds.GetValueOrDefault(f)?.Count ?? 0);
    Console.WriteLine($"Deleting old points: {staleCount} stale + {changedCount} changed...");

    // Batch deletes concurrently (max 8 parallel)
    var deleteSemaphore = new SemaphoreSlim(8);
    var deleteTasks = filesToDelete.Select(async fp =>
    {
        await deleteSemaphore.WaitAsync();
        try
        {
            var ids = existingPointIds[fp].Select(id => Guid.Parse(id)).ToList();
            await qdrantClient.DeleteAsync(config.Collection, ids);
        }
        finally { deleteSemaphore.Release(); }
    });
    await Task.WhenAll(deleteTasks);
    Console.WriteLine("  Deletion complete.");
}

// ── 11. Embed and upsert changed files (pipelined) ─────────────────────────
var chunksToProcess = filesToProcess
    .Where(fp => fileChunks.ContainsKey(fp))
    .SelectMany(fp => fileChunks[fp].Chunks)
    .ToList();
int total = chunksToProcess.Count;

if (total == 0)
{
    Console.WriteLine("No changes detected — index is up to date.");
}
else
{
    const int batchSize = 32;
    int processed = 0;
    int failed = 0;
    Task? pendingUpsert = null; // pipeline: upsert runs while next batch embeds

    Console.WriteLine($"Embedding and upserting {total} chunks from {filesToProcess.Count} file(s) (batch size {batchSize})...");

    for (int i = 0; i < total; i += batchSize)
    {
        var batch = chunksToProcess.Skip(i).Take(batchSize).ToList();
        var texts = batch.Select(c => $"{string.Join(" > ", c.ParentHeadings)}\n\n{c.Content}").ToArray();

        // Embed (CPU-bound, sequential — ONNX session is not thread-safe)
        float[][] embeddings;
        try
        {
            embeddings = embedder.GenerateEmbeddings(texts)
                .Select(e => e.ToArray()).ToArray();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n  [ERROR] Embedding batch {i / batchSize + 1}: {ex.Message}");
            failed += batch.Count;
            continue;
        }

        // Build point structs
        var points = new List<PointStruct>();
        for (int j = 0; j < batch.Count; j++)
        {
            var chunk = batch[j];
            var fileHash = fileChunks[chunk.FilePath].Hash;
            var id = DeterministicGuid(chunk.FilePath + "#" + chunk.Heading);
            var payload = new Dictionary<string, Value>
            {
                ["file_path"] = chunk.FilePath,
                ["file_hash"] = fileHash,
                ["heading"] = chunk.Heading,
                ["heading_level"] = chunk.HeadingLevel,
                ["parent_headings"] = new Value { ListValue = new ListValue { Values = { chunk.ParentHeadings.Select(h => new Value { StringValue = h }) } } },
                ["category"] = chunk.Category,
                ["content"] = chunk.Content,
                ["indexed_at"] = DateTime.UtcNow.ToString("o")
            };

            points.Add(new PointStruct
            {
                Id = new PointId { Uuid = id.ToString() },
                Vectors = embeddings[j],
                Payload = { payload }
            });
        }

        // Wait for previous upsert before firing next (pipeline overlap)
        if (pendingUpsert != null)
            await pendingUpsert;

        // Fire upsert in background — next iteration embeds while this uploads
        var capturedPoints = points;
        var capturedCount = batch.Count;
        pendingUpsert = Task.Run(async () =>
        {
            await qdrantClient.UpsertAsync(config.Collection, capturedPoints);
            Interlocked.Add(ref processed, capturedCount);
        });

        Console.Write($"\r  Progress: {processed}/{total} chunks ({failed} failed)");
    }

    // Wait for final upsert
    if (pendingUpsert != null)
        await pendingUpsert;

    Console.Write($"\r  Progress: {processed}/{total} chunks ({failed} failed)");
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"Indexing complete: {processed} upserted, {failed} failed out of {total} total.");
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

static Guid DeterministicGuid(string input)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    var guidBytes = new byte[16];
    Array.Copy(hash, guidBytes, 16);
    guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
    guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
    return new Guid(guidBytes);
}

// ─────────────────────────────────────────────────────────────────────────────
#region ONNX Model Manager
// ─────────────────────────────────────────────────────────────────────────────

static class OnnxModelManager
{
    private const string ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";
    private const string VocabUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt";

    public static async Task EnsureModelAsync(string modelDir)
    {
        Directory.CreateDirectory(modelDir);
        var modelPath = Path.Combine(modelDir, "model.onnx");
        var vocabPath = Path.Combine(modelDir, "vocab.txt");

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(10);

        // Download both files concurrently if needed
        var tasks = new List<Task>();
        if (!File.Exists(modelPath))
        {
            Console.WriteLine("Downloading model.onnx from Hugging Face (~90MB, first run only)...");
            tasks.Add(Task.Run(async () =>
            {
                var bytes = await http.GetByteArrayAsync(ModelUrl);
                await File.WriteAllBytesAsync(modelPath, bytes);
                Console.WriteLine($"  Saved to {modelPath}");
            }));
        }
        if (!File.Exists(vocabPath))
        {
            Console.WriteLine("Downloading vocab.txt from Hugging Face...");
            tasks.Add(Task.Run(async () =>
            {
                var bytes = await http.GetByteArrayAsync(VocabUrl);
                await File.WriteAllBytesAsync(vocabPath, bytes);
                Console.WriteLine($"  Saved to {vocabPath}");
            }));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);

        Console.WriteLine("ONNX model files ready.");
    }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region Markdown Discovery
// ─────────────────────────────────────────────────────────────────────────────

static class MarkdownDiscovery
{
    private static readonly string[] ExcludeDirs =
        [".git", "node_modules", "bin", "obj", "data", "worktrees", "TestResults", ".cache"];

    public static List<string> FindAll(string repoRoot)
    {
        var files = new List<string>();
        WalkDirectory(repoRoot, files);
        return files.OrderBy(f => f).ToList();
    }

    private static void WalkDirectory(string dir, List<string> files)
    {
        var dirName = Path.GetFileName(dir);
        if (ExcludeDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            return;

        foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
            files.Add(file);

        foreach (var subDir in Directory.EnumerateDirectories(dir))
            WalkDirectory(subDir, files);
    }

    public static string CategorizeFile(string relativePath)
    {
        if (relativePath.StartsWith("specs/")) return "specs";
        if (relativePath.StartsWith(".github/skills/")) return "skills";
        if (relativePath.StartsWith(".github/agents/") || relativePath.StartsWith(".claude/agents/")) return "agents";
        if (relativePath.StartsWith(".github/prompts/") || relativePath.StartsWith(".claude/commands/")) return "prompts";
        if (relativePath.StartsWith(".claude/instructions/")) return "instructions";
        if (relativePath.StartsWith("src/docs/")) return "docs";
        if (relativePath.StartsWith(".specify/")) return "specify";
        if (relativePath.StartsWith("src/")) return "source";
        return "other";
    }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region Markdown Chunker
// ─────────────────────────────────────────────────────────────────────────────

record MarkdownChunk(
    string FilePath, string Heading, int HeadingLevel,
    List<string> ParentHeadings, string Category, string Content);

static class MarkdownChunker
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline);
    private const int MinChunkLength = 50;
    private const int MaxChunkLength = 1800; // ~512 tokens — max for all-MiniLM-L6-v2

    public static List<MarkdownChunk> Chunk(string markdown, string filePath, string category)
    {
        var matches = HeadingRegex.Matches(markdown);
        if (matches.Count == 0)
        {
            var trimmed = markdown.Trim();
            if (trimmed.Length < MinChunkLength) return [];
            return [new MarkdownChunk(filePath, Path.GetFileNameWithoutExtension(filePath), 0, [filePath], category, trimmed)];
        }

        var sections = new List<(int Level, string Heading, int Start, int End)>();
        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var level = m.Groups[1].Value.Length;
            var heading = m.Groups[2].Value.Trim();
            var start = m.Index + m.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;
            sections.Add((level, heading, start, end));
        }

        var preContent = markdown[..matches[0].Index].Trim();
        var chunks = new List<MarkdownChunk>();
        if (preContent.Length >= MinChunkLength)
        {
            chunks.Add(new MarkdownChunk(filePath, "Preamble", 0, [filePath], category, preContent));
        }

        var parentStack = new List<string> { filePath };

        foreach (var (level, heading, start, end) in sections)
        {
            var content = markdown[start..end].Trim();

            while (parentStack.Count > level)
                parentStack.RemoveAt(parentStack.Count - 1);
            if (parentStack.Count < level)
                parentStack.Add(heading);
            else
                parentStack[^1] = heading;

            var parents = new List<string>(parentStack);

            if (content.Length < MinChunkLength)
                continue;

            if (content.Length > MaxChunkLength)
            {
                var paragraphs = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
                var buffer = new StringBuilder();
                int partNum = 0;

                foreach (var para in paragraphs)
                {
                    if (buffer.Length + para.Length > MaxChunkLength && buffer.Length > 0)
                    {
                        partNum++;
                        chunks.Add(new MarkdownChunk(filePath, $"{heading} (part {partNum})", level, parents, category, buffer.ToString().Trim()));
                        buffer.Clear();
                    }
                    buffer.AppendLine(para);
                    buffer.AppendLine();
                }

                if (buffer.Length >= MinChunkLength)
                {
                    partNum++;
                    chunks.Add(new MarkdownChunk(filePath, partNum > 1 ? $"{heading} (part {partNum})" : heading, level, parents, category, buffer.ToString().Trim()));
                }
            }
            else
            {
                chunks.Add(new MarkdownChunk(filePath, heading, level, parents, category, content));
            }
        }

        return chunks;
    }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
#region CLI Config
// ─────────────────────────────────────────────────────────────────────────────

static class VectorCliConfig
{
    public record ParseResult(
        string QdrantHost, int QdrantPort, string Collection,
        string ModelDir, bool DryRun, bool Purge, string RepoRoot,
        List<string>? ChangedFiles);

    public static ParseResult Parse(string[] args)
    {
        var cliArgs = args
            .Where(a => a.StartsWith("--"))
            .Select(a => a[2..].Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "true");

        string GetArg(string key, string envVar, string fallback) =>
            cliArgs.GetValueOrDefault(key) ?? Environment.GetEnvironmentVariable(envVar) ?? fallback;

        var qdrantHost = GetArg("host", "QDRANT_HOST", "localhost");
        var qdrantPort = int.Parse(GetArg("port", "QDRANT_PORT", "6334"));
        var collection = GetArg("collection", "QDRANT_COLLECTION", "monxa-docs");
        var dryRun = cliArgs.ContainsKey("dry-run");
        var purge = cliArgs.ContainsKey("purge");

        // Resolve repo root
        var explicitSrc = cliArgs.GetValueOrDefault("src");
        string repoRoot;
        if (explicitSrc != null)
        {
            repoRoot = Path.GetFullPath(explicitSrc);
        }
        else
        {
            var scriptDir = Path.GetDirectoryName(Path.GetFullPath(
                Environment.GetCommandLineArgs().FirstOrDefault(a => a.EndsWith("vector.cs")) ?? "graph/vector.cs"))!;
            repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
        }

        // Model cached in ~/.cache/all-minilm-l6-v2/ (standard cache location, outside repo)
        var defaultCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "all-minilm-l6-v2");
        var modelDir = GetArg("model-dir", "ONNX_MODEL_DIR", defaultCacheDir);

        // --changed-files=file1.md,file2.md (comma-separated repo-relative paths)
        List<string>? changedFiles = null;
        if (cliArgs.TryGetValue("changed-files", out var changedFilesArg) && changedFilesArg != "true")
        {
            changedFiles = changedFilesArg
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim().Replace('\\', '/'))
                .ToList();
        }

        return new ParseResult(qdrantHost, qdrantPort, collection, modelDir, dryRun, purge, repoRoot, changedFiles);
    }
}

#endregion
