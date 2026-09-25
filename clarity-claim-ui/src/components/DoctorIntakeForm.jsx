import { useState } from 'react'
import { api } from '../services/api'
import { validateFullName, validateEmail, validateSpecialty, validateNpi } from '../utils/doctorValidation'

const VALIDATORS = {
  fullName: validateFullName,
  email: validateEmail,
  specialty: validateSpecialty,
  npiNumber: validateNpi,
}

// One-time intake form -- captures and stores the doctor's profile in
// ClarityClaimDb (dbo.Doctors) before the app is used. After this, "logged in"
// just means this profile is cached client-side and shown top-right.
export function DoctorIntakeForm({ onComplete }) {
  const [form, setForm] = useState({ fullName: '', email: '', specialty: '', npiNumber: '' })
  const [touched, setTouched] = useState({})
  const [serverError, setServerError] = useState('')
  const [saving, setSaving] = useState(false)

  const errors = {
    fullName: VALIDATORS.fullName(form.fullName),
    email: VALIDATORS.email(form.email),
    specialty: VALIDATORS.specialty(form.specialty),
    npiNumber: VALIDATORS.npiNumber(form.npiNumber),
  }
  const isValid = Object.values(errors).every((e) => !e)

  const handleChange = (field) => (e) => setForm((prev) => ({ ...prev, [field]: e.target.value }))
  const handleBlur = (field) => () => setTouched((prev) => ({ ...prev, [field]: true }))

  const handleSubmit = async (e) => {
    e.preventDefault()
    setTouched({ fullName: true, email: true, specialty: true, npiNumber: true })
    if (!isValid) return

    setSaving(true)
    setServerError('')
    try {
      const profile = await api.upsertDoctor(form)
      onComplete(profile)
    } catch (err) {
      setServerError('Could not save your profile. Check that the API and database are running.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-[var(--hc-bg)] px-4">
      <div className="w-full max-w-md bg-[var(--hc-surface)] border border-[var(--hc-border)] rounded-2xl shadow-sm p-8">
        <div className="flex items-center gap-2 mb-1">
          <div className="w-9 h-9 rounded-lg bg-[var(--hc-primary)] flex items-center justify-center text-white font-bold text-lg">
            C
          </div>
          <div className="text-xl font-semibold text-[var(--hc-text)]">ClarityClaim</div>
        </div>
        <p className="text-sm text-[var(--hc-text-muted)] mb-6">
          Welcome — tell us who's using ClarityClaim today.
        </p>

        <form onSubmit={handleSubmit} noValidate className="space-y-4">
          <Field label="Full name" required error={touched.fullName && errors.fullName}>
            <input
              type="text"
              value={form.fullName}
              onChange={handleChange('fullName')}
              onBlur={handleBlur('fullName')}
              placeholder="Jane Patel"
              className="hc-input"
              autoFocus
            />
          </Field>
          <Field label="Email" required error={touched.email && errors.email}>
            <input
              type="email"
              value={form.email}
              onChange={handleChange('email')}
              onBlur={handleBlur('email')}
              placeholder="jane.patel@clinic.com"
              className="hc-input"
            />
          </Field>
          <Field label="Specialty" error={touched.specialty && errors.specialty}>
            <input
              type="text"
              value={form.specialty}
              onChange={handleChange('specialty')}
              onBlur={handleBlur('specialty')}
              placeholder="Internal Medicine"
              className="hc-input"
            />
          </Field>
          <Field label="NPI number" error={touched.npiNumber && errors.npiNumber}>
            <input
              type="text"
              inputMode="numeric"
              value={form.npiNumber}
              onChange={handleChange('npiNumber')}
              onBlur={handleBlur('npiNumber')}
              placeholder="1234567893"
              className="hc-input"
              maxLength={10}
            />
          </Field>

          {serverError && <p className="text-red-500 text-sm">{serverError}</p>}

          <button
            type="submit"
            disabled={saving || (Object.keys(touched).length > 0 && !isValid)}
            className="w-full bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white font-medium py-2.5 rounded-lg transition disabled:opacity-50"
          >
            {saving ? 'Saving...' : 'Continue'}
          </button>
        </form>
      </div>
    </div>
  )
}

function Field({ label, required, error, children }) {
  return (
    <label className="block">
      <span className="block text-sm font-medium text-[var(--hc-text)] mb-1">
        {label} {required && <span className="text-red-500">*</span>}
      </span>
      {children}
      {error && <span className="block text-xs text-red-500 mt-1">{error}</span>}
    </label>
  )
}
