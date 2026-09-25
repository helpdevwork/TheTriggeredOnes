using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClarityClaim.API.Controllers;

// Backs the UI's one-time doctor intake form. "Logged in" here means a saved
// profile the frontend keeps locally and shows top-right -- not password auth.
[ApiController]
[Route("api/doctor")]
public class DoctorController : ControllerBase
{
    private readonly IDoctorRepository _doctors;

    public DoctorController(IDoctorRepository doctors) => _doctors = doctors;

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] DoctorUpsertRequest req, CancellationToken ct)
    { 
        var profile = await _doctors.UpsertAsync(req.FullName, req.Email, req.Specialty, req.NpiNumber, ct);
        return Ok(profile);
    }

    [HttpGet("{doctorId:int}")]
    public async Task<IActionResult> GetById(int doctorId, CancellationToken ct)
    {
        var profile = await _doctors.GetByIdAsync(doctorId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }
}
