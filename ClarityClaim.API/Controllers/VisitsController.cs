using ClarityClaim.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClarityClaim.API.Controllers;

// Full pipeline history + audit trail for one visit -- transcript, SOAP note
// with codes, validation result with gaps/fixes, and every logged audit step.
[ApiController]
[Route("api/visits")]
public class VisitsController : ControllerBase
{
    private readonly IVisitRepository _visits;
    private readonly IPipelinePersistenceRepository _persistence;

    public VisitsController(IVisitRepository visits, IPipelinePersistenceRepository persistence)
    {
        _visits = visits;
        _persistence = persistence;
    }

    [HttpGet("{visitId:int}/history")]
    public async Task<IActionResult> GetHistory(int visitId, CancellationToken ct)
    {
        var history = await _visits.GetHistoryAsync(visitId, ct);
        return Ok(history);
    }

    // ── POST /api/visits/{id}/complete -- reviewer marks the review as done ──
    [HttpPost("{visitId:int}/complete")]
    public async Task<IActionResult> Complete(int visitId, CancellationToken ct)
    {
        await _visits.CompleteAsync(visitId, ct);

        try { await _persistence.LogAuditAsync(visitId, null, null, "ReviewCompleted", "Success", null, null, ct); }
        catch { /* audit logging must never block the completion itself */ }

        return NoContent();
    }
}
