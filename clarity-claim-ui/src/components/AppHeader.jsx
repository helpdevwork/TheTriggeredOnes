import { useState } from 'react'

function initials(name) {
  if (!name) return '?'
  return name
    .split(' ')
    .map((p) => p[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
}

export function AppHeader({ doctor, onSwitchDoctor, theme, onToggleTheme, search, onSearchChange, showSearch }) {
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <header className="sticky top-0 z-20 bg-[var(--hc-surface)] border-b border-[var(--hc-border)] px-6 py-3 flex items-center gap-4">
      <div className="flex items-center gap-2 shrink-0">
        <div className="w-8 h-8 rounded-lg bg-[var(--hc-primary)] flex items-center justify-center text-white font-bold">
          C
        </div>
        <span className="text-lg font-semibold text-[var(--hc-text)] hidden sm:inline">ClarityClaim</span>
      </div>

      <div className="flex-1" />

      {showSearch && (
        <div className="relative w-full max-w-sm">
          <input
            type="text"
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="Search patients, MRN, payor..."
            className="hc-input pl-9"
          />
          <svg
            className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--hc-text-muted)]"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-4.35-4.35M17 10.5a6.5 6.5 0 11-13 0 6.5 6.5 0 0113 0z" />
          </svg>
        </div>
      )}

      <button
        onClick={onToggleTheme}
        title="Toggle light / dark mode"
        className="w-9 h-9 rounded-lg border border-[var(--hc-border)] flex items-center justify-center text-[var(--hc-text-muted)] hover:bg-[var(--hc-surface-2)] shrink-0"
      >
        {theme === 'dark' ? (
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.36 6.36l-.7-.7M6.34 6.34l-.7-.7m12.72 0l-.7.7M6.34 17.66l-.7.7M16 12a4 4 0 11-8 0 4 4 0 018 0z" />
          </svg>
        ) : (
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
          </svg>
        )}
      </button>

      {doctor && (
        <div className="relative shrink-0">
          <button
            onClick={() => onSwitchDoctor && setMenuOpen((o) => !o)}
            className={`flex items-center gap-2 rounded-lg pl-1 pr-2 py-1 ${onSwitchDoctor ? 'hover:bg-[var(--hc-surface-2)]' : 'cursor-default'}`}
          >
            <div className="w-8 h-8 rounded-full bg-[var(--hc-accent)] flex items-center justify-center text-white text-xs font-semibold">
              {initials(doctor.fullName)}
            </div>
            <div className="text-left hidden md:block">
              <div className="text-sm font-medium text-[var(--hc-text)] leading-tight">{doctor.fullName}</div>
              <div className="text-xs text-[var(--hc-text-muted)] leading-tight">{doctor.specialty || 'Physician'}</div>
            </div>
          </button>

          {menuOpen && onSwitchDoctor && (
            <div className="absolute right-0 mt-2 w-48 hc-card shadow-lg py-1 text-sm">
              {doctor.email && (
                <div className="px-3 py-2 text-xs text-[var(--hc-text-muted)] border-b border-[var(--hc-border)]">
                  {doctor.email}
                </div>
              )}
              <button
                onClick={() => {
                  setMenuOpen(false)
                  onSwitchDoctor()
                }}
                className="w-full text-left px-3 py-2 hover:bg-[var(--hc-surface-2)] text-[var(--hc-text)]"
              >
                Switch profile
              </button>
            </div>
          )}
        </div>
      )}
    </header>
  )
}
