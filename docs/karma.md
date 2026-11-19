# Karma and activity planning

Fastasys calculates karma from a participant's activity preferences and assigned run seats. A preference expresses interest in a scheduled occurrence; a seat is an actual assignment. Editing either does not change the other. Totals are calculated on every read, with no stored total or cache.

## Planner guide

Open a participant's karma page from the participant list or their name in an activity/run view. It shows their total, grouped contributions, preferences, and explanations for excluded inputs. Activity viewers can inspect and refresh it; activity editors and Admins can also manage preferences.

Select an activity, occurrence, role, and priority to add a preference. Player priorities are 1–4. Use priority 0 for a new game-master preference; existing GM priorities 1–4 are supported too. Existing player preferences without a priority remain visibly unranked until explicitly corrected. Unknown legacy roles also require explicit correction. Preferences are separate from public signup submissions, which are not yet connected to these records.

Only active, time-exclusive occurrences contribute to karma. Drop-in/non-time-exclusive activities are excluded from **both preferences and assignments**, including GM preferences. This fixes [#26](https://github.com/Alea-Fastaval/Fastasys/issues/26).

| Input | Contribution |
| --- | --- |
| Game-master preferences | -7 once, regardless of their count or recorded priority |
| First-priority player preferences | Counts 1–8: -12, -20, -26, -29, -32, -34, -35, -36; capped at -36 |
| Second-priority player preferences | -1 per preference, capped at -3 |
| Third-priority player preferences | -1 once |
| Fourth-priority player preferences | 0 |
| Assigned Big player seats | +10 per seat, capped at +100 |
| Assigned Small player seats | +7 per seat, capped at +70 |
| Assigned Medium/None player seats or GM seats | 0 |

Group points include the cap; they are not independent points for every row. Assigning a participant does not remove their preference contribution. Multiple assigned runs of an activity count separately. A participant without contributing inputs has integer karma 0. Karma is an input to planning, not a guarantee of allocation.

For example, one first-priority preference (-12) and one Big player seat (+10) give **-2**. Changing that preference to second priority gives **9**. Adding a non-time-exclusive preference and Small seat changes neither result. Removing the Big seat leaves the preference's contribution. Cancelling its occurrence excludes both records, giving **0** in this example; restoring it restores their contributions.

### Cancellation and refresh

Activity editors and Admins can cancel one occurrence from the activity page. Cancellation retains preferences, runs, and seats, excludes their karma, releases room bookings, and prevents new preferences, runs, or seats. Other occurrences stay active. Existing preferences and seats can still be removed, and preferences can be moved to an active occurrence.

Restoration checks retained rooms against current bookings and other retained runs. A conflict leaves the occurrence cancelled. Review its run rooms or times and try again; records are never reassigned automatically.

**Recalculate karma** reloads the participant's current preferences and breakdown. Repeating it cannot accumulate points. Finish or cancel an open preference edit first. Failed reloads hide the previous result so it is not presented as current. Cancelling/restoring an occurrence reloads all displayed activity runs, since a participant's total may also change in other runs.

## Compatibility and storage

The compatibility target is Infosys's [runtime `Model::buildKarma` rules](https://github.com/Alea-Fastaval/Infosys/blob/b6225c471951bddaf592a8c61fb1bbaea5a446d6/include/framework/model.php#L291), pinned at `b6225c471951bddaf592a8c61fb1bbaea5a446d6`. Its [input queries](https://github.com/Alea-Fastaval/Infosys/blob/b6225c471951bddaf592a8c61fb1bbaea5a446d6/include/framework/Karma.php) distinguish preferences from assignments. Older Infosys tests award +7 to Medium instead of Small; Fastasys preserves the application rules, as agreed for [#16](https://github.com/Alea-Fastaval/Fastasys/issues/16). Score direction, allocation, and tie-breaking rules are unchanged.

`ParticipantSchedule` (`participant_schedules`) is the authoritative preference source: generated integer `Id`, participant/schedule references, string `Role`, nullable integer `Priority`, and UTC `SignedUpAt`. Uniqueness is `(ParticipantId, ScheduleId, Priority)`, with nulls treated as equal, regardless of role. This supports the legacy combined preference: GM priority 0 and player priority 1 on the same occurrence.

Migration `20261119090356_AddRankedActivitySignups` preserves existing roles and timestamps, generates IDs, and leaves old priorities null rather than guessing a rank. Schedule deletion is restricted while preferences exist; the API returns 409. Rollback refuses multiple rows per participant/schedule because the previous key cannot represent them. Preserve/export priorities before rolling back.

Migration `20261119110000_AddScheduleCancellation` adds `IsCancelled` (false) and `CancelledAt` (null). Cancellation records UTC time; restoration clears it. Repeating the same state changes neither that timestamp nor the activity's update time. Rollback refuses while cancelled occurrences exist to avoid silently reactivating retained records without room validation.

For future Infosys imports, map participant/schedule identities, translate `spiller` to `Player` and `spilleder` to `GameMaster`, preserve numeric `prioritet`, and insert combined preferences separately. Do not infer assignments from preferences, invent ranks, merge distinct priorities, or reinterpret priority-0 players as GMs. Production imports and identity mapping remain separate work.

## API

Reads require `ActivityView` (`activities_view`, `activities_edit`, or Admin); writes require `ActivityManagement` (`activities_edit` or Admin). Participant-management access alone does not grant planning access.

| Method | Path | Behavior |
| --- | --- | --- |
| GET | `/api/participants/{participantId}/karma` | Current participant total, grouped contributions, and exclusions; 404 if missing |
| GET | `/api/participants/{participantId}/activity-signups` | Preferences with activity title, occurrence times/state, raw role/priority, timestamp |
| GET | `/api/participants/{participantId}/activity-signups/{id}` | One preference scoped to this participant |
| POST | `/api/participants/{participantId}/activity-signups` | Create; 201 and Location header |
| PUT | `/api/participants/{participantId}/activity-signups/{id}` | Replace schedule/role/priority; preserve ID and timestamp |
| DELETE | `/api/participants/{participantId}/activity-signups/{id}` | Remove this preference only; 204 |
| PUT | `/api/activities/{activityId}/schedules/{scheduleId}/cancellation` | Cancel/restore; return occurrence with retained runs/seats and current karma |

Preference POST/PUT bodies require `{"scheduleId":42,"role":"Player","priority":1}`. An activity must use game masters to accept a GM preference. Invalid fields, unknown schedules, or cancelled destinations return 400. Missing participants/preferences return 404. Duplicate schedule/priority or a referenced record disappearing during save returns 409. Database uniqueness also protects concurrent writes. Hidden/closed-to-public-signup/non-time-exclusive activities may have admin preferences; eligibility is applied separately.

Cancellation bodies require `{"isCancelled":true}` (or false to restore). Missing/null state returns 400, missing/wrong-activity occurrences 404, and room conflicts 409. Transitions are logged with the acting user when available. Cancellation state is included in preference, schedule, run list/detail, and participant schedule responses; cancelled runs do not advertise vacancies.

The karma response contains `participantId`, `participantName`, and:

- `karma.total`: integer sum, including zero and negative values.
- `karma.signups`: effective priority, count, and whole-group points. Priority 0 combines GM preferences (even null priority) and legacy priority-0 players. Unmatched player priorities remain visible with zero points.
- `karma.assignments`: activity karma type, player-seat count, and capped points. Medium/None remain visible with zero points.
- `karma.exclusions`: counts by reason, derived from the same classification as `excludedInputs`.
- `excludedInputs`: source, activity/schedule/run references, raw role/priority, and a named reason.

Each excluded input has one reason: `CancelledSchedule` first, then `NonTimeExclusive`, then `UnknownSignupRole`, `UnrankedPlayerSignup`, or `GameMasterAssignment` as applicable. Only exact stored `Player`/`GameMaster` strings are recognized; numeric strings are not parsed as enums.

### Calculation and concurrency

`KarmaRules` is the pure calculator; `KarmaService` loads and classifies current records. `CalculateAsync` deduplicates participant IDs and performs two input queries per batch, including inputs from other activities/runs. Empty batches make no queries. The individual endpoint adds an identity query. Run detail/mutation and nested activity schedule responses use this same service, batching all visible participants instead of querying per seat.

On PostgreSQL, cancellation and preference/run/seat creation lock the same schedule row in a transaction. Preference updates lock the destination occurrence. Writes waiting behind cancellation see the committed cancelled state. Room allocation, room changes, and restoration lock room rows in ID order before checking active bookings, so concurrent reservations are seen before restoration proceeds.

## Verification and CI

Run the [standard repository checks](../README.md#checks). Calculator tests cover thresholds/caps and #26; service/controller/HTTP tests cover current totals, batching, legacy inputs, cancellation, and permissions. Frontend tests cover editing, read-only access, navigation races, cancellation, and failed reloads. The test setup uses Vitest's jsdom storage because Node 26 also exposes a separate Web Storage global.

The backend **Test** CI job starts a disposable PostgreSQL 18 service. `FASTASYS_TEST_POSTGRES` tells the relational tests where to create independent `fastasys_test_*` databases, removed after each test. These tests verify migration preservation/rollback, SQL translation, null-aware uniqueness, foreign keys, and cancellation/allocation races. EF's in-memory provider cannot verify those behaviors. Other CI jobs do not start PostgreSQL. The service's credentials are disposable test values and do not refer to an application database.

To run the relational tests locally, supply a PostgreSQL 15+ connection with permission to create databases (the project uses PostgreSQL 18). Without the variable they are explicitly skipped:

```sh
docker run --detach --name fastasys-karma-tests --publish 127.0.0.1:55432:5432 \
  --env POSTGRES_USER=fastasys --env POSTGRES_PASSWORD=test-password \
  --env POSTGRES_DB=fastasys_tests postgres:18-alpine

# Once PostgreSQL is ready:
FASTASYS_TEST_POSTGRES='Host=127.0.0.1;Port=55432;Database=fastasys_tests;Username=fastasys;Password=test-password' \
  dotnet test backend/tests/Fastasys.Tests/Fastasys.Tests.csproj --configuration Release

docker rm --force --volumes fastasys-karma-tests
```

This uses no application data volume. The tests create/drop only generated databases, never the configured database itself. The system-wide overview remains [planned follow-up work](karma-port-plan.md).
