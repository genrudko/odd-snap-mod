# Native window capture — stage 2

This stage replaces fixed desktop-bound window recording with an HWND-backed Windows Graphics Capture source while preserving the existing GIF and FFmpeg encoding pipelines.

## Behaviour

- Region and monitor recording continue to use the existing desktop frame source.
- Window recording prefers Windows Graphics Capture when the runtime supports it.
- Moving or covering the selected window must not change the captured content.
- The initial selected window size remains the fixed encoder output size for this stage.
- If native source initialization fails, OddSnap logs the failure and falls back to the existing fixed desktop bounds.
- Closing the selected window ends frame production cleanly.

## Manual acceptance checks

1. Record a window and cover it with another window; the covering window must not appear in the recording.
2. Move the selected window between monitors; recording must continue with the selected window content.
3. Minimize and restore the selected window and verify the resulting behaviour.
4. Resize the selected window; output dimensions must remain fixed without encoder failure.
5. Record with cursor capture both enabled and disabled.
6. Verify MP4, WebM, MKV, and GIF paths.
