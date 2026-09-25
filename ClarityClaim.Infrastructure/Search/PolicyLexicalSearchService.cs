using System.Text.RegularExpressions;
using ClarityClaim.Domain.Interfaces;

namespace ClarityClaim.Infrastructure.Search;

// Policy retrieval via TF-IDF cosine similarity over chunked PDF text, kept
// entirely in memory. No embedding model is available on the configured
// Ollama server (see OllamaClient.Models), so this replaces the original
// Nomic-embedding + vector-store design with a lexical alternative that needs
// no LLM call at all for retrieval -- faster and has no external dependency.
public class PolicyLexicalSearchService : IPolicySearchService
{
    private readonly List<IndexedChunk> _chunks = [];
    private readonly Lock _lock = new();
    private const int MAX_CHUNK_CHARS = 1400 * 4;   // ~1400 tokens, matching the original chunking size (BP-07)
    private const int OVERLAP_CHARS = 150 * 4;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "are", "was", "were", "that", "this", "with", "from",
        "have", "has", "had", "not", "but", "you", "your", "shall", "must", "may",
        "can", "been", "being", "also", "into", "such", "their", "its", "who",
        "which", "when", "where", "how", "what", "all", "any", "each", "other"
    };

    public Task IndexDocumentAsync(string text, string source, string lcdId)
    {
        var rawChunks = ChunkText(text, MAX_CHUNK_CHARS, OVERLAP_CHARS);

        lock (_lock)
        {
            foreach (var (chunk, idx) in rawChunks.Select((c, i) => (c, i)))
            {
                var tf = ComputeTermFrequency(Tokenize(chunk));
                _chunks.Add(new IndexedChunk(chunk, source, lcdId, idx, tf));
            }
        }
        return Task.CompletedTask;
    }

    public Task<List<PolicyChunk>> RetrieveAsync(string query, int topK = 3)
    {
        List<IndexedChunk> snapshot;
        lock (_lock) { snapshot = [.. _chunks]; }
        if (snapshot.Count == 0) return Task.FromResult(new List<PolicyChunk>());

        var queryTf = ComputeTermFrequency(Tokenize(query));
        if (queryTf.Count == 0) return Task.FromResult(new List<PolicyChunk>());

        var idf = ComputeIdf(snapshot, queryTf.Keys);

        var results = snapshot
            .Select(c => (chunk: c, score: CosineSimilarity(queryTf, c.TermFrequency, idf)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => new PolicyChunk(x.chunk.Text, x.chunk.Source, x.chunk.LcdId, x.chunk.Page))
            .ToList();

        return Task.FromResult(results);
    }

    private static List<string> Tokenize(string text) =>
        Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+")
            .Select(m => m.Value)
            .Where(t => t.Length > 2 && !StopWords.Contains(t))
            .ToList();

    private static Dictionary<string, int> ComputeTermFrequency(List<string> terms)
    {
        var tf = new Dictionary<string, int>();
        foreach (var t in terms) tf[t] = tf.GetValueOrDefault(t) + 1;
        return tf;
    }

    // Smoothed IDF over the current corpus, computed only for the query's terms (cheap -- corpus is a few hundred chunks)
    private static Dictionary<string, double> ComputeIdf(List<IndexedChunk> corpus, IEnumerable<string> terms)
    {
        var idf = new Dictionary<string, double>();
        var n = corpus.Count;
        foreach (var term in terms.Distinct())
        {
            var df = corpus.Count(c => c.TermFrequency.ContainsKey(term));
            idf[term] = Math.Log((n + 1.0) / (df + 1.0)) + 1.0;
        }
        return idf;
    }

    // Cosine similarity restricted to the query's term dimensions -- a practical
    // simplification (not full-corpus cosine) that's more than sufficient for
    // ranking a few hundred short policy chunks against a short query.
    private static double CosineSimilarity(Dictionary<string, int> queryTf, Dictionary<string, int> docTf, Dictionary<string, double> idf)
    {
        double dot = 0, queryNorm = 0, docNorm = 0;

        foreach (var (term, qCount) in queryTf)
        {
            var w = idf.GetValueOrDefault(term, 0);
            var qWeight = qCount * w;
            queryNorm += qWeight * qWeight;

            if (docTf.TryGetValue(term, out var dCount))
            {
                var dWeight = dCount * w;
                dot += qWeight * dWeight;
                docNorm += dWeight * dWeight;
            }
        }

        if (queryNorm == 0 || docNorm == 0) return 0;
        return dot / (Math.Sqrt(queryNorm) * Math.Sqrt(docNorm));
    }

    // Simple word-boundary chunking with overlap
    private static List<string> ChunkText(string text, int maxChars, int overlapChars)
    {
        var chunks = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + maxChars, text.Length);
            chunks.Add(text[start..end]);
            if (end == text.Length) break;
            start += maxChars - overlapChars;
        }
        return chunks;
    }

    private record IndexedChunk(string Text, string Source, string LcdId, int Page, Dictionary<string, int> TermFrequency);
}
