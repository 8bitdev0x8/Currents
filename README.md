# Fluid Currents — Unity

An interactive fluid-art scene inspired by the supplied Blender render. A 96×96 grid solves 2D incompressible flow using viscous diffusion, semi-Lagrangian velocity advection, pressure projection, and vorticity confinement. The 3D sphere acts as a moving no-slip obstacle when it intersects the fluid plane; the rendered streamlines and highlighted red current come from the solver's streamfunction.

The simulation is a real-time 2D numerical flow model rendered on a perspective 3D surface. It is intentionally lower-dimensional than a full 3D CFD solver.

## Run it

1. Open `D:\Currents\UnityProject` in Unity Hub with Unity 6.0.3 or newer.
2. Open `Assets/Scenes/FluidCurrentsDemo` if needed.
3. Press **Play**. Right-drag the silver ball; hold it and scroll to raise or lower it. Left-drag on the plane to change the flow direction. The Game view shows the live FPS in its upper-right corner.

Select **Main Camera** to tune inflow speed, viscosity, simulation rate, rainbow fade and cycle length, streamline spacing and widths, flow direction, and the palette. The demo scene is listed in Build Settings.

The project uses Unity's built-in renderer and requires no extra packages.
