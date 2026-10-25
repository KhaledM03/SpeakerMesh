# Production release gates

0.5 is a development preview, not a qualified production release. No universal speaker, latency, or acoustic accuracy claim is made. This checklist maps the seven-step product plan to implementation and outstanding acceptance.

| Step | Implemented | Still required before stable release |
| --- | --- | --- |
| 1. Foundation | .NET 10 LTS; independent core; executable regression suite; Linux/Windows CI | Run Windows CI on the remote branch; qualify supported Windows releases |
| 2. Windows audio route | Existing virtual-input routing; atomic recovery journal; preserve explicit user output changes | Choose and license a distributable signed virtual driver; qualify private policy-interface fallback; driver install/repair testing |
| 3. Regular volume controls | Live speaker gain/mute with ramps; virtual-source master volume/mute; Windows knob reflected by polling; explicit driver compatibility mode | Measure actual audio gain at 100/50/10/0%, mute and unmute for each supported driver; no double attenuation; focused/minimized/tray tests |
| 4. Synchronization | Existing two-pass microphone Auto Sync; bounded cursor correction; invalidation on routing/device changes | Device-clock-driven adaptive resampling, format-aware persisted calibration, repeated physical acoustic measurements, long-duration drift qualification |
| 5. Reliability | Isolate output faults after startup; stop when every output fails; capture failures remain fatal; sleep/wake stops playback; restore defaults | Automatic muted/rebuffered rejoin, moving lifecycle out of WPF, audio-service recovery, allocation/scheduling tuning, eight-hour hardware soak |
| 6. Desktop experience | Tray playback/exit, live controls, named profiles, atomic saves, offline speaker retention, optional installer sign-in startup | End-to-end onboarding/accessibility/scale QA; better diagnostics export and per-output buffer metrics |
| 7. Release | Self-contained Windows publish; CI artifact; per-user Inno Setup recipe | Installer compilation/testing, application/installer signing, licensed driver distribution, authenticated updates and rollback, hardware beta |

## Required Windows acceptance

Use a clean Windows PC, a USB microphone, and two to four Windows-visible outputs with at least two different transports. Record Windows build, driver versions, Bluetooth adapter, speaker models, source format, and microphone location.

1. Install preview. Verify no microphone capture, playback, default switch, or sign-in startup occurs without selecting it. Installer startup task is opt-in.
2. Select virtual input and speakers. Start browser music. Confirm no physical source duplicates playback and no feedback route is selectable.
3. Turn keyboard knob and Windows slider through 100/50/10/0%; confirm actual recorded amplitude follows once, not twice. Test mute/unmute, relative speaker balances, and per-app volume. If the virtual driver ignores endpoint volume in loopback, enable the compatibility setting and repeat. This is a per-driver qualification, not an automatic detection claim.
4. Close the playing window. Confirm playback and keyboard volume still work. Reopen via tray; mute and stop from tray; exit restores the previous Windows output. Explicitly select headphones during playback: SpeakerMesh must stop without overriding them.
5. Calibrate in a quiet room. Verify alignment at start, 10, 30, and 60 minutes. Proposed acceptance: <=10 ms inter-speaker arrival spread at the microphone. Measure total source-to-sound latency separately. Video lip-sync is not guaranteed.
6. Unplug a speaker: remaining outputs continue, UI reports degraded state, and calibration is no longer displayed as current. Reconnecting currently requires stopping/restarting and recalibrating. Unplug the source or all outputs: playback stops and routing is restored where an output remains.
7. Exercise sleep/wake, calibration cancellation, permission denial, app crash/restart, missing previous default, and explicit user output changes. Recovery must not overwrite the user's new route. Run an eight-hour playback soak and check memory/CPU, underruns, and audible artifacts.
8. Test clean install, upgrade, uninstall, and rollback. Uninstall after exiting the tray app. Keep user profiles unless the user elects to remove them; remove the sign-in entry. Confirm driver licensing/signing and signed release hashes before distribution.

## Test evidence

The core harness is independent of WPF and WASAPI. It tests digital timing, gain ramps, routing ownership with a fake backend, profile integrity, and synthetic acoustic detection. Passing these tests does not establish native audio reliability, keyboard-to-audible gain behavior, or room synchronization. Historical claims in VERIFICATION.md describe the original version, not this preview.
