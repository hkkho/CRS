# Start a time-based run

1. Choose **Time based** and wait for **Start**. Under **Starting core**, you can enter a seed to reproduce a core.
2. Press **Start** and inspect the core map and liquid-zone levels. Each simulated half-day takes 7.5 seconds to display when the next solution is ready. Choose **0.5×** for 10 seconds or **1.5×** for 5 seconds. Press the selected speed again to return to normal.
3. Queue fuel when a channel needs it. Use **Pause** to inspect the core without advancing playback. **Retry seed** repeats the starting core and **New core** draws a different seed.

## Controls

| Action | Pointer or touch | Keyboard | Standard gamepad |
| --- | --- | --- | --- |
| Inspect a channel | Select a channel | Arrow keys on the map | D-pad |
| Queue fuel | Double-click or use **Queue fuel** | Enter or R on the map | Bottom face button |
| Change the map | Use the map selector | Focus and change the selector | Shoulder buttons |
| Pause or resume | Use the playback button | Focus and activate the button | Start |

## Custom physics

Choose **Custom physics** on the launcher to set up a sandbox. Edit nominal power, device absorption, iodine and xenon kinetics, diffusion and boundary geometry, fuel coefficients and solver settings. **Restore defaults** clears the draft. **Build custom core** validates the settings and solves the starting state. If the core cannot settle, choose **Back** and revise your draft. The run uses the chosen settings through retries and local update restores.

## Updates and recovery

When a new build is ready, close other game tabs if prompted. The game saves the latest solved state and waiting orders, then reloads. A compatible checkpoint opens at **Start**. If the model or score policy has changed, the game keeps the checkpoint and offers a return to the launcher.

In retained daily views, select channels in the plan and confirm the day explicitly. Each accepted day covers 24 simulated hours.
