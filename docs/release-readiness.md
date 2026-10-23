# Remito release readiness

This document is the release gate and operational runbook for the Android app and API. A checked-in workflow or checklist is not evidence that a production service, Play listing, monitoring alert, or device test is configured. Keep the release blocked until each required item has a named owner and attached evidence.

## Current repository state

| Area | Current state | Release evidence still required |
|---|---|---|
| Android bundle | The `v*` GitHub Actions workflow builds a signed `prodRelease` AAB from repository secrets. Version name comes from the tag; version code comes from the workflow run number. | Successful tagged pipeline, Play App Signing enrollment, and confirmation of the artifact's signing certificate. |
| R8 mapping | The tagged workflow retains `mapping.txt` beside the AAB as a 90-day artifact. | Upload the mapping to the configured crash-reporting service for the matching release and verify symbolication. |
| Crash reporting | Firebase Messaging is present; no Android crash-reporting SDK or upload task is configured in this repository. | Select/configure the crash-reporting provider, validate a test crash and dashboard, then block release on a green signal. |
| Performance | The API has Redis-backed latest-rate reads, ETags/cache headers, database indexes, and the k6 script exercises three concurrency levels. HTTPS response compression is limited to public rate endpoints. No Baseline Profile or startup benchmark harness is configured. | Run against a representative staging deployment and attach raw k6 output, environment/device details, and query plans. Measure a Baseline Profile/startup trace on a named device. No result is assumed here. |
| Accessibility and localization | The app contains localized resources, Compose semantics, and stable keys on current lazy lists. | Complete device checks at 200% font scale, TalkBack, contrast, reduced motion, pseudo-locale, and RTL; attach screenshots and checklist. |
| Store and legal | The app build accepts privacy/terms URL and Firebase configuration through Gradle properties/environment. | Real legal URLs, listing/media, Data Safety answers, declarations, and compliance approval are not present in the repository. |
| Deployment | The workflow creates an AAB artifact but does not publish it to Google Play. The backend rollback workflow swaps Azure slots. | Play Console access, release service account, approved cohort, dashboards/alerts, on-call contacts, and signed go/no-go approvals. |

## Performance gate

Run the load profile only against an authorized staging or production-like deployment with representative data and capacity. It ramps through 100, 1,000, and 5,000 virtual users. `API_BASE_URL` must be set to the target API root, for example `https://staging.example/api/v1`.

```sh
k6 run backend/scripts/rates-cache-load.js
```

Pass criteria:

- P95 `http_req_duration` below 400 ms.
- Failed HTTP request rate below 0.5%.
- Successful checks above 99.5%, including response status and latest-rate cache validators.
- Record the k6 version, commit, target environment, database/Redis tier, run timestamp, and complete output.
- Capture representative database query plans for latest-rate/history hot queries and verify the intended indexes are selected. Index definitions alone do not satisfy this check.
- Measure Android cold start on a named mid-range physical device. The target is below 2.5 seconds; attach the startup trace and device/build details. No baseline-profile claim is valid until measured.

Do not run the 5,000-user profile against production without an approved change window, capacity confirmation, and on-call coverage.

## Accessibility and localization gate

Test release builds on physical devices with TalkBack enabled and disabled. Record pass/fail, device/OS, build, tester, and issue links for each check.

- [ ] All interactive controls have meaningful TalkBack labels; decorative graphics are excluded from focus.
- [ ] Focus order follows visual and task order and does not trap focus in dialogs or navigation.
- [ ] Interactive targets are at least 48 dp and controls remain usable when enlarged.
- [ ] Text and meaningful graphics meet WCAG AA contrast; status is not conveyed by colour alone.
- [ ] At 200% font scale, content remains readable, controls do not overlap, and primary actions remain reachable.
- [ ] Reduced-motion/system animation settings do not hide information or block task completion.
- [ ] All user-facing copy is from Android string resources; plural and parameterized text use locale-aware resources/formatters.
- [ ] Verify number/date/currency formatting for `en-IN` and `en-OM`.
- [ ] Run pseudo-locale `en-XA`; capture screenshots of every primary screen and important dialog.
- [ ] Run an RTL locale (for Phase 2, Arabic); capture screenshots and verify layout direction, icons, charts, navigation, and text alignment.

Attach screenshots and the completed checklist to the release PR. Emulator-only screenshots do not replace TalkBack and physical-device checks.

## Store listing and compliance gate

The public listing and in-app disclosures must say: **“comparison and referral only, Remito does not process payments”**. Describe transactions as completed through the selected exchange's own app or gateway. Do not imply that Remito holds, transfers, or guarantees funds or exchange rates.

- [ ] Confirm final title, short description, full description, localized copy, screenshots, and feature graphic.
- [ ] Confirm Data Safety answers against actual SDKs, event payloads, retention, sharing, deletion, and account behavior.
- [ ] Publish and verify the privacy policy and terms URLs; configure those exact HTTPS URLs in the production build.
- [ ] Complete content rating and target-audience declarations.
- [ ] Complete the Play Console financial-features declaration with compliance review; do not infer the answer from marketing copy.
- [ ] Enroll in Play App Signing and verify the upload key/certificate handling.
- [ ] Verify the signed AAB, version name/code, R8 mapping, and crash-reporting symbolication before beta.

## Closed beta and staged rollout

Before enabling a track, name the cohort owner and define the cohort in Play Console (eligible accounts, size, geography, invitation/feedback channel, and start/end dates). Do not use a public or unspecified cohort as the closed beta.

Beta exit criteria:

- Crash-free sessions greater than 99.5% over a representative observation period.
- No open critical or high severity defects.
- Rate accuracy audit passes against the approved independent source and tolerance.
- Redirect success greater than 98%; define success from the server-side redirect outcome, not button taps.
- QA and business approve the sample size, observation window, and evidence for all metrics.

Rollout sequence: 5%, 20%, 50%, then 100%. At each step, hold for the approved observation window and review crash-free sessions, redirect success, API P95/error rate, rate freshness, and support reports before advancing.

Automatic halt/rollback is a release requirement, not currently wired to Play Console in this repository. Before rollout, configure and test alerts/actions that halt promotion when crash-free sessions fall below 99.5% or redirect success falls below 98%. Record alert thresholds, minimum sample/window, Play track access, action owner, and a successful halt drill. Do not advance while any signal or halt action is unverified.

## Operational runbook

### On-call and escalation

Populate and verify these contacts before beta; keep a copy in the production on-call system:

| Role | Name/team | Contact | Backup |
|---|---|---|---|
| Release commander | TBD | TBD | TBD |
| Android on-call | TBD | TBD | TBD |
| API/data on-call | TBD | TBD | TBD |
| Rates/compliance owner | TBD | TBD | TBD |
| Customer support lead | TBD | TBD | TBD |

### Halt and rollback

1. Stop promotion in Play Console immediately; record the track, percentage, time, and triggering dashboard evidence.
2. Disable or roll back the affected Android release using the Play Console release controls; communicate that already-installed clients may remain on that version.
3. For an API regression, use the approved Azure slot rollback workflow (`.github/workflows/rollback.yml`) for the affected environment and verify `/health` plus key API flows.
4. If the rollback workflow is unavailable, the Azure on-call swaps the previous healthy slot back using the approved deployment procedure. Never run a production slot swap without the environment's change approval.
5. Confirm latest rates, stale indication, and referral redirects; preserve logs/trace IDs without collecting or sharing personal redirect data.
6. Notify support and stakeholders, open an incident, and keep rollout halted until the release commander, QA, and service owners approve recovery.

### Rate-feed outage

1. Confirm provider failure and the last successful fetch with the rates on-call; do not present an unverified rate as current.
2. Enable approved, pair-scoped manual overrides with source, reason, expiry, and a second-person review.
3. Verify the API and app mark carried-forward/override data stale as appropriate and visibly show the stale banner.
4. Monitor freshness and accuracy; remove the override only after the provider feed is validated and a fresh ingestion succeeds.
5. Escalate if the override expires or no trustworthy quote is available; prefer an unavailable state over a fabricated/current-looking rate.

### Support macros

- **How do I send money?** “Remito is for comparison and referral only; Remito does not process payments. Choose an exchange to continue in that exchange's app or payment gateway. Please contact the exchange for a transfer or payment issue.”
- **Why is this rate different or stale?** “Rates are supplied by exchange/market data sources and can change. Remito displays the source update status; a stale banner means the latest refresh could not be confirmed. Check the chosen exchange for its current quote before proceeding.”
- **Where is my transfer?** “Remito does not receive or process transfers. Please contact the exchange you selected using its official support channel. Do not send us passwords, OTPs, card details, or full payment credentials.”
- **How do referrals work?** “Remito may refer you to an exchange. The exchange's own terms, fees, eligibility, and final quote apply.”

## Go/no-go approvals

Do not declare release ready until every section above has attached evidence and these roles have signed the same release candidate:

| Role | Approver | Decision (Go/No-go) | Date and evidence |
|---|---|---|---|
| Development | TBD | Pending | TBD |
| QA | TBD | Pending | TBD |
| Compliance | TBD | Pending | TBD |
| Business | TBD | Pending | TBD |

After reaching 100%, keep crash, redirect, rate freshness, API latency/error, and support dashboards green for 72 hours before closing the release.
