using System.Text.Json;
using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using ClarityClaim.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClarityClaim.Services;

// Orchestrates transcript + FHIR context -> the clinical-reasoning model -> structured SOAP
// note + ICD-10/CPT codes. Falls back to pre-generated JSON if inference fails.
public class SoapGenerationService
{
    private readonly ILanguageModelClient _lm;
    private readonly IConfiguration _config;
    private readonly ILogger<SoapGenerationService> _logger;
    private readonly IPipelinePersistenceRepository _persistence;

    public SoapGenerationService(ILanguageModelClient lm, IConfiguration config, ILogger<SoapGenerationService> logger, IPipelinePersistenceRepository persistence)
    {
        _lm = lm;
        _config = config;
        _logger = logger;
        _persistence = persistence;
    }

    public async Task<SoapResponse> GenerateAsync(
        GenerateSoapRequest req, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        SoapNote note;
        bool fromFallback;

        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(req.Transcript, req.PatientContext);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(45)); // BP-04 -- fallback after 45s

            var raw = await _lm.CompleteAsync(
                OllamaClient.Models.CLINICAL_REASONING,
                systemPrompt, userPrompt,
                temperature: 0.1f, maxTokens: 4096, ct: cts.Token
            );

            note = ParseSoapJson(raw);
            fromFallback = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SOAP generation failed live -- serving fallback");
            note = LoadFallback();
            fromFallback = true;
        }
        stopwatch.Stop();

        int? soapNoteId = null;
        if (req.VisitId is int visitId)
        {
            // Persist + audit -- never let a DB hiccup break the API response.
            try
            {
                soapNoteId = await _persistence.SaveSoapNoteAsync(visitId, note, fromFallback, ct);
                await _persistence.LogAuditAsync(visitId, null, null, "SoapGeneration",
                    fromFallback ? "Fallback" : "Success", (int)stopwatch.ElapsedMilliseconds, null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist SOAP note / audit log for visit {VisitId}", visitId);
            }
        }

        return new SoapResponse(note, fromFallback, soapNoteId);
    }

    private static string BuildSystemPrompt() => """
        You are a clinical documentation specialist. Generate structured SOAP notes
        from doctor-patient encounter transcripts. Output valid JSON only.
        Use ICD-10 codes at the highest specificity -- never unspecified codes.
        Prefer M51.16 over M54.5 (unspecified low back pain) when radiculopathy
        or disc pathology is documented. Always link codes to transcript evidence.
        """;

    private static string BuildUserPrompt(string transcript, FhirPatientContext ctx) => $$"""
        PATIENT CONTEXT:
        Name: {{ctx.FullName}} | DOB: {{ctx.DateOfBirth}} | Gender: {{ctx.Gender}}
        Existing Conditions: {{string.Join(", ", ctx.Conditions)}}
        Current Medications: {{string.Join(", ", ctx.Medications)}}
        Known Allergies: {{string.Join(", ", ctx.Allergies)}}

        ENCOUNTER TRANSCRIPT:
        {{transcript}}

        Return ONLY this JSON:
        {
          "subjective": "...",
          "objective": "...",
          "assessment": "...",
          "plan": "...",
          "icd10Codes": [{"code":"M51.16","description":"...","confidence":0.94}],
          "cptCodes": [{"code":"72148","description":"...","confidence":0.91}],
          "clinicalReasoning": "..."
        }
        """;

    private static SoapNote ParseSoapJson(string raw)
    {
        // Strip any markdown code fences the model may add
        var clean = raw.Trim().Trim('`').Trim();
        if (clean.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            clean = clean[4..].Trim();

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<SoapNote>(clean, opts)
            ?? throw new InvalidOperationException("Failed to parse SOAP JSON");
    }

    private SoapNote LoadFallback()
    {
        var path = Path.Combine(_config["Fallbacks:FolderPath"] ?? "Data/Fallbacks", "soap-note.json");
        if (!File.Exists(path)) return HardcodedFallback();
        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<SoapNote>(json, opts) ?? HardcodedFallback();
    }

    private static SoapNote HardcodedFallback() => new()
    {
        Subjective = "Patient reports 6 weeks of progressive low back pain radiating down the left leg to the calf, consistent with radiculopathy. Pain worsens with sitting and bending.",
        Objective = "Positive straight leg raise on the left at 40 degrees. Diminished sensation in the L5 dermatome. Reflexes 2+ and symmetric. No motor deficit noted.",
        Assessment = "Lumbar radiculopathy, likely secondary to disc pathology at L4-L5 or L5-S1.",
        Plan = "Order lumbar MRI without contrast to evaluate for disc herniation. Continue NSAIDs and physical therapy. Follow up in 2 weeks with imaging results.",
        Icd10Codes = [new CodedDiagnosis("M51.16", "Intervertebral disc disorders with radiculopathy, lumbar region", 0.93)],
        CptCodes = [new CodedProcedure("72148", "MRI lumbar spine without contrast", 0.90)],
        ClinicalReasoning = "Radicular symptoms with positive straight leg raise and dermatomal sensory loss support disc-related nerve root compression, warranting MRI per LCD L34220 criteria."
    };
}
