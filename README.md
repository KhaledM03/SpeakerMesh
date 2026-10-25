# SpeakerMesh 0.5 development preview

Play one Windows audio source through multiple Windows-visible speakers. Control speaker balance live, use the Windows volume knob for computer audio, and calibrate relative speaker delays with a microphone.

**This is a development preview.** Linux cross-compilation and automated core tests do not qualify the Windows audio path or acoustic synchronization. See [production release gates](docs/PRODUCTION-READINESS.md) for the remaining work and hardware acceptance procedure.

## Build and test

Install the .NET SDK pinned in `global.json` (.NET 10 LTS), then from the repository root:

```sh
dotnet build SpeakerMesh.csproj -c Release
dotnet run --project SpeakerMesh.Tests.csproj -c Release
```

The test project is an executable harness that reports every check and returns a failing exit code if any check fails. `dotnet test` is not the test command. On Windows, `./Build.ps1 -Publish` runs the build/tests and creates a self-contained x64 app in `artifacts/win-x64`. Run that folder's `SpeakerMesh.exe`; the obsolete root-level 0.4 binaries have been removed. Inno Setup 6 can compile `packaging/SpeakerMesh.iss` after publishing. The recipe supports per-user installation and optional sign-in startup; it does not bundle a virtual audio driver or provide release signing.

## Everyday playback

1. Pair/connect your speakers in Windows. Install a compatible virtual audio cable from its vendor; follow its licensing and installation instructions. SpeakerMesh does not distribute a driver.
2. Select **Computer audio**, the virtual input, and the speaker cards you want. Start listening to make the dedicated input the Windows output. Media applications must use that output.
3. Use your keyboard volume knob, Windows volume slider, or **Group volume**. Speaker volume and mute remain usable during playback. Delay/channel/group changes require stopping.
4. Most virtual drivers apply endpoint volume to captured audio. If your Windows slider changes numerically but not audibly, enable **Virtual driver compatibility → Apply Windows volume in engine**, then verify the volume once at several levels. Leave it off on drivers that already apply endpoint gain. Mute is also enforced in the engine. Physical speaker endpoint volumes are never overwritten. WAV/test mode has app-local master volume; the keyboard knob remains a Windows control.
5. Run **Auto Sync** with a PC/USB microphone in a quiet room. It applies delays only after the second sweep passes. Recalibrate after reconnecting, moving speakers, or restarting streams. Continuous acoustic lock is not implemented.
6. Close the window while playing to keep audio in the tray. The tray menu reopens, mutes, stops, or exits. Choosing a different Windows default output stops the relay and respects your selection.
7. Save your setup or **Save as named group**. Load a named JSON group while stopped. Files preserve relative gains, mute, delay, channel, and the driver volume mode; saving is atomic.

A failed output is isolated during normal playback; healthy speakers continue. Rejoining currently requires stop/start and recalibration. Source failure, loss of all outputs, or sleep/wake stops playback. On startup the app attempts to restore an interrupted owned Windows route; it does not change routes that the user has already replaced.

Supported transports are those exposed as Windows audio outputs. Proprietary Wi-Fi speaker grouping is not supported. Bluetooth capacity and latency depend on your adapter and speakers. Audio stays on the PC; calibration microphone audio is held in memory and discarded. Diagnostic text includes device names/IDs, so review it before sharing.

## Original 0.4 calibration reference

## Upgrade and sync

1. Close the previous SpeakerMesh app. Extract this archive and open App/SpeakerMesh.exe. Saved setups load automatically when available. The historical 0.4 binary required Windows 10/11 x64 and .NET 9 Desktop Runtime; use the 0.5 build instructions above for this checkout.
2. Include at least two connected speaker cards. Start at comfortable listening volume and place a PC/USB microphone near your listening position. Pause music and keep the room quiet.
3. Click Auto Sync. On first use, choose a microphone and click Sync speakers. Your PC exposes USB CAMERA and USB PnP audio microphones; choose a suitable USB mic rather than the Anker headset mic, which can change Bluetooth playback mode.
4. The app plays three distinct chirps per speaker, measures their arrivals, adds delay to faster paths, and runs a second sweep to check the correction. Successful corrections replace and save your delays. If music was relaying before the test, playback resumes afterward.
5. After a successful run, the microphone choice is remembered. Later Auto Sync clicks start the measurement using that mic when it is still available. Progress and Cancel remain visible.

A sweep takes roughly 2 x (2 + 4 x speaker count) seconds plus initialization/analysis. For three speakers, allow about 30-40 seconds. Do not move the microphone during the test.

## What is measured

This measures speaker sound arriving at one microphone location, including transport, device buffering, and room travel time. It does not send a Bluetooth ping or read an authoritative Bluetooth latency response. Common microphone input latency cancels when computing relative corrections. The fastest paths are delayed to match the slowest; total audio latency is not removed.

Three different chirp patterns reduce confusion between repeats. Normalized matched filtering looks for each arrival. A speaker needs at least two strong, unambiguous arrivals within a 5 ms cluster. The check sweep must find no more than 10 ms separation between the measured speaker medians before new delays are applied. This is an acceptance rule, not a guaranteed physical accuracy specification.

Keep manual timing available for fine adjustments. Re-run after moving or reconnecting speakers, changing codecs/audio formats, or noticing drift. Auto Sync is a one-time measurement on demand, not continuous monitoring. Calibration while playback is already running reuses that stream's sample rate.

## Privacy, failure handling, and limits

Microphone audio is held in a bounded memory buffer only during calibration, then released. No microphone audio is written to a recording or uploaded. The app remembers the microphone endpoint ID and saves speaker delays in your existing local profile. It does not change microphone permissions or defaults. If Windows blocks desktop microphone access, enable access yourself or use manual timing.

Cancel, missing tones, ambiguous matches, microphone dropouts, unstable repeats, speaker disconnects, and a failed check pass leave previous delays unchanged. The UI pauses its relay while measuring, retains its current virtual Windows route where owned, and resumes the prior playback session afterward. Pause other music sources manually so they cannot interfere. Muted speakers cannot be measured; very quiet speakers may need a higher comfortable volume.

Use a real PC/USB microphone, not CABLE Output or a virtual-loopback input. Those are excluded from the microphone chooser. Bluetooth headset microphones can force a lower-quality playback profile, so a USB mic is preferred. Noise suppression/echo cancellation, heavy reverberation, weak levels, speaker processing and changing Bluetooth buffers can prevent a reliable measurement.

A successful check describes that measurement run at that microphone position. It cannot guarantee permanent synchronization after stream restart, Bluetooth reconnection, distance changes, or long-term clock drift. Manual gain, channel and delay controls remain available. Zero total latency, continuous room correction, true Dolby/DTS surround and 5.1 decoding are not included.

## Historical build and test notes (0.4)

The original release used the .NET 9 SDK. No external NuGet packages are needed. Source includes the probe generator, matched-filter detector, microphone capture, calibration controller, and UI.

The test harness accepts --devices --startup-failure --silent-hardware. Native hardware tests send zero-gain output; they do not open a physical microphone or verify acoustic alignment. Command-line --microphones lists capture endpoints without recording.

The offscreen UI checks and render commands from 0.3 remain available. --render-calibration-preview generates an offscreen view of the calibration dialog without showing it or recording. See VERIFICATION.md for the completed checks and remaining hardware acceptance.

References:
https://www.bluetooth.com/wp-content/uploads/2022/01/Introducing-Bluetooth-LE-Audio-book.pdf
https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudioclock-getposition
https://learn.microsoft.com/en-us/windows/win32/coreaudio/capturing-a-stream
https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudiocaptureclient-getbuffer
