# SpeakerMesh 0.4 verification

Release build: zero warnings and zero errors. 64 engine/routing/native checks and six offscreen interface checks passed, zero failed.

New synthetic acoustic tests verify known-delay recovery, 44.1 kHz input conversion, noise/reflection/gain/polarity robustness, relative correction calculation, cancellation of common microphone latency, successful aligned check measurements, rejection of silence/noise/unstable/ambiguous arrivals, rejection of excessive residual error, tolerant handling of one missing repeat, and per-speaker probe isolation. These simulations do not establish real-room accuracy.

Native WASAPI checks enumerated five active outputs, including Anker SoundCore and Echo Spot-EG3. Individual and simultaneous zero-gain render/start/stop/restart tests passed. The new per-output probe stream path was exercised at zero gain with all endpoints open, including shared start-timestamp availability. Separate loopback/output lifecycle checks passed. No audible probe or physical microphone test was run during development.

Read-only microphone enumeration found USB CAMERA and USB PnP audio endpoints, plus headset/virtual inputs. The chooser prefers USB devices and filters known virtual audio endpoints. Device presence alone does not verify capture, processing behavior or acoustic detection.

Six offscreen interface checks passed, including the Auto Sync control. Main and calibration window renders were visually inspected. The calibration dialog has scrolling content and fixed action/cancel buttons. Live mouse/keyboard automation was unavailable in prior checks and is not claimed here.

Still pending: actual microphone capture permission and timestamp behavior on this PC, acoustic measurements on the target speakers, cancellation/disconnection during a live measurement, real-room noise/reverberation performance, the complete measure/check/apply/resume cycle, and post-restart/long-duration synchronization. End-to-end Auto Sync remains experimental until those checks are performed by starting it with a suitable microphone.

Normal audio routing and saved-profile compatibility are retained from the working version. The user reported the prior release works well; that report is not treated as verification of the new calibration feature. No driver installation or OS microphone/default changes were made during tests.
