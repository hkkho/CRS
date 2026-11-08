# 1. Entry points and startup

[index.html](../../web/candu-playtest/index.html) mounts [main.ts](../../web/candu-playtest/src/main.ts). The launcher selects a seed and opens the time-based view. Custom physics supplies different initialization inputs. The tutorial uses the same view and worker on a fixed seed and advances only when the player submits the exercise.

### main.ts constructs the browser run

```annotated-source
path: web/candu-playtest/src/main.ts
title: "Restore the seed, custom draft and replay pointer"
language: typescript
start: "function openGame(coreSeed: number, id?: string, restored = false, customPhysics?: CustomPhysics, guided = false): void {"
end: "  unsubscribe = game.subscribe(() => renderVersion());"
steps:
- through: "  activePhysics = customPhysics;"
  explanation: "Restore the saved seed and any custom editor values. Retain the options for the lifetime of this run."
- through: "  }) : undefined;"
  explanation: "Remove the launcher and load the journal from IndexedDB if the checkpoint includes a record ID."
- through: "  unsubscribe = game.subscribe(() => renderVersion());"
  explanation: "Give the view the starting inputs and recovery callbacks. Version status follows the active view. Starting regular play consumes the resume pointer."
```

### The view starts its own worker

```annotated-source
path: web/candu-playtest/src/DemoView.ts
title: "Construct the worker and start paused"
language: typescript
start: "    if (customPhysics) this.text(\".arcade-kicker\", \"CRS / CUSTOM PHYSICS\");"
end: "        this.map?.focusSelected();"
steps:
- through: "    this.loop = new DemoLoop(createBridge(seed), undefined, true, createBridge, restore, isTutorial);"
  explanation: "Initialize a worker in demo pacing. The factory also carries custom options into a replacement worker after a transport failure."
- through: "        this.map?.focusSelected();"
  explanation: "Track visibility and hold playback until the player presses Start. Open the first tutorial lesson or start regular playback and focus the selected channel."
```

[Transport](02-browser-transport.md) follows initialization into the managed runtime.
