# Sonar3D

ML-enhanced 3D sonar for the Water Linked 3D-15 style range image: physics raycasts feed a deployed ONNX model, which refines detection, range, and intensity. The component inherits from `Sonar`, so existing ROS publishers and visualizers work without adapters.

(range and intensity estimation is still very experimental)

Legacy **FLS**, **SSS**, and **MBES** sonars remain on the base `Sonar` component. Use **Sonar3D** only for the fixed 3D-15 geometry and inference pipeline.

## Requirements (SMARCUnity)

Sonar3D needs the ONNX model and runtime in the Unity project (not in this package alone):

1. **ONNX Runtime** — `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.OnnxRuntime.Managed` (1.22.1) via NuGetForUnity in `SMARCUnity/Assets/packages.config`.
2. **Model file** — `sonar_model.onnx` (~55 MB) in `SMARCUnity/Assets/StreamingAssets/`.

If the model is missing or fails to load, Sonar3D falls back to physics-only returns (raw raycast hits).

## Adding Sonar3D to a vehicle

**New sensor**

1. Add **Smarc → Sensor → Sonar 3D** on the sensor GameObject.
2. Add **Smarc → ROS → SonarPointCloud_Pub** on the same object (or ensure one `Sonar` is on the publisher’s GameObject).

**Replace an existing Sonar**

1. In Edit Mode, select the legacy `Sonar` component.
2. Use **Convert to Sonar3D** from the Inspector or the component context menu.
3. Conversion is undoable and keeps the component file ID and shared settings.
4. Re-check model path and debug options after conversion.

Only one `Sonar` component should be on the publisher GameObject; the publisher uses `GetComponent<Sonar>()`.

## Fixed geometry (v1)

| Parameter | Value |
|-----------|--------|
| Grid | 256 × 64 rays (16,384 total) |
| Horizontal FOV | 90° |
| Vertical FOV | 40° |
| Max range | 15 m |

These match the Water Linked 3D-15 sensor layout. Beam count and FOV fields on the base `Sonar` type are overridden and hidden in the Inspector.

## Model contract (v1)

Tensor shapes are defined in code as `Sonar3DModelSpec.V1` (see `Runtime/Scripts/VehicleComponents/Sensors/Sonar3DModelSpec.cs`).

**Input** `sonar_features` — float32 `[1, 4, 64, 256]` (NCHW), per pixel:

| Channel | Meaning |
|---------|---------|
| 0 | Normalized azimuth |
| 1 | Normalized elevation |
| 2 | Range / 15 m |
| 3 | Non-negative cosine of incidence |

**Output** `sensor_parameters` — float32 `[1, 3, 64, 256]`:

| Channel | Decoding |
|---------|----------|
| 0 | Detection logit → sigmoid → probability |
| 1 | Range residual (m) added to input range |
| 2 | Log intensity → `max(0, exp(log I) - 1)`, normalized per frame |

Input/output **channel counts** may change when a new ONNX model is deployed; image height/width are tied to the sensor and are not expected to change. Add a `V2` spec block and update tensor build/decode paths when upgrading the model.

## Detection policy

Inspector settings on Sonar3D:

- **Threshold** (default): accept when `p >= ModelDetectionThreshold` (default 0.5).
- **Hybrid**: reject below lower threshold; accept at or above upper; between them, Bernoulli sample with probability `p` (defaults 0.25 / 0.75).

A published return requires **both** a physics raycast hit and an accepted inference result. Ranges outside `(0, 15]` m or non-finite model outputs are rejected.

Hybrid sampling uses a dedicated random source (not `UnityEngine.Random`), so runs are stochastic unless a seed is added later.

## ROS point cloud

`SonarPointCloud_Pub` publishes `sensor_msgs/PointCloud2` in the `unity_origin` frame (ENU), one point per ray, 13 bytes per point (float32 x/y/z + uint8 intensity).

**Breaking change vs legacy sonar:** `is_dense` is **false**. Rejected or missing returns use **NaN** coordinates (not omitted points). Downstream nodes must filter invalid points.

## Debugging

**In-game debug window** — enable **Show Model Debug Window** on Sonar3D for heatmaps (raycasts, probability, mask, range, intensity) and timing stats.

**Scene gizmos** — **Draw Model Debug** shows raw raycast hits (cubes) and accepted model returns (spheres by probability).

**RayViewer** — optional `RayViewer` on the same object:

- **Draw Rays** — strided grid (`RayDrawStride`, default 4 ≈ 1/16 of rays); only **detected** hits are drawn.
- **Draw Hits** — particle hits; respects `DrawEveryNthFrame`.

For dense 16k-ray sonars, prefer the Sonar3D debug window over drawing every ray line.

## File layout

| File | Role |
|------|------|
| `Sonar.cs` | Legacy sonar types, shared raycast scheduling, buffers |
| `Sonar3D.cs` | Fixed geometry, model I/O, detection policy |
| `Sonar3D.Model.cs` | Model path resolution, editor parity tools |
| `Sonar3D.Debug.cs` | Debug window, heatmaps, gizmos |
| `SonarReturnModel.cs` | ONNX session and inference |
| `Sonar3DModelSpec.cs` | v1 tensor shapes and channel indices |
| `SonarHit.cs` | Per-ray hit state and PointCloud2 bytes |
| `Editor/Scripts/SonarEditor.cs` | Inspector and Convert to Sonar3D |

## Known limitations

- **Platform:** macOS editor/standalone tested with CoreML/CPU. Windows/Linux player builds need ONNX native plugin verification.
- **Dependency:** `SMARC.SMARCAssets` references ONNX Runtime for all consumers, even legacy sonar-only projects.
- **Performance:** Inference runs synchronously on the main thread after each raycast batch completes.
- **Calibration:** Range and intensity semantics follow the deployed model; confirm against training/export before quantitative use.

## Prefab

`Runtime/Prefabs/BlueROV2_Heavy_Sonar3D.prefab` — BlueROV2 Heavy with Sonar3D and publisher pre-wired.
