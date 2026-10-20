## Summary

<!-- What changed and why? -->

## Verification

- [ ] `dotnet build backend/Remito.sln`
- [ ] `dotnet test backend/Remito.sln`
- [ ] `./android/gradlew -p android assembleDebug testDebugUnitTest`
- [ ] `./android/gradlew -p android ktlintCheck detekt`

## Product and security checks

- [ ] Remito does not process or move customer money
- [ ] Fees and amount received are shown only when supplied by the exchange
- [ ] Sponsored listings are labelled and do not change Best rate
- [ ] Rate screens show last-updated time and stale status
- [ ] No PII in logs, analytics, or redirect URLs

## Assumptions and open questions

<!-- Record assumptions or write "None". -->