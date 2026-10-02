# Jalali Tray

A small Windows tray app that shows today's Jalali (Shamsi) day on the tray icon. The tooltip shows the Jalali, Gregorian and Hijri dates. A click opens a calendar flyout like time.ir, with events for the selected day.

Download the latest build from [Releases](https://github.com/avestura/JalaliTray/releases/latest).

## Features
- Day number rendered on the tray icon, updated at midnight
- Calendar flyout with Jalali / Gregorian / Hijri dates, live clock and per-day events
- Optional NTP time (`ntp.time.ir`): used by the app only, the Windows clock is not changed
- Events from `api.time.ir` (needs your own `x-api-key`, set in Settings), cached locally, with a bundled offline fallback
- Persian (default) or English UI, light/dark theme, start with Windows, icon font and colors

## Build
Requires the .NET 10 SDK.

```
dotnet run --project src/JalaliTray
```

## Release
Push a tag; GitHub Actions builds and publishes the executables to Releases.

```
git tag v1.0.0
git push origin v1.0.0
```

The landing page in `site/` is deployed to GitHub Pages by `.github/workflows/pages.yml` (enable Pages with source "GitHub Actions" in the repo settings).

Settings and the events cache live in `%AppData%\JalaliTray`.
