using ClarityClaim.Domain.Enums;
using ClarityClaim.Domain.Models;
using FluentAssertions;
using Xunit;

namespace ClarityClaim.Tests.Domain;

// DOM-01, DOM-02, DOM-03
public class DomainTests
{
    [Fact]
    public void ClinicalRecord_DefaultConstructs_WithNonNullNestedObjects()
    {
        var record = new ClinicalRecord();

        record.Patient.Should().NotBeNull();
        record.SoapNote.Should().NotBeNull();
        record.Validation.Should().NotBeNull();
        record.Transcript.Should().BeEmpty();
        record.PatientSummary.Should().BeEmpty();
    }

    [Fact]
    public void ValidationSeverity_OrdersCriticalHighest()
    {
        ((int)ValidationSeverity.Critical).Should().BeGreaterThan((int)ValidationSeverity.Major);
        ((int)ValidationSeverity.Major).Should().BeGreaterThan((int)ValidationSeverity.Minor);
    }

    [Fact]
    public void ModelInstance_HasNoPhi2Member()
    {
        var names = Enum.GetNames<ModelInstance>();
        names.Should().NotContain(n => n.Contains("Phi2", StringComparison.OrdinalIgnoreCase)
                                        || n.Contains("Phi_2", StringComparison.OrdinalIgnoreCase));
    }
}
