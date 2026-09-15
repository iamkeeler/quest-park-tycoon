# Quest Park Tycoon — Gary's on-machine checklist (Phase 0 spike)

Goal: open the scaffold in Unity 2022 LTS, install the Meta XR SDK, and hit the
Phase 0 exit criteria — boots on Quest 3 at 72 Hz, one grabbable cube, terrain
tile grid renders. Nothing here compiles or runs on the Linux build box; every
step below happens on your machine.

## 1. Install Unity

1. Install **Unity Hub**, sign in.
2. Add **Unity 2022.3 LTS — latest patch** (e.g. via Hub: Installs > Install Editor > 2022.3.x).
3. In the module picker, check **Android Build Support** (includes Android SDK/NDK tools).
4. Clone the repo and copy the scaffold in, or clone directly:
   `git clone https://github.com/iamkeeler/quest-park-tycoon.git`

## 2. Open the project

1. Unity Hub > Open > select the `quest-park-tycoon` folder.
2. Let Unity import. Expect it to regenerate `Library/` and fill in any
   PlayerSettings fields the scaffold's minimal `ProjectSettings.asset` omits.

## 3. Install the Meta XR SDK packages

Meta's current (2026) recommendation is the **Unity Asset Store**; the raw UPM
registry route also works. Pick one:

**Option A — Asset Store (recommended):**
1. Window > Package Manager > Packages: My Assets.
2. Add **Meta XR Core SDK** and **Meta XR Interaction SDK** to your assets, Open in Unity, Install (latest).

**Option B — scoped registry (already in `Packages/manifest.json`):**
1. Edit > Project Settings > Package Manager > Scoped Registries — confirm the
   `Meta` entry: URL `https://npm.developer.meta.com`, scope `com.meta.xr.sdk`.
2. Window > Package Manager > Packages: My Registries > install
   `com.meta.xr.sdk.core` and `com.meta.xr.sdk.interaction` (latest).
3. If the pinned versions in `manifest.json` fail to resolve, install latest
   from the registry UI instead — the *package names* are what was verified.

Also confirm these Unity packages are present (Package Manager > Unity Registry):
`com.unity.inputsystem`, `com.unity.xr.management`, `com.unity.xr.oculus`
(Oculus XR Plugin), `com.unity.xr.hands`.

## 4. Switch platform + XR plug-in

1. File > Build Settings > select **Android** > Switch Platform.
2. Edit > Project Settings > **XR Plug-in Management** (install it if prompted).
3. In the **Android** tab, check **Oculus**.
4. Edit > Project Settings > **Player** > verify:
   - Company Name: `iamkeeler`, Product Name: `Quest Park Tycoon`
   - Application Identifier: `com.iamkeeler.questparktycoon`
   - Minimum API Level: **Android 12 (API 32)**, Target API Level: **API 34**
   - Scripting Backend: **IL2CPP**, Target Architectures: **ARM64 only**

## 5. Build & run on the Quest 3

1. On the headset: enable **Developer Mode** (Meta Horizon mobile app > Devices).
2. Connect via USB, accept the RSA prompt in-headset.
3. File > Build Settings > **Build And Run**.
4. Put the headset on.

## 6. What "done" looks like

- App boots to the procedural scene (no setup scene needed — `SpikeBootstrap`
  builds everything at runtime).
- A 16×16 vertex-colored terrain tile grid renders (green diorama base with a
  gravel path cross).
- A 25 cm cube floats at chest height in front of you. It **slowly spins**
  until `com.meta.xr.sdk.interaction` is installed; once installed, re-run and
  the cube becomes a real Meta `Grabbable` (grab it with the controller).
- `adb logcat` shows `[QuestParkTycoon] Spike bootstrap complete.`
- Frame rate holds 72 Hz (enable the OVR metrics overlay to confirm).

## 7. Generate the clay props (Blender)

The starter prop set is generated procedurally — no manual modeling:

```
blender --background --python Assets/Blender/generate_assets.py
```

This writes `tree.fbx`, `bench.fbx`, `lamp_post.fbx`, `litter_bin.fbx`,
`burger_stall.fbx` into `Assets/_Project/Models/`. Reopen Unity and they'll
import automatically (vertex colors + flat shading baked in).

## Troubleshooting

- **Pink terrain:** the `QuestParkTycoon/VertexColorUnlit` shader didn't compile —
  check the Console; the bootstrap falls back to Standard automatically.
- **Cube spins but never grabbable:** Interaction SDK isn't installed or the
  script assemblies haven't recompiled — install `com.meta.xr.sdk.interaction`,
  wait for compile, re-run.
- **Package resolve errors on open:** delete the version pins for the
  `com.meta.xr.sdk.*` entries in `Packages/manifest.json` and install via the
  Package Manager UI (Option A/B above).
