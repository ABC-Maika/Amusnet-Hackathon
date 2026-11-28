# Sprint 1 art direction

Preserve the butterfly's existing low-poly silhouette. Give the wings, body, and antennae distinct shapes that read from the follow camera. Use simple surfaces and flat shading on environmental props. Prefer few materials per mesh and share palettes across small props.

| Use | Color |
| --- | --- |
| White butterfly wings | `#F4EFE1` |
| Butterfly body and deep shadow | `#27252B` |
| Grass and foliage | `#728A52` |
| Ground and timber | `#8C694D` |
| Warm accent and future pollen | `#E9B65D` |
| Sky and water | `#8DB4C7` |

Use URP Lit or Simple Lit for world materials. Keep source art and gameplay assets grouped by feature. The butterfly model and clips are under `Assets/Art/Butterfly`, the reusable prefab is under `Assets/Prefabs`, and the flight and camera components are under `Assets/Scripts`. The standalone flight scene has a few primitives for collision checks.

## Flight preview

Open `Assets/Scenes/ButterflyPreview.unity` and enter Play mode. Hold **W** to climb or take off, **S** to dive, and **A/D** to turn. Arrow keys and the gamepad left stick also steer. Release the controls to descend automatically and stop on landing. The wings animate during flight. Hold **Space** to leave a pollen trail. Hold **Left Shift** to boost and leave wind streaks from the outer wing edges that drift with the local wind.

Move the mouse to orbit the camera. Press **Esc** to release the cursor and click the Game view to capture it again.

Sprint 1D village work is outside this preview.
