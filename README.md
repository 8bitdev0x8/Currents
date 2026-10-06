# Fluid Currents — Unity starter

A small procedural shader demo for flowing, psychedelic current artwork. The effect is animated and rendered on a camera-filling quad; it does not simulate physical fluid. The shader works with Unity's built-in renderer and URP.

## Open the included project

Open `UnityProject` in Unity Hub. It is a standard project created with the installed Unity 6 editor, with the demo assets already copied into its `Assets` folder.

## Get it running

1. Open `D:\Currents\UnityProject` from Unity Hub using Unity 6.0.3 or newer.
2. Open `Assets/Scenes/FluidCurrentsDemo` if Unity did not open it automatically.
3. Press **Play**. The animated contour field and silver ball appear.
4. Drag the ball with the mouse in the Game view. The current lines bend around it as it moves.
5. Select **Main Camera** to adjust the palette, flow scale, speed, swirl, contour bands, and ball influence.

The demo scene is included and enabled in Build Settings. Its palette uses near-black, lavender, silver, red, and gold. The shader works with Unity's built-in renderer and URP, and needs no extra packages.

## Tuning tips

- Lower **Flow Scale** for broad, liquid folds; raise it for tighter currents.
- Raise **Swirl** for more warped, psychedelic motion.
- Lower **Speed** for a slowly shifting album-art loop.
- Try a dark navy base with cyan and magenta highlights for a saturated, high-contrast palette.

For a still image, pause at a chosen frame and capture the Game view at the desired resolution. For a seamless loop, keep the animation time periodic and record a full repeat; the current shader is designed as an evolving effect rather than an exact seamless loop.
