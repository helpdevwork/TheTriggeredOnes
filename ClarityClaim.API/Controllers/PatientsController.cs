using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClarityClaim.API.Controllers;

// Backs the landing-page patient dashboard: grid, search, and the expandable
// per-row detail panel (demographics, eligibility window, last visit).
[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientRepository _patients;
    private readonly IVisitRepository _visits;

    public PatientsController(IPatientRepository patients, IVisitRepository visits)
    {
        _patients = patients;
        _visits = visits;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _patients.ListDashboardAsync(ct);
        return Ok(rows);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            var rows = await _patients.ListDashboardAsync(ct);
            return Ok(rows);
        }
        var results = await _patients.SearchAsync(q, ct);
        return Ok(results);
    }

    [HttpGet("{patientId:int}")]
    public async Task<IActionResult> GetDetail(int patientId, CancellationToken ct)
    {
        var detail = await _patients.GetDetailAsync(patientId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    // ── POST /api/patients/{id}/visits -- start a new encounter ────────────
    [HttpPost("{patientId:int}/visits")]
    public async Task<IActionResult> CreateVisit(int patientId, [FromBody] CreateVisitRequest req, CancellationToken ct)
    {
        var visitId = await _visits.CreateAsync(patientId, req.DoctorId, ct);
        return Ok(new { visitId });
    }
}
