import { useState } from 'react'

const STORAGE_KEY = 'clarityclaim-doctor'

function loadStoredDoctor() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

// "Assume a doctor is always logged in" -- the intake form runs once, the
// profile is then kept client-side (and in ClarityClaimDb) and shown top-right.
export function useDoctor() {
  const [doctor, setDoctorState] = useState(loadStoredDoctor)

  const setDoctor = (profile) => {
    setDoctorState(profile)
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(profile))
    } catch {
      /* ignore -- per-viewer convenience only */
    }
  }

  const clearDoctor = () => {
    setDoctorState(null)
    try {
      localStorage.removeItem(STORAGE_KEY)
    } catch {
      /* ignore */
    }
  }

  return { doctor, setDoctor, clearDoctor }
}
