\# Project editing rules



\- Treat all source files as UTF-8.

\- Preserve all Chinese comments and strings exactly.

\- Never rewrite an entire file unless explicitly requested.

\- Never guess fixes for garbled text.

\- If encoding looks uncertain, stop and ask.

\- Only make minimal diffs.

\- Modify one file at a time.

\- Do not refactor unrelated code.

\- Do not reformat entire files.

\- Show the plan and target files before editing.

\- For new features, new data structures, and new config fields, do not add legacy-data fallback, legacy-logic fallback, or hidden compatibility fallback unless the user explicitly asks for it.

\- Treat editor-exposed fields and current config values as the only source of truth; if new logic is not wired correctly, surface the problem instead of silently falling back to old paths.

