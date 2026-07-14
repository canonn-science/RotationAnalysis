# Video Analysis Lab

Video Analysis Lab is a Windows desktop app that turns **Elite Dangerous** flight footage into measured data and stylized images for [Canonn](https://canonn.science). It has five modes - Ring Rotation, Station Rotation, Jet Cone Length, Long Exposure, and Slit Scan - built around one shared video library.

## Who it's for

Elite Dangerous explorers recording or already holding flight footage for Canonn's rotation and jet-cone research, or anyone who wants to turn a clip into a star-trail or slit-scan image.

## The video library

Every mode reads from the same video library panel on the left instead of having its own file picker, so a video you tag once is ready for any mode. Click **Upload Video…** to add a file; a prompt lets you confirm or search for the system (and body/ring/station) it was recorded at. The library stores a reference to the file's location instead of copying videos into an app folder; if you accept the suggested rename in the upload dialog, the app may rename or move the file (depending on your Configuration settings). An entry shows as missing if the file is later moved, renamed, or deleted outside the app.

### Matching a video to its system

When you upload a video, the app looks for a system name in the leading words of its filename - e.g. `Aurorae AB 3 A Ring.mp4` matches the system Aurorae, since ring and station names in Elite Dangerous start with the system name. This is the same convention the app itself uses when it offers to rename a file after analysis. If the filename doesn't match anything (a screen recorder's default name, for example), just search for the system by hand in the upload prompt.

## Modes

### Ring Rotation

Estimates how fast a planetary ring or asteroid belt is rotating, then measures the actual period from your footage.

1. **Upload or select a video** from the library on the left.
2. **Find a system.** Type a system name (3+ characters, auto-filled if the filename matched) into the search box. The app queries the [Spansh](https://spansh.co.uk) systems API for matches as you type and lets you pick one.
3. **Browse its rings.** The app fetches the full body dump for that system from Spansh and lists every ring and belt it finds, each with:
   - an estimated rotation period, computed from Kepler's third law using a nominal radius between the ring's inner and outer edge, and
   - a suggested recording duration (roughly the estimated period divided by 36, rounded up to the nearest minute).
4. **Record the video in-game.** For the ring you want to measure, park on the ring surface in free camera, face the horizon (not straight up at the rotation center), keep the horizon roughly centered with open starfield above it, avoid zooming, and record for at least the suggested duration.
5. **Analyze.** Click **Analyze Video** - the app tracks stars across the frame as the disk you're standing on rotates and solves for the observed rotation period. A results dialog shows the estimated vs. observed period, the percent difference, and a confidence score.
6. **Save and send.** Saving appends a row to a local CSV log (system, coordinates, ring geometry, estimated/observed periods, video filename), viewable from the "Measurement History" tab. You can send a measurement to Canonn immediately or later. All captured Ring Rotation data is viewable in a [Google Spreadsheet](https://docs.google.com/spreadsheets/d/1rmZLv9sLChYD3QaLos6lWba2TWp5I2W3La4tuvm5hro/edit?resourcekey=&gid=947491494#gid=947491494).

### Station Rotation

Measures how fast an orbital station, installation, or Guardian Beacon is rotating, compared against its parent body's known rotation period (surface ports and settlements aren't offered, since they don't rotate). Pick a system and a station, installation, or beacon, film it framed the same way as a ring - horizon-centered, holding position - and the same star-tracking analysis produces an observed period, logged and sent to Canonn the same way as Ring Rotation.

### Jet Cone Length

Measures the length of a neutron star or white dwarf's jet cone. Fly straight toward the jet and film the approach until the "WARNING! FSD OPERATING BEYOND SAFETY LIMITS" overlay appears. The app finds the exact frame where that warning appears and reads the HUD's distance counter at that instant - with an optional Claude Vision fallback if the local reading is uncertain - then asks you to confirm the reading before it's saved and optionally sent to Canonn. All captured Jet Cone Length data is viewable in a [Google Spreadsheet](https://docs.google.com/spreadsheets/d/1xVrR5bprokc0SBOYz_QDI1eSBUMySSqbpxto1bA6Ebs/edit?gid=864619879#gid=864619879).

### Long Exposure

Stacks every frame of a video into a single still image using one of six blending modes - Average, Maximum, Minimum, Max Minus Min, Motion Variance, or Motion Blur - producing effects like star trails or motion heatmaps. Not a logged measurement; it's for turning ordinary flight or orbit footage into a striking single image, saved to disk rather than a CSV row.

### Slit Scan

Builds a single image by sampling a thin strip from every frame and laying those strips side by side in time order, so motion crossing the strip turns into diagonal or curved streaks instead of a normal photo. Sliders control the strip's angle, position, width, and motion (fixed, sweeping, or orbiting a center point for a spiral/tunnel look), previewed live before you generate the final image. Like Long Exposure, it produces a stylized image rather than a logged measurement.

## Configuration

Settings shared across every mode: whether the app watches your Elite Dangerous journal folder to auto-fill your commander name and current system, how renamed library videos get organized into folders, and an optional Claude API key used only as a fallback for Jet Cone Length's distance reading.

## Download

[⬇ Download latest release (Windows, self-contained, no .NET install required)](https://github.com/canonn-science/VideoAnalysis/releases/latest)
