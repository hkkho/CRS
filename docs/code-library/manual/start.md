# Start a time-based run

1. Choose **Start game** and wait for **Start**. Under **Starting core**, you can enter a seed to reproduce a core.
2. Press **Start** and inspect the core map and liquid-zone levels. Each simulated half-day takes 7.5 seconds to display when the next solution is ready. Choose **0.5×** for 10 seconds or **1.5×** for 5 seconds. Press the selected speed again to return to normal.
3. Queue fuel when a channel needs it. Use **Pause** to inspect the core and edit waiting orders without advancing playback. Yellow insets mark waiting orders. Blue insets mark orders already submitted to the solver. **Retry seed** repeats the starting core and **New core** draws a different seed.

If the run ends, the result dialog gives the reason and offers **Retry seed**, **New core** or **Main screen**. Press Escape to inspect the final core. **Run result** reopens the dialog.

## Tutorial

Choose **Tutorial** below **Start game**. The tutorial holds seed 1001 at the starting state while you explore the HUD, map tabs, zone gauges, and trend tabs. The lesson sits beside the map on wider screens and above it on smaller screens. After comparing two channels, queue one and advance 12 simulated hours. Use **Try the other channel** to repeat the exercise from the same starting state and compare the outcomes.

## Controls

| Action | Pointer or touch | Keyboard | Standard gamepad |
| --- | --- | --- | --- |
| Inspect a channel | Select a channel | Arrow keys on the map | D-pad |
| Queue or remove waiting fuel | Double-click or use **Queue fuel** or **Remove order** | Enter or R on the map | Bottom face button |
| Change the map | Select a map tab | Left or Right on the map tabs | Shoulder buttons |
| Pause or resume | Use the playback button | Focus and activate the button | Start |

## Custom physics

Choose **Custom physics** on the launcher to set up a sandbox. Edit nominal power, device absorption, iodine and xenon kinetics, diffusion and boundary geometry, fuel coefficients and solver settings. **Restore defaults** clears the draft. **Build custom core** validates the settings and solves the starting state. If the core cannot settle, choose **Back** and revise your draft. The run uses the chosen settings through retries and local update restores.

## Updates and recovery

When a new build is ready, close other game tabs if prompted. The game saves the latest solved state and waiting orders, then reloads. A compatible checkpoint opens at **Start**. If the model or score policy has changed, the game keeps the checkpoint and offers a return to the launcher.

In retained daily views, select channels in the plan and confirm the day explicitly. Each accepted day covers 24 simulated hours.
