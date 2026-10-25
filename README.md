# SpeakerMesh 0.4 - Auto Sync

This update adds microphone-based automatic speaker alignment and an Apple-inspired neutral/blue interface. Keep your current working Bluetooth and audio-source setup; no new driver is required for Auto Sync.

## Upgrade and sync

1. Close the previous SpeakerMesh app. Extract this archive and open App/SpeakerMesh.exe. Saved setups load automatically when available. Windows 10/11 x64 and .NET 9 Desktop Runtime are still required.
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

## Build and test

Run Build.ps1 with the .NET 9 SDK. No external NuGet packages are needed. Source includes the probe generator, matched-filter detector, microphone capture, calibration controller, and UI.

The test harness accepts --devices --startup-failure --silent-hardware. Native hardware tests send zero-gain output; they do not open a physical microphone or verify acoustic alignment. Command-line --microphones lists capture endpoints without recording.

The offscreen UI checks and render commands from 0.3 remain available. --render-calibration-preview generates an offscreen view of the calibration dialog without showing it or recording. See VERIFICATION.md for the completed checks and remaining hardware acceptance.

References:
https://www.bluetooth.com/wp-content/uploads/2022/01/Introducing-Bluetooth-LE-Audio-book.pdf
https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudioclock-getposition
https://learn.microsoft.com/en-us/windows/win32/coreaudio/capturing-a-stream
https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudiocaptureclient-getbuffer
