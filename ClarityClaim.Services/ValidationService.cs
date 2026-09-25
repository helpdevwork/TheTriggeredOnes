using System.Text.Json;
using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Enums;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using ClarityClaim.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClarityClaim.Services;

// The core of the product. Retrieves relevant NCD/LCD policy chunks via lexical
// search, calls two independent models for gap analysis (primary + second opinion),
// merges scores with weighted average (0.6 / 0.4), and returns a full scorecard.
public class ValidationService
{
    private readonly ILanguageModelClient _lm;
    private readonly IPolicySearchService _policySearch;
    private readonly IConfiguration _config;
    private readonly ILogger<ValidationService> _logger;
    private readonly IPipelinePersistenceRepository _persistence;

    private const string VALIDATION_SYSTEM_PROMPT = """
        You are a medical billing compliance specialist.
        Evaluate clinical documentation against CMS NCD/LCD coverage criteria.
        Identify exactly what is missing, insufficient, or inconsistent.
        Output valid JSON only. No markdown, no explanation outside the JSON.
        """;

    public ValidationService(ILanguageModelClient lm, IPolicySearchService policySearch, IConfiguration config, ILogger<ValidationService> logger, IPipelinePersistenceRepository persistence)
    {
        _lm = lm;
        _policySearch = policySearch;
        _config = config;
        _logger = logger;
        _persistence = persistence;
    }

    public async Task<ValidationResponse> ValidateAsync(
        ValidateDocumentRequest req, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        ValidationResult result;
        bool fromFallback;

        try
        {
            // Step 1: policy retrieval -- query using ICD codes + "medical necessity"
            var query = $"{string.Join(" ", req.Icd10Codes)} medical necessity documentation requirements";
            var chunks = await _policySearch.RetrieveAsync(query, topK: 3);

            var policyContext = BuildPolicyContext(chunks);
            var soapText = SoapToText(req.SoapNote);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(45)); // BP-04 -- fallback after 45s

            // Step 2: Run both validator models in parallel for speed
            var primaryTask = RunPrimaryValidationAsync(soapText, policyContext, cts.Token);
            var secondaryTask = RunSecondaryValidationAsync(soapText, policyContext, cts.Token);
            await Task.WhenAll(primaryTask, secondaryTask);

            var primaryResult = primaryTask.Result;
            var secondaryResult = secondaryTask.Result;

            // Step 3: Merge -- primary weighted 0.6, secondary 0.4
            var mergedScore = (int)Math.Round(
                primaryResult.ApprovalScore * 0.6 +
                secondaryResult.ApprovalScore * 0.4
            );

            // Step 4: Flag disagreements -- where models differ by >20 points
            var flags = Math.Abs(primaryResult.ApprovalScore - secondaryResult.ApprovalScore) > 20
                ? new List<string> { "Models disagree significantly — human review recommended" }
                : [];

            result = new ValidationResult
            {
                ApprovalProbability = mergedScore,
                Gaps = primaryResult.Gaps,
                Fixes = primaryResult.Fixes,
                PolicyReferences = chunks.Select(c => $"{c.Source} p.{c.Page}").ToList(),
                PassingCriteria = primaryResult.PassingCriteria,
                HumanReviewFlags = flags
            };
            fromFallback = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Validation failed live -- serving fallback");
            result = LoadFallbackValidation(req);
            fromFallback = true;
        }
        stopwatch.Stop();

        if (req.VisitId is int visitId)
        {
            try
            {
                await _persistence.SaveValidationResultAsync(visitId, req.SoapNoteId, result, fromFallback, ct);
                await _persistence.LogAuditAsync(visitId, null, null, "Validation",
                    fromFallback ? "Fallback" : "Success", (int)stopwatch.ElapsedMilliseconds, null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist validation result / audit log for visit {VisitId}", visitId);
            }
        }

        return new ValidationResponse(result, fromFallback);
    }

    private async Task<LlmValidationOutput> RunPrimaryValidationAsync(
        string soapText, string policyContext, CancellationToken ct)
    {
        var raw = await _lm.CompleteAsync(
            OllamaClient.Models.VALIDATION_PRIMARY,
            VALIDATION_SYSTEM_PROMPT,
            BuildValidationUserPrompt(soapText, policyContext),
            temperature: 0.1f, maxTokens: 3000, ct: ct
        );
        return ParseValidationJson(raw);
    }

    private async Task<LlmValidationOutput> RunSecondaryValidationAsync(
        string soapText, string policyContext, CancellationToken ct)
    {
        var raw = await _lm.CompleteAsync(
            OllamaClient.Models.VALIDATION_SECONDARY,
            VALIDATION_SYSTEM_PROMPT,
            BuildValidationUserPrompt(soapText, policyContext),
            temperature: 0.1f, maxTokens: 2000, ct: ct
        );
        return ParseValidationJson(raw);
    }

    private static string BuildPolicyContext(List<PolicyChunk> chunks) =>
        string.Join("\n\n---\n\n", chunks.Select(c => $"[{c.Source}]\n{c.Text}"));

    private static string SoapToText(SoapNote note) => $"""
        Subjective: {note.Subjective}
        Objective: {note.Objective}
        Assessment: {note.Assessment}
        Plan: {note.Plan}
        ICD-10 Codes: {string.Join(", ", note.Icd10Codes.Select(c => $"{c.Code} ({c.Description})"))}
        CPT Codes: {string.Join(", ", note.CptCodes.Select(c => $"{c.Code} ({c.Description})"))}
        """;

    private static string BuildValidationUserPrompt(string soapText, string policyContext) => $$"""
        RELEVANT CMS NCD/LCD POLICY TEXT:
        {{policyContext}}

        CLINICAL DOCUMENTATION TO VALIDATE:
        {{soapText}}

        Evaluate this documentation against the policy criteria above. Return ONLY this JSON:
        {
          "approvalScore": 74,
          "gaps": [{"policyRef":"LCD L34220","requirement":"minimum 4 weeks conservative treatment","currentDoc":"no conservative treatment duration documented","severity":"Critical"}],
          "fixes": [{"gapIndex":0,"suggestedLanguage":"Patient completed 4 weeks of physical therapy and NSAIDs without improvement."}],
          "passingCriteria": ["Radicular symptoms documented", "Positive straight leg raise documented"]
        }
        "severity" must be exactly one of: "Critical", "Major", "Minor".
        """;

    private static LlmValidationOutput ParseValidationJson(string raw)
    {
        var clean = raw.Trim().Trim('`').Trim();
        if (clean.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            clean = clean[4..].Trim();

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        return JsonSerializer.Deserialize<LlmValidationOutput>(clean, opts)
            ?? throw new InvalidOperationException("Failed to parse validation JSON");
    }

    // Chooses the pre-fix or post-fix fallback JSON based on whether the SOAP note
    // already documents the 4-week conservative-treatment fix -- keeps the 74%->93%
    // demo moment working even when running fully offline (BP-04).
    private ValidationResult LoadFallbackValidation(ValidateDocumentRequest req)
    {
        var noteText = $"{req.SoapNote.Subjective} {req.SoapNote.Plan}".ToLowerInvariant();
        var fixApplied = noteText.Contains("4 week") && (noteText.Contains("physical therapy") || noteText.Contains("conservative"));
        var fileName = fixApplied ? "validation-postfix.json" : "validation-prefix.json";

        var path = Path.Combine(_config["Fallbacks:FolderPath"] ?? "Data/Fallbacks", fileName);
        if (!File.Exists(path)) return HardcodedFallback();
        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        return JsonSerializer.Deserialize<ValidationResult>(json, opts) ?? HardcodedFallback();
    }

    private static ValidationResult HardcodedFallback() => new()
    {
        ApprovalProbability = 74,
        Gaps =
        [
            new ValidationGap("LCD L34220", "Minimum 4 weeks of documented conservative treatment (PT, NSAIDs, or activity modification) prior to advanced imaging", "No conservative treatment duration documented in the current note", ValidationSeverity.Critical)
        ],
        Fixes =
        [
            new ValidationFix(0, "Patient completed 4 weeks of physical therapy and NSAID therapy without symptomatic improvement, per LCD L34220 conservative management requirement.")
        ],
        PolicyReferences = ["LCD L34220 — Lumbar MRI p.3"],
        PassingCriteria = ["Radicular symptoms documented", "Positive straight leg raise documented", "Dermatomal sensory findings documented"],
        HumanReviewFlags = []
    };
}

// Internal parse target -- not exposed outside Services layer
internal record LlmValidationOutput(
    int ApprovalScore,
    List<ValidationGap> Gaps,
    List<ValidationFix> Fixes,
    List<string> PassingCriteria
);
