// services/api.js -- single source of truth for all HTTP calls
const BASE = 'http://localhost:8000/api'

async function jsonFetch(url, options) {
  const r = await fetch(url, options)
  if (!r.ok) throw new Error(`Request failed (${r.status}): ${url}`)
  if (r.status === 204) return null
  return r.json()
}

const postJson = (path, payload) =>
  jsonFetch(`${BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })

export const api = {
  // ── Doctor profile ──────────────────────────────────────
  async upsertDoctor(payload) {
    return postJson('/doctor', payload)
  },
  async getDoctor(doctorId) {
    return jsonFetch(`${BASE}/doctor/${doctorId}`)
  },

  // ── Patient dashboard ───────────────────────────────────
  async listPatients() {
    return jsonFetch(`${BASE}/patients`)
  },
  async searchPatients(query) {
    return jsonFetch(`${BASE}/patients/search?q=${encodeURIComponent(query)}`)
  },
  async getPatientDetail(patientId) {
    return jsonFetch(`${BASE}/patients/${patientId}`)
  },
  async createVisit(patientId, doctorId) {
    return postJson(`/patients/${patientId}/visits`, { doctorId })
  },

  // ── Visit history / audit ───────────────────────────────
  async getVisitHistory(visitId) {
    return jsonFetch(`${BASE}/visits/${visitId}/history`)
  },
  async completeVisit(visitId) {
    return jsonFetch(`${BASE}/visits/${visitId}/complete`, { method: 'POST' })
  },

  // ── Review team hand-off ─────────────────────────────────
  async listReviewTeam() {
    return jsonFetch(`${BASE}/review-team`)
  },
  async updateSoapNote(soapNoteId, soapNote) {
    return jsonFetch(`${BASE}/soap-notes/${soapNoteId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ soapNote }),
    })
  },
  async assignReviewer(visitId, reviewTeamMemberId) {
    return jsonFetch(`${BASE}/visits/${visitId}/assign-reviewer`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reviewTeamMemberId }),
    })
  },
  async getReviewContext(visitId) {
    return jsonFetch(`${BASE}/visits/${visitId}/review`)
  },

  // ── Legacy FHIR-cache patient lookup (kept for the demo patient) ────────
  async getPatient(patientId) {
    return jsonFetch(`${BASE}/patient/${patientId}`)
  },

  // ── Pipeline ─────────────────────────────────────────────
  async transcribe(audioFile, visitId) {
    const form = new FormData()
    form.append('audio', audioFile, 'encounter.wav')
    const url = visitId ? `${BASE}/transcribe?visitId=${visitId}` : `${BASE}/transcribe`
    const r = await fetch(url, { method: 'POST', body: form })
    if (!r.ok) throw new Error('Transcription failed')
    return r.json()
  },
  async saveManualTranscript(visitId, transcriptText) {
    return postJson('/transcript', { visitId, transcriptText })
  },
  async generateSoap(payload) {
    return postJson('/generate-soap', payload)
  },
  async validate(payload) {
    return postJson('/validate', payload)
  },
  async getPatientSummary(payload) {
    return postJson('/patient-summary', payload)
  },
  async getRegulatoryUpdates() {
    return jsonFetch(`${BASE}/regulatory`)
  },
}
