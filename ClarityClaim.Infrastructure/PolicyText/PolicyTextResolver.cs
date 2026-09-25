using ClarityClaim.Infrastructure.Cms;
using ClarityClaim.Infrastructure.Ocr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClarityClaim.Infrastructure.PolicyText;

// Resolution chain for getting real policy text for one LCD/Article ID, in
// order of preference: disk cache -> live CMS fetch (then cached) -> OCR of
// the scanned PDF (then cached) -> the raw (likely near-empty) PdfPig text as
// a last resort so indexing never fails outright. Each step only runs if the
// previous one didn't produce usable text -- CMS and OCR are each called at
// most once per policy per cache lifetime, not on every startup.
public class PolicyTextResolver
{
    private readonly CmsPolicyTextService _cms;
    private readonly PolicyPdfOcrService _ocr;
    private readonly ILogger<PolicyTextResolver> _logger;
    private readonly string _cacheDir;

    private const int MIN_USABLE_LENGTH = 100;

    public PolicyTextResolver(CmsPolicyTextService cms, PolicyPdfOcrService ocr, IConfiguration config, ILogger<PolicyTextResolver> logger)
    {
        _cms = cms;
        _ocr = ocr;
        _logger = logger;
        _cacheDir = config["Policies:FolderPath"] ?? "Data/Policies";
    }

    public async Task<string> ResolveAsync(string lcdOrArticleId, string pdfFilePath, string rawPdfText, CancellationToken ct = default)
    {
        var cachePath = Path.Combine(_cacheDir, $"{lcdOrArticleId}.txt");

        if (File.Exists(cachePath))
        {
            var cached = await File.ReadAllTextAsync(cachePath, ct);
            if (cached.Length >= MIN_USABLE_LENGTH)
            {
                _logger.LogInformation("Policy text for {Id}: using cached text ({Chars} chars)", lcdOrArticleId, cached.Length);
                return cached;
            }
        }

        var cmsText = await _cms.FetchPolicyTextAsync(lcdOrArticleId, ct);
        if (cmsText is not null)
        {
            await File.WriteAllTextAsync(cachePath, cmsText, ct);
            _logger.LogInformation("Policy text for {Id}: fetched live from CMS ({Chars} chars)", lcdOrArticleId, cmsText.Length);
            return cmsText;
        }

        var ocrText = await _ocr.OcrPdfAsync(pdfFilePath, ct);
        if (!string.IsNullOrWhiteSpace(ocrText) && ocrText.Length >= MIN_USABLE_LENGTH)
        {
            await File.WriteAllTextAsync(cachePath, ocrText, ct);
            _logger.LogInformation("Policy text for {Id}: OCR'd from PDF ({Chars} chars)", lcdOrArticleId, ocrText.Length);
            return ocrText;
        }

        _logger.LogWarning(
            "Policy text for {Id}: CMS fetch and OCR both unavailable -- falling back to raw PDF text ({Chars} chars, likely near-empty)",
            lcdOrArticleId, rawPdfText.Length);
        return rawPdfText;
    }
}
