## Main Menu Spike

Goal: provide a small, testable Main Menu system with a single "Start" button. When pressed the system will run a coroutine that replicates the current scene/bootstrap, displays a loading progress bar while the planet and game model are generated, then fades the menu UI out and enables player controls (camera/unit movement).

Design constraints
- Keep UI minimal (Canvas + Start button + Loading bar + fade panel).
- No heavy animation or polish; focus on a deterministic boot sequence and clear progress reporting.
- The bootstrap must expose progress (0..1) so the loading bar can be updated. We'll add a simple progress callback to the existing bootstrap code.

API contract
- MainMenuController
  - StartButton() — called by UI to begin the coroutine
  - IEnumerator StartGameCoroutine(Action<float> onProgress) — runs bootstrap, invokes onProgress with progress [0..1]
  - void OnBootstrapComplete() — called when done; fades UI and enables gameplay inputs

Bootstrap contract
- The existing bootstrap that builds topology and models should be refactored slightly to accept an IProgress<float> or an Action<float> to report progress stages: topology build (0.0-0.4), model build (0.4-0.8), lookup & unit spawn (0.8-1.0).

UI layout
- Canvas (Screen Space Overlay)
  - Panel (full-screen) with background dim
    - StartButton (center)
    - LoadingBar (Slider or Image, hidden until Start pressed)
    - FadePanel (Image) used to fade out after loading

Edge cases
- Bootstrap failure: show an error dialog and re-enable Start button.
- Long hangs: add a simple timeout and fallback error.

Testing
- Manual: press Start, observe loading bar progress, menu fade, then ability to control camera and right-click units.
- Automated: unit test the bootstrap progress callbacks by invoking the coroutine's internal steps synchronously.

Next actionable items
1. Add `MainMenuController.cs` that implements start coroutine and fade.
2. Modify bootstrap to accept an onProgress callback (small patch to the bootstrap file).
3. Connect `UnitInputController` and camera enabling in `OnBootstrapComplete`.
4. Add a simple scene Canvas and wire the Start button to `MainMenuController.StartButton`.
