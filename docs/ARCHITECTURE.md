# Architecture

```
building.json ──► BuildingDataLoader ──► BuildingData ──┬──► BuildingValidator (consistency, walls-vs-edges)
 (Resources)          (MiniJson)                         ├──► FloorLayout ─► WallGenerator ─► BuildingRenderer (meshes, signs)
                                                         ├──► RoomSearch  ◄── search box
                                                         └──► RouteFinder (NavGraph + Dijkstra)
                                                                   │ RouteResult (nodes, edges, metres, ETA)
                                                                   ▼
                                                              RoutePath (rounded corners, distance-parameterised)
                                                                   │ progress 0..1  ◄── slider
                                                                   ▼
                                                         RouteCameraController (pose = f(progress))
```

## Assemblies
* **Atria.Core** (`Scripts/Core`, `noEngineReferences`). Contains all logic that can be tested
  without rendering. It uses `System.Numerics` vectors, so the same code compiles in Unity and in
  `Tools/CoreTests` with the .NET SDK.
* **Atria.App** (`Scripts/Runtime/Atria.App.asmdef`). Contains the Unity presentation layer and
  references Atria.Core and `UnityEngine.UI`. A `versionDefines` entry sets `ATRIA_INPUT_SYSTEM`
  only when the `com.unity.inputsystem` package is installed. The UI therefore picks
  `InputSystemUIInputModule` or `StandaloneInputModule` safely, whichever input backend the
  project ends up with.
* **Assembly-CSharp-Editor** (`Editor/`). Contains the Atria menu and auto-references Atria.App.
* **Atria.Core.Tests** (`Tests/EditMode`). Contains EditMode NUnit tests that reference only
  Atria.Core.

## Key decisions
* **Walls come from walkable space.** Authors describe the convex walkable polygons
  (corridors, lobbies). Walls are the outline of their union, with door and entrance gaps cut
  where data says. Junctions need no hand-built walls, and geometry stays independent of the
  navigation graph.
* **Floor voids.** A hex ring's centre is a `VoidData` cutout: no floor or ceiling is
  generated over it, wall edges that border it (`WallSegment.BordersVoid`) get no wall at all, and the area is a
  dark, solid fill recessed 0.15 m below the floor (`VoidRecessDepth`). It is all in the same single floor mesh, with no extra materials,
  lights or transparency.
* **No route through a wall.** The validator samples every navigable same-floor edge at its
  centreline and at ±45% of its clear width. Any sample outside walkable space is a data error.
  The camera path only cuts corners by at most half the corridor width. A quadratic Bezier stays
  inside its control triangle, which keeps the camera inside the validated corridor. A test
  checks every entrance-to-room camera path in the sample.
* **Unverified edges cannot be routed.** Edges with `"navigable": false` are excluded from the
  graph, and connections that are not listed don't exist.
* **Distance-based scrubbing.** Slider value = walked distance ÷ path length, so long corridors
  take up more of the slider than short turns. Camera yaw looks at a point 2.5 m ahead on the
  track. Past the end, the track is extended towards the door. As a result, orientation is
  continuous and turns start smoothly before each corner.
* **Rendering cost.** Each floor is one combined mesh using one material, the custom
  `Atria/VertexLit` shader. Lighting is a fixed per-vertex term from vertex colours, with no
  realtime lights, shadows, lightmaps or post-processing. Room numbers are `TextMesh` objects
  drawn with the depth-tested `Atria/SignText` shader. The camera renders nothing on the search
  screen and is limited to the visible viewport during the walkthrough.
* **Offline.** Data and shaders live under `Resources/` and are bundled in the APK. The only
  font used is the engine's built-in `LegacyRuntime.ttf`. Nothing is downloaded at runtime, and
  the Android settings step removes the INTERNET permission.
* **No scene YAML.** `AtriaApp` builds the world and UI in code. The editor menu creates the
  one-object scene and applies player settings, which keeps the repository reviewable.
