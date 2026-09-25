import { useState } from 'react'
import { api } from '../services/api'

export function PatientSummary({ soapNote }) {
  const [summary, setSummary] = useState('')
  const [loading, setLoading] = useState(false)

  const handleGenerate = async () => {
    setLoading(true)
    try {
      const result = await api.getPatientSummary({ soapNote, language: 'en' })
      setSummary(result.summaryText)
    } catch {
      setSummary('Unable to generate a summary right now. Please try again shortly.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="hc-card p-5">
      <div className="text-xs uppercase tracking-wider text-[var(--hc-primary)] font-semibold mb-3">
        Patient-Friendly Summary
      </div>

      {loading ? (
        <p className="text-sm text-[var(--hc-text-muted)]">Generating summary...</p>
      ) : summary ? (
        <p className="text-sm text-[var(--hc-text)] leading-relaxed">{summary}</p>
      ) : (
        <button onClick={handleGenerate} className="text-sm text-[var(--hc-primary)] hover:underline">
          Generate summary
        </button>
      )}
    </div>
  )
}
