// hooks/useClinicalRecord.js
import { useState } from 'react'

export function useClinicalRecord() {
  const [record, setRecord] = useState({
    visitId: null,
    patientId: null, // SQL PatientId (int)
    patient: null, // FhirPatientContext-shaped: fullName, dateOfBirth, gender, conditions, medications, allergies
    transcript: '',
    soapNote: null,
    soapNoteId: null,
    validation: null,
    patientSummary: '',
    fromFallback: false,
  })

  const updateRecord = (patch) => setRecord((prev) => ({ ...prev, ...patch }))

  const resetRecord = () =>
    setRecord({
      visitId: null,
      patientId: null,
      patient: null,
      transcript: '',
      soapNote: null,
      soapNoteId: null,
      validation: null,
      patientSummary: '',
      fromFallback: false,
    })

  return { record, updateRecord, resetRecord }
}
