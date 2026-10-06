# Fluid Currents — Unity

An interactive, perspective 3D fluid-art scene inspired by the supplied Blender render. The sphere is a true 3D object resting on a large procedural surface. Drag it through the Game view to move the obstacle and its wake.

The surface uses a potential-flow approximation around the sphere plus an alternating, viscously broadened vortex street. Its shedding frequency follows `f = St × U / D`, with the Strouhal number exposed in the Inspector. This is a real-time visual model, not a full computational-fluid-dynamics solver.

## Run it

1. Open `D:\Currents\UnityProject` in Unity Hub with Unity 6.0.3 or newer.
2. Open `Assets/Scenes/FluidCurrentsDemo` if needed.
3. Press **Play** and drag the silver ball in the Game view.

Select **Main Camera** to tune flow velocity, Strouhal number, viscosity, wake strength, line frequency, flow direction, and the black, violet, silver-lavender, red, and amber palette. The demo scene is listed in Build Settings.

The project uses Unity's built-in renderer and requires no extra packages.
