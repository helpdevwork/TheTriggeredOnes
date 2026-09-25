using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Tesseract;

namespace ClarityClaim.Infrastructure.Ocr;

// Fallback for when a policy PDF has no CMS live-page match and no extractable
// text of its own (the 6 shipped CMS PDFs are scanned images -- see README).
// Rasterizes each page with PDFtoImage (PDFium) and OCRs it with Tesseract.
// Requires the eng.traineddata language file to be downloaded once (like
// Whisper's ggml model) -- if it's missing, this quietly returns null so the
// resolution chain falls through to the (near-empty) raw PDF text instead of
// crashing startup.
public class PolicyPdfOcrService
{
    private readonly string _tessDataPath;
    private readonly ILogger<PolicyPdfOcrService> _logger;

    public PolicyPdfOcrService(IConfiguration config, ILogger<PolicyPdfOcrService> logger)
    {
        _tessDataPath = config["Ocr:TessDataPath"] ?? "Data/Models/tessdata";
        _logger = logger;
    }

    public Task<string?> OcrPdfAsync(string pdfPath, CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(_tessDataPath, "eng.traineddata")))
        {
            _logger.LogWarning("Tesseract language data not found at {Path} -- skipping OCR fallback", _tessDataPath);
            return Task.FromResult<string?>(null);
        }

        try
        {
            using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
            var pdfBytes = File.ReadAllBytes(pdfPath);
            var sb = new System.Text.StringBuilder();
            var pageNum = 0;

            foreach (var bitmap in PDFtoImage.Conversion.ToImages(pdfBytes))
            {
                ct.ThrowIfCancellationRequested();
                pageNum++;

                using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                using var pix = Pix.LoadFromMemory(png.ToArray());
                using var page = engine.Process(pix);
                sb.AppendLine(page.GetText());
                bitmap.Dispose();
            }

            _logger.LogInformation("OCR'd {Pages} pages from {Path}", pageNum, Path.GetFileName(pdfPath));
            return Task.FromResult<string?>(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR failed for {Path}", pdfPath);
            return Task.FromResult<string?>(null);
        }
    }
}
