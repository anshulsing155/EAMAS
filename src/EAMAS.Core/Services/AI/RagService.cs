using EAMAS.Core.Data;
using EAMAS.Core.Models;
using MongoDB.Driver;

namespace EAMAS.Core.Services.AI
{
    /// <summary>
    /// Retrieval-Augmented Generation: chunk project docs → embed → store → semantic search.
    /// Uses in-memory cosine similarity (no external vector DB required for &lt;10k chunks).
    /// </summary>
    public class RagService
    {
        private readonly MongoDbContext _db;

        public RagService(MongoDbContext db) => _db = db;

        public async Task IndexProjectAsync(string projectId, IAiProvider provider,
            string prdContent, string architectureNotes, string techStack)
        {
            // Remove old prd/architecture embeddings for this project
            await _db.ProjectEmbeddings.DeleteManyAsync(
                e => e.ProjectId == projectId && (e.ChunkType == "prd" || e.ChunkType == "architecture")).ConfigureAwait(false);

            var chunks = new List<(string type, string source, string content)>();

            if (!string.IsNullOrWhiteSpace(prdContent))
                chunks.AddRange(ChunkText(prdContent, 400).Select((c, i) => ("prd", $"PRD-{i}", c)));

            if (!string.IsNullOrWhiteSpace(architectureNotes))
                chunks.AddRange(ChunkText(architectureNotes, 400).Select((c, i) => ("architecture", $"ARCH-{i}", c)));

            if (!string.IsNullOrWhiteSpace(techStack))
                chunks.Add(("architecture", "TechStack", techStack));

            var embeddings = new List<ProjectEmbedding>();
            foreach (var (type, source, content) in chunks)
            {
                float[] vec = Array.Empty<float>();
                try { vec = await provider.EmbedAsync(content).ConfigureAwait(false); }
                catch { /* embedding optional — fallback to keyword search */ }

                embeddings.Add(new ProjectEmbedding
                {
                    ProjectId = projectId,
                    ChunkType = type,
                    SourcePath = source,
                    Content = content,
                    Embedding = vec,
                    IndexedAt = DateTime.UtcNow
                });
            }

            if (embeddings.Any())
                await _db.ProjectEmbeddings.InsertManyAsync(embeddings).ConfigureAwait(false);
        }

        public async Task IndexCodeChunkAsync(string projectId, IAiProvider provider,
            string filePath, string content, string commitSha)
        {
            // Remove old embedding for this file+commit
            await _db.ProjectEmbeddings.DeleteManyAsync(
                e => e.ProjectId == projectId && e.SourcePath == filePath).ConfigureAwait(false);

            var chunks = ChunkText(content, 500).Take(10).ToList(); // max 10 chunks per file
            var embeddings = new List<ProjectEmbedding>();

            foreach (var (chunk, idx) in chunks.Select((c, i) => (c, i)))
            {
                float[] vec = Array.Empty<float>();
                try { vec = await provider.EmbedAsync(chunk).ConfigureAwait(false); }
                catch { }

                embeddings.Add(new ProjectEmbedding
                {
                    ProjectId = projectId,
                    ChunkType = "code",
                    SourcePath = $"{filePath}#{idx}",
                    Content = chunk,
                    Embedding = vec,
                    CommitSha = commitSha,
                    IndexedAt = DateTime.UtcNow
                });
            }

            if (embeddings.Any())
                await _db.ProjectEmbeddings.InsertManyAsync(embeddings).ConfigureAwait(false);
        }

        /// <summary>Returns top-K most relevant chunks via cosine similarity (or keyword if no embeddings).</summary>
        public async Task<List<string>> SearchAsync(string projectId, IAiProvider provider,
            string query, int topK = 5)
        {
            var all = await _db.ProjectEmbeddings
                .Find(e => e.ProjectId == projectId)
                .ToListAsync().ConfigureAwait(false);

            if (!all.Any()) return new List<string>();

            // If provider supports embeddings, use cosine similarity
            var hasEmbeddings = all.Any(e => e.Embedding.Length > 0);
            if (hasEmbeddings)
            {
                float[] queryVec = Array.Empty<float>();
                try { queryVec = await provider.EmbedAsync(query).ConfigureAwait(false); }
                catch { }

                if (queryVec.Length > 0)
                {
                    return all
                        .Where(e => e.Embedding.Length == queryVec.Length)
                        .Select(e => (e.Content, Score: CosineSimilarity(queryVec, e.Embedding)))
                        .OrderByDescending(x => x.Score)
                        .Take(topK)
                        .Select(x => x.Content)
                        .ToList();
                }
            }

            // Keyword fallback: simple term frequency scoring
            var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return all
                .Select(e => (e.Content, Score: queryTerms.Count(t => e.Content.ToLowerInvariant().Contains(t))))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Content)
                .ToList();
        }

        public string BuildContext(List<string> chunks) =>
            string.Join("\n---\n", chunks.Select((c, i) => $"[Context {i + 1}]\n{c}"));

        private static List<string> ChunkText(string text, int targetTokens)
        {
            // Approximate: 1 token ≈ 4 chars
            int charLimit = targetTokens * 4;
            var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<string>();
            var current = new System.Text.StringBuilder();

            foreach (var para in paragraphs)
            {
                if (current.Length + para.Length > charLimit && current.Length > 0)
                {
                    chunks.Add(current.ToString().Trim());
                    current.Clear();
                }
                current.AppendLine(para);
            }
            if (current.Length > 0) chunks.Add(current.ToString().Trim());
            return chunks;
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0, magA = 0, magB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }
            float denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
            return denom == 0 ? 0 : dot / denom;
        }
    }
}
