using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace ClarityClaim.Infrastructure.Cms;

// Fetches the real, live policy text for an LCD/Article ID from CMS's public
// Medicare Coverage Database (cms.gov) -- the same official source the scanned
// PDFs in Data/Policies/ were printed from, but published there as accessible
// HTML rather than a scanned image, so it has genuine extractable text.
// Called once per policy; the result is cached to disk by PolicyTextResolver,
// so this is not a runtime dependency of the running app.
public class CmsPolicyTextService
{
    private readonly HttpClient _http;
    private readonly ILogger<CmsPolicyTextService> _logger;

    // The specific content sections on an LCD/Article page -- deliberately excludes
    // the site's search/navigation chrome, which surrounds these in a <main> wrapper.
    private static readonly string[] ContentDivIds =
    [
        "divCoverageIndication", "divCmsNationalCoveragePolicy", "divAnalysisOfEvidence",
        "divBibliography", "divArticleText", "divBillCodes", "divHcpcsCodes",
        "divIcd10Support", "divIcd10DontSupport", "divIcd10Covered", "divIcd10Noncovered",
        "divOtherCoding"
    ];

    public CmsPolicyTextService(IHttpClientFactory factory, ILogger<CmsPolicyTextService> logger)
    {
        _http = factory.CreateClient("cms");
        _logger = logger;
    }

    // Minimum acceptable extracted length -- shorter than this means the expected
    // content divs weren't found (e.g. site layout changed) and the fetch should
    // be treated as failed rather than caching near-empty text.
    private const int MIN_VALID_LENGTH = 200;

    public async Task<string?> FetchPolicyTextAsync(string lcdOrArticleId, CancellationToken ct = default)
    {
        try
        {
            var isLcd = lcdOrArticleId.StartsWith('L');
            var numericId = lcdOrArticleId[1..];
            var path = isLcd
                ? $"medicare-coverage-database/view/lcd.aspx?lcdid={numericId}"
                : $"medicare-coverage-database/view/article.aspx?articleId={numericId}";

            var html = await _http.GetStringAsync(path, ct);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var sb = new StringBuilder();
            foreach (var id in ContentDivIds)
            {
                var node = doc.GetElementbyId(id);
                if (node is null) continue;

                var decoded = System.Net.WebUtility.HtmlDecode(node.InnerText);
                var text = Regex.Replace(decoded, @"\s+", " ").Trim();
                if (text.Length > 0) sb.AppendLine(text);
            }

            var result = sb.ToString().Trim();
            if (result.Length < MIN_VALID_LENGTH)
            {
                _logger.LogWarning("CMS fetch for {Id} returned only {Length} chars -- treating as failed", lcdOrArticleId, result.Length);
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CMS live fetch failed for {Id}", lcdOrArticleId);
            return null;
        }
    }
}
