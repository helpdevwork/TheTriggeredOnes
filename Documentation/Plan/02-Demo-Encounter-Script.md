# ClarityClaim — Demo Encounter Script v1.0

A two-voice doctor/patient script for recording the live demo audio clip (or for
reading straight into the "type / paste transcript" box on Screen 1). Matches the
primary demo scenario used throughout the build: low back pain with radiculopathy,
evaluated against **LCD L34220** (lumbar MRI), including the **4-week** conservative
treatment requirement.

Read time is roughly 45–60 seconds at a natural pace — matching the Development
Plan's target demo clip length (§Phase 0).

---

## Speakers

- **DR** — Dr. Patel (clinician). Speak at a normal, even pace, slightly lower pitch.
- **PT** — Margaret Chen (patient, the demo's cached FHIR patient, `demo-001`).
  Speak a bit higher-pitched and slower, with mild discomfort in tone.

If recording with one person switching voices, pause briefly between speaker
changes so Whisper's segment boundaries stay clean.

---

## Script

**DR:** Good morning, Margaret. What brings you in today?

**PT:** Hi, Dr. Patel. It's my lower back — it's been about six weeks now.
The pain shoots down my left leg, all the way to my calf.

**DR:** Okay. And has that gotten better, worse, or stayed about the same over
that time?

**PT:** Honestly, a bit worse. It's hard to sit for long, and bending forward
makes it much sharper.

**DR:** Have you tried anything for it — physical therapy, medication, anything
like that?

**PT:** Yes, I've been doing physical therapy for about four weeks now, twice a
week, and I've been taking ibuprofen regularly. It hasn't really helped much.

**DR:** Understood. Let me examine you. I'm going to lift your leg straight up —
tell me if this reproduces the pain. [pause] Okay, that's positive at about
forty degrees on the left side. I'm also checking sensation here on the outside
of your lower leg and top of your foot — do you feel this?

**PT:** That side feels a little numb, actually, compared to the other one.

**DR:** That's consistent with what we call radiculopathy — a nerve in your
lower back is being irritated, most likely from a disc problem at one of the
lower lumbar levels. Your reflexes look normal and you don't have any
weakness, which is reassuring.

**PT:** Is that serious? What do we do next?

**DR:** Given that you've already completed four weeks of physical therapy and
anti-inflammatory medication without real improvement, and given the nerve
findings on exam, I think it's time to get an MRI of your lumbar spine without
contrast. That will let us see exactly what's compressing the nerve and plan
treatment from there.

**PT:** Okay, that makes sense. Should I keep taking the ibuprofen in the
meantime?

**DR:** Yes, continue that as needed. I'll put the order in for the MRI today,
and let's plan to follow up in two weeks once we have the results.

**PT:** Sounds good, thank you, Doctor.

**DR:** You're welcome, Margaret. We'll get this sorted out.

---

## How this maps to the pipeline

| Script detail | Where it lands |
|---|---|
| "six weeks" back pain, left leg radiation to calf | `SoapNote.Subjective` |
| Positive straight leg raise at ~40°, sensory loss, normal reflexes/strength | `SoapNote.Objective` |
| "radiculopathy... disc problem" | `SoapNote.Assessment`, drives `M51.16` over unspecified `M54.5` |
| "four weeks" of PT + NSAIDs without improvement | Satisfies **LCD L34220**'s 4-week conservative-treatment rule — this is the line the Validation screen checks for |
| "MRI of your lumbar spine without contrast" | `SoapNote.Plan`, drives CPT `72148` |

## Using it

1. Open Screen 1 (Encounter Capture) with the API and UI running.
2. Either:
   - **Record live**: read both parts aloud (switching voice/pacing per speaker)
     into the "Record Encounter" button, or
   - **Paste directly**: copy the spoken lines (without the `DR:`/`PT:` labels)
     into the manual transcript text box.
3. Click **Generate Clinical Note** and proceed through the flow as normal.

Note: the script already mentions four weeks of PT/NSAIDs, so a live model may
score this fairly high on the first pass. For the clearest "before/after" gap
demo, either trim that line out of what you read/paste (so conservative
treatment is undocumented on the first validation pass), or rely on the
pre-generated fallback flow, which is written to show the classic 74% → 93%
jump regardless of what live models return (see `ValidationService`'s
pre-fix/post-fix fallback selection in `ClarityClaim.Services`).
