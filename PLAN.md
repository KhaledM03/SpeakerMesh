# SpeakerMesh development plan

Goal: play one Windows audio source through independently connected mixed-brand outputs, with adjustable relative delay. Literal zero latency and universal Alexa Wi-Fi grouping are infeasible; acoustic synchronization must be measured on the actual speakers.

1. Research Amazon's proprietary speaker grouping, Soundcore PartyCast, and Microsoft WASAPI.
2. Build native Windows endpoint discovery, shared-mode multi-output rendering, WAV/system-audio/test-pulse sources, per-output volume/delay/channel, and saved profiles.
3. Use a shared monotonic audio timeline, bounded timestamped capture history, queue-aware rendering, gentle software drift correction, cancellation, feedback prevention, and transactional startup.
4. Validate PCM decoding, delay math, channel routing, ring-buffer wrap, malformed files, native enumeration, and startup failure. Compile Release. Attempt visual QA; report unavailable infrastructure.
5. Package the application, source, tests, user guide, and verification record.

Architecture: WinForms controls -> AudioSession -> independent WASAPI render workers. WAV/test pulses/loopback -> common stereo source. Every output samples this timeline at an offset based on its render queue and added delay. The correction follows PC time; it does not measure hidden Bluetooth buffering, speaker clock error, or sound at the listener.

Acceptance requiring your hardware: hear audio on each intended speaker; align recorded pulses; repeat at 1/10/30 minutes; stop/restart; disconnect/reconnect; measure video lip-sync if needed. No hardware synchronization claim before these pass.

Deferred: automatic acoustic calibration, true multichannel surround decoding, universal Alexa Wi-Fi transport, reconnection automation, signed installer, and guaranteed latency bounds.

Privacy: audio stays in memory on this PC; no cloud service, telemetry, account, microphone capture, or automatic changes to Windows devices. The user pairs Bluetooth speakers and selects the source/destinations.

## Revision 0.2
User reported doubled audio/crackling in WAV mode, silent Echo, and failed system audio. Changed WAV/test output to a continuous cursor without timeline seeks; primed render buffers and increased buffering for stability. Mapped capture packets with contiguous device frame positions. Added default-output selection, an input activity meter, diagnostic copy, and selection of an existing Oculus virtual source. No Windows routing, driver, Bluetooth, or endpoint volume changes are made automatically. Source speaker remains undelayed; a separate non-audible source is required for adjustable delay on every physical destination. Native zero-gain checks include currently connected Echo and Anker; acoustic acceptance is still pending.

## Revision 0.3
WPF hub-and-speaker interface replaces the grid. Keep virtual inputs separate from physical destinations. Integrate setup for a separately installed VB-CABLE endpoint with a verified SpeakerMesh name. Acquire console/music defaults after streams open; optionally retain the input across default changes while relaying. Restore previous outputs or a physical fallback on stop/close. Add isolated policy lifecycle tests and offscreen interface checks. Driver installation and full default-output acceptance remain pending; no driver payload is distributed.

## Revision 0.4 - acoustic Auto Sync
User approved brief local microphone use for calibration. Add per-speaker scheduled chirps, bounded WASAPI microphone capture, normalized matched filtering, repeated-arrival stability gates, faster-path delay calculation, and a second verification sweep. Apply/save only after a successful check; preserve previous delays on cancellation/failure. Keep the virtual route during measurement and resume the prior relay. Prefer USB microphones and exclude known virtual sources. Remember a successfully used microphone for later one-click runs. Restyle the desktop UI with neutral surfaces, system typography and blue accents. No Bluetooth latency-report API or ping-based timing claim is made. Real acoustic capture/alignment acceptance remains pending.
