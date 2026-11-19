# Karma port: remaining work

The port implements the agreed Infosys runtime rules and excludes non-time-exclusive preferences and assignments for [#26](https://github.com/Alea-Fastaval/Fastasys/issues/26). See [Karma and activity planning](karma.md) for behavior, compatibility decisions, APIs, migrations, and verification.

Progress for [#16](https://github.com/Alea-Fastaval/Fastasys/issues/16):

- [x] Pure calculator with threshold, cap, and exclusion regressions.
- [x] Ranked preferences, admin CRUD, and data-preserving migration.
- [x] Current/batched karma service, participant page, preference editor, and run/activity integration.
- [x] Occurrence cancellation/restoration and individual refresh.
- [ ] System-wide overview.
- [ ] Complete workflow verification including the overview.

## 5. System-wide overview

Add a searchable, sortable participant karma list with links to individual breakdowns. Show participant count, average, minimum, and maximum. Define the statistics population explicitly, distinguish participants with eligible inputs from those without, and handle an empty population. The existing participant endpoint's 100-row limit must not silently truncate the list or statistics; use server-side filtering/paging where needed.

Provide Danish and English labels and explain the overview in the planner guide. Extend the technical guide for its API and batching. Karma remains an input to planning, without introducing automatic allocation or new tie-breaking rules.

Done when a planner can find a participant and explain their score, statistics include the intended population, and documented examples agree with executable fixtures.

## 6. Complete workflow verification

Run the root README's backend/frontend checks, including migration drift and disposable PostgreSQL tests. Exercise ranked preferences, player/GM seat changes, nested schedule totals, time-exclusivity changes, cancellation/restoration, individual refresh, and the overview in both languages.

Compare synthetic time-exclusive fixtures with the pinned Infosys runtime rules and verify the deliberate #26 differences separately. Production data imports, automatic team allocation, and the full public signup flow remain separate work. The changes are in Fastasys; deploying an additional fix to Infosys is separate work too.
