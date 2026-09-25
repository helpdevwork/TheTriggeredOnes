using System.ComponentModel.DataAnnotations;

namespace ClarityClaim.Domain.Validation;

// Validates a National Provider Identifier's Luhn check digit per the CMS spec:
// the payload is the constant prefix "80840" + the NPI's first 9 digits, and the
// standard Luhn algorithm applied to that payload must produce the NPI's 10th digit.
// Optional field -- null/empty is valid (NPI wasn't required by the intake form).
public class NpiCheckDigitAttribute : ValidationAttribute
{
    private const string NPI_PREFIX = "80840";

    public NpiCheckDigitAttribute()
        => ErrorMessage = "NPI number must be 10 digits and a valid National Provider Identifier.";

    public override bool IsValid(object? value)
    {
        if (value is not string npi || string.IsNullOrWhiteSpace(npi)) return true; // optional

        if (npi.Length != 10 || !npi.All(char.IsDigit)) return false;

        return ComputeCheckDigit(NPI_PREFIX + npi[..9]) == (npi[9] - '0');
    }

    // Standard Luhn check-digit computation for a payload with the check digit
    // not yet appended: reverse the payload, double every digit at an even index
    // (0-indexed from the right), sum all digits (folding doubled values >9),
    // and the check digit is (10 - (sum mod 10)) mod 10.
    private static int ComputeCheckDigit(string payload)
    {
        var reversed = payload.Reverse().ToArray();
        var sum = 0;

        for (var i = 0; i < reversed.Length; i++)
        {
            var digit = reversed[i] - '0';
            if (i % 2 == 0)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }

        return (10 - (sum % 10)) % 10;
    }
}
