using System.Text.Json;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClarityClaim.Infrastructure.Fhir;

// Queries the HAPI FHIR R4 public test server for patient context. Serves from local
// JSON cache (/Data/FHIR/) when offline -- which is always during the demo. Cache is
// populated once before the hackathon (or on first successful live call).
public class FhirService : IFhirService
{
    private readonly HttpClient _http;
    private readonly ILogger<FhirService> _logger;
    private readonly string _cacheDir;

    public FhirService(IHttpClientFactory factory, IConfiguration config, ILogger<FhirService> logger)
    {
        _http = factory.CreateClient("fhir");
        _logger = logger;
        _cacheDir = config["Fhir:CacheDir"] ?? "Data/FHIR";
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<FhirPatientContext> GetPatientContextAsync(
        string patientId, CancellationToken ct = default)
    {
        // Always try cache first -- demo runs offline
        var cachePath = Path.Combine(_cacheDir, $"patient_{patientId}.json");
        if (File.Exists(cachePath))
        {
            var cached = await File.ReadAllTextAsync(cachePath, ct);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<FhirPatientContext>(cached, opts)!;
        }

        // Live call -- fetch, parse, write cache for future use
        try
        {
            using var patient = await FetchFhirResourceAsync($"baseR4/Patient/{patientId}", ct);
            using var conditions = await FetchFhirResourceAsync($"baseR4/Condition?patient={patientId}", ct);
            using var meds = await FetchFhirResourceAsync($"baseR4/MedicationRequest?patient={patientId}", ct);
            using var allergies = await FetchFhirResourceAsync($"baseR4/AllergyIntolerance?patient={patientId}", ct);

            var context = ParseFhirBundle(patient, conditions, meds, allergies);
            await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(context), ct);
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FHIR live call failed, returning empty context");
            return new FhirPatientContext { PatientId = patientId };
        }
    }

    private async Task<JsonDocument> FetchFhirResourceAsync(string relativeUrl, CancellationToken ct)
    {
        var resp = await _http.GetAsync(relativeUrl, ct);
        resp.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private static FhirPatientContext ParseFhirBundle(
        JsonDocument patient, JsonDocument conditions,
        JsonDocument meds, JsonDocument allergies)
    {
        var name = TryGetName(patient.RootElement);
        var dob = TryGetString(patient.RootElement, "birthDate");
        var gender = TryGetString(patient.RootElement, "gender");
        var id = TryGetString(patient.RootElement, "id");

        return new FhirPatientContext
        {
            PatientId = id,
            FullName = name,
            DateOfBirth = dob,
            Gender = gender,
            Conditions = ExtractBundleTextList(conditions.RootElement, "code"),
            Medications = ExtractBundleTextList(meds.RootElement, "medicationCodeableConcept"),
            Allergies = ExtractBundleTextList(allergies.RootElement, "code")
        };
    }

    private static string TryGetName(JsonElement patientRoot)
    {
        if (patientRoot.TryGetProperty("name", out var names) && names.GetArrayLength() > 0)
        {
            var first = names[0];
            if (first.TryGetProperty("text", out var text))
                return text.GetString() ?? "Unknown";
            if (first.TryGetProperty("given", out var given) && first.TryGetProperty("family", out var family))
                return $"{string.Join(' ', given.EnumerateArray().Select(g => g.GetString()))} {family.GetString()}".Trim();
        }
        return "Unknown";
    }

    private static string TryGetString(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var v) ? (v.GetString() ?? "") : "";

    // Extract entry[].resource.<codeProp>.text (or coding[0].display) -- skips malformed entries
    private static List<string> ExtractBundleTextList(JsonElement bundleRoot, string codeProp)
    {
        var results = new List<string>();
        if (!bundleRoot.TryGetProperty("entry", out var entries)) return results;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource)) continue;
            if (!resource.TryGetProperty(codeProp, out var code)) continue;

            if (code.TryGetProperty("text", out var text) && text.GetString() is { Length: > 0 } t)
            {
                results.Add(t);
            }
            else if (code.TryGetProperty("coding", out var coding) && coding.GetArrayLength() > 0
                     && coding[0].TryGetProperty("display", out var display) && display.GetString() is { Length: > 0 } d)
            {
                results.Add(d);
            }
        }
        return results;
    }
}
