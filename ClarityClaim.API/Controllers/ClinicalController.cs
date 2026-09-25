using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClarityClaim.API.Controllers;

// Thin controller. Receives HTTP, calls one service method, returns result.
// No business logic here. All at localhost:8000/api/.
[ApiController]
[Route("api")]
public class ClinicalController : ControllerBase
{
    private readonly SoapGenerationService _soapSvc;
    private readonly ValidationService _validSvc;
    private readonly PatientSummaryService _summarySvc;
    private readonly RegulatoryService _regSvc;
    private readonly IFhirService _fhirSvc;
    private readonly ITranscriptionService _sttSvc;
    private readonly IPipelinePersistenceRepository _persistence;
    private readonly ILogger<ClinicalController> _logger;

    public ClinicalController(
        SoapGenerationService soapSvc,
        ValidationService validSvc,
        PatientSummaryService summarySvc,
        RegulatoryService regSvc,
        IFhirService fhirSvc,
        ITranscriptionService sttSvc,
        IPipelinePersistenceRepository persistence,
        ILogger<ClinicalController> logger)
    {
        _soapSvc = soapSvc;
        _validSvc = validSvc;
        _summarySvc = summarySvc;
        _regSvc = regSvc;
        _fhirSvc = fhirSvc;
        _sttSvc = sttSvc;
        _persistence = persistence;
        _logger = logger;
    }

    // ── GET /api/patient/{id} ───────────────────────────────
    [HttpGet("patient/{patientId}")]
    public async Task<IActionResult> GetPatient(string patientId, CancellationToken ct)
    {
        var result = await _fhirSvc.GetPatientContextAsync(patientId, ct);
        return Ok(result);
    }

    // ── POST /api/transcribe?visitId=123 ────────────────────
    [HttpPost("transcribe")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Transcribe(IFormFile audio, [FromQuery] int? visitId, CancellationToken ct)
    {
        if (audio is null || audio.Length == 0)
            return BadRequest("No audio file provided.");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tmpPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");
        try
        {
            await using (var stream = new FileStream(tmpPath, FileMode.Create))
                await audio.CopyToAsync(stream, ct);

            var result = await _sttSvc.TranscribeAsync(tmpPath, ct);
            stopwatch.Stop();

            if (visitId is int vid)
                await TryPersistTranscriptionAsync(vid, result.FullText, "Audio", (int)stopwatch.ElapsedMilliseconds, ct);

            return Ok(result);
        }
        catch (Exception ex) when (visitId is int failedVid)
        {
            await TryLogAuditAsync(failedVid, "Transcription", "Error", (int)stopwatch.ElapsedMilliseconds, ct);
            _logger.LogWarning(ex, "Transcription failed for visit {VisitId}", failedVid);
            throw;
        }
        finally
        {
            if (System.IO.File.Exists(tmpPath))
                System.IO.File.Delete(tmpPath);
        }
    }

    // ── POST /api/transcript?visitId=123 -- persist a manually-typed transcript ──
    [HttpPost("transcript")]
    public async Task<IActionResult> SaveManualTranscript([FromBody] SaveTranscriptRequest req, CancellationToken ct)
    {
        if (req.VisitId is int visitId)
            await TryPersistTranscriptionAsync(visitId, req.TranscriptText, "Manual", null, ct);
        return Ok();
    }

    private async Task TryPersistTranscriptionAsync(int visitId, string text, string source, int? durationMs, CancellationToken ct)
    {
        try
        {
            await _persistence.SaveTranscriptionAsync(visitId, text, source, ct);
            await _persistence.LogAuditAsync(visitId, null, null, "Transcription", "Success", durationMs, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist transcription for visit {VisitId}", visitId);
        }
    }

    private async Task TryLogAuditAsync(int visitId, string stepName, string status, int? durationMs, CancellationToken ct)
    {
        try { await _persistence.LogAuditAsync(visitId, null, null, stepName, status, durationMs, null, ct); }
        catch { /* audit logging must never mask the original failure */ }
    }

    // ── POST /api/generate-soap ─────────────────────────────
    [HttpPost("generate-soap")]
    public async Task<IActionResult> GenerateSoap(
        [FromBody] GenerateSoapRequest req, CancellationToken ct)
    {
        var result = await _soapSvc.GenerateAsync(req, ct);
        return Ok(result);
    }

    // ── POST /api/validate ──────────────────────────────────
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateDocumentRequest req, CancellationToken ct)
    {
        var result = await _validSvc.ValidateAsync(req, ct);
        return Ok(result);
    }

    // ── POST /api/patient-summary ───────────────────────────
    [HttpPost("patient-summary")]
    public async Task<IActionResult> PatientSummary(
        [FromBody] PatientSummaryRequest req, CancellationToken ct)
    {
        var result = await _summarySvc.SummariseAsync(req, ct);
        return Ok(result);
    }

    // ── GET /api/regulatory ─────────────────────────────────
    [HttpGet("regulatory")]
    public IActionResult RegulatoryUpdates()
    {
        return Ok(_regSvc.GetUpdates());
    }
}
