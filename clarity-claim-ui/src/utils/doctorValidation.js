// Mirrors the server-side validation in ClarityClaim.Domain.DTOs.DoctorUpsertRequest
// and ClarityClaim.Domain.Validation.NpiCheckDigitAttribute -- client-side checks are
// for immediate feedback; the API re-validates everything server-side regardless.

const NAME_REGEX = /^[A-Za-z ]+$/
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const NPI_PREFIX = '80840'

export function validateFullName(value) {
  if (!value.trim()) return 'Full name is required.'
  if (!NAME_REGEX.test(value)) return 'Full name may only contain letters and spaces.'
  return ''
}

export function validateEmail(value) {
  if (!value.trim()) return 'Email is required.'
  if (!EMAIL_REGEX.test(value)) return 'Enter a valid email address.'
  return ''
}

export function validateSpecialty(value) {
  if (value && !NAME_REGEX.test(value)) return 'Specialty may only contain letters and spaces.'
  return ''
}

// Luhn check digit over "80840" + the NPI's first 9 digits must equal the 10th digit.
function npiCheckDigit(payload) {
  const digits = payload.split('').reverse().map(Number)
  let sum = 0
  digits.forEach((d, i) => {
    if (i % 2 === 0) {
      d *= 2
      if (d > 9) d -= 9
    }
    sum += d
  })
  return (10 - (sum % 10)) % 10
}

export function validateNpi(value) {
  if (!value) return '' // optional
  if (!/^\d{10}$/.test(value)) return 'NPI number must be exactly 10 digits.'
  const expected = npiCheckDigit(NPI_PREFIX + value.slice(0, 9))
  if (expected !== Number(value[9])) return 'That NPI number is not valid (failed checksum).'
  return ''
}
