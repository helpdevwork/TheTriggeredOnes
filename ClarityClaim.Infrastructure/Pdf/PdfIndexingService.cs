using System.Text;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Infrastructure.PolicyText;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace ClarityClaim.Infrastructure.Pdf;

// Reads the NCD/LCD policy PDFs from /Data/Policies/, resolves each one's real
// text (see PolicyTextResolver -- the shipped PDFs are scanned images with no
// extractable text of their own), and indexes into the policy search index.
// Runs once on application startup.
public class PdfIndexingService : IPdfIndexingService
{
    private readonly IPolicySearchService _policySearch;
    private readonly PolicyTextResolver _textResolver;
    private readonly ILogger<PdfIndexingService> _logger;

    // Maps filename prefix to LCD ID for metadata. Corrected IDs per Development Plan
    // Section 1.1 -- NOT L35062/L34067/L35101 as the stale HLD/strategy docs claimed.
    internal static readonly Dictionary<string, string> LCD_MAP = new()
    {
        { "L34220", "LCD L34220 — Lumbar MRI" },
        { "L36839", "LCD L36839 — Polysomnography & Sleep Studies" },
        { "L39266", "LCD L39266 — Cognitive Assessment" },
        { "A56903", "Article A56903 — Sleep Studies Billing & Coding" },
        { "A57206", "Article A57206 — Lumbar MRI Billing & Coding" },
        { "A59036", "Article A59036 — Cognitive Assessment Coding" }
    };

    public PdfIndexingService(IPolicySearchService policySearch, PolicyTextResolver textResolver, ILogger<PdfIndexingService> logger)
    {
        _policySearch = policySearch;
        _textResolver = textResolver;
        _logger = logger;
    }

    public async Task IndexAllPoliciesAsync(string policiesFolderPath)
    {
        if (!Directory.Exists(policiesFolderPath))
        {
            _logger.LogWarning("Policies folder not found at {Path} -- skipping RAG indexing", policiesFolderPath);
            return;
        }

        var pdfFiles = Directory.GetFiles(policiesFolderPath, "*.pdf");

        foreach (var filePath in pdfFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var lcdId = LCD_MAP.Keys.FirstOrDefault(k => fileName.StartsWith(k)) ?? fileName;
            var source = LCD_MAP.GetValueOrDefault(lcdId, fileName);

            _logger.LogInformation("Indexing policy: {Source}", source);

            try
            {
                var rawPdfText = ExtractPdfText(filePath);
                var text = await _textResolver.ResolveAsync(lcdId, filePath, rawPdfText);
                await _policySearch.IndexDocumentAsync(text, source, lcdId);
                _logger.LogInformation("Indexed {Source} — {Chars} chars", source, text.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to index {File} -- continuing with remaining policies", fileName);
            }
        }
    }

    private static string ExtractPdfText(string filePath)
    {
        using var doc = PdfDocument.Open(filePath);
        var sb = new StringBuilder();

        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);

        return sb.ToString();
    }
}
