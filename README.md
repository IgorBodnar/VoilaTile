![VoilaTile Banner](assets/banner.png)

## Overview

VoilaTile is a lightweight Windows utility that lets you create, save, and instantly snap to fully custom window layouts.  
It’s built for speed, precision, and a **keyboard-first workflow** that keeps your hands where the action is.  

VoilaTile takes the idea of custom window layouts — popularized by tools like Microsoft’s FancyZones — and fine-tunes it for **power users who live on the keyboard**.  
It’s not about automatic tiling or rigid layouts. Instead, VoilaTile lets you **create and save layouts that match your exact workflow**, then snap any window into place instantly with intuitive keyboard shortcuts.  

Whether you’re on one monitor or many, VoilaTile keeps your workspace exactly how you designed it — no dragging, no pixel-perfect fiddling, just seamless snapping.  

> **Download the latest version:** [Releases page](https://github.com/IgorBodnar/VoilaTile/releases)

## Why VoilaTile?  

Your workspace should adapt to you — not the other way around.  
VoilaTile is for people who:  

- Prefer to **design their own layouts** instead of settling for fixed tiling rules  
- Want to **snap windows in milliseconds** without touching the mouse  
- Work across **multiple monitors** and need precise, repeatable window placement  
- Value **minimal overhead** and a clean, focused workflow  

## Features  

- **Fully custom layouts** — create and save zones for any monitor configuration  
- **Instant snapping** — place windows exactly where you want them with a light keyboard motion  
- **Multi-monitor aware** — layouts are saved per monitor setup  
- **Keyboard-first design** — optimized for fast, mouse-free use  
- **Configurator** — easy visual editor for creating and adjusting layouts  

## Installation

1. Download the **latest installer** from the [Releases page](https://github.com/IgorBodnar/VoilaTile/releases).
2. Run the installer and follow the on-screen instructions.
3. Launch VoilaTile.Configurator when installation completes or from the Start Menu.
4. (Optional) Enable “Launch VoilaTile Snapper on Windows startup” in the installer to keep snapping always available.

## Usage

### VoilaTile.Configurator

#### Quick Start

1. Open the VoilaTile.Configurator
2. Select the active monitor
3. Create a new layout or reuse existing layouts
4. Select the layout for each of your monitors

#### Other Features

- **Full control** — rename, duplicate, or delete layouts in the library  
- **Multiple monitors** — create independent layouts per monitor  
- **Customize hints** — modify the hint string to use preferred characters for snapping  
- **Customize shortcut** — change the Snapper launch hotkey directly from the Configurator  
- **Easy Snapper launching** — don’t have the Snapper running? The Configurator will prompt you to launch it on exit  

![Quickstart Create Layout](assets/quickstart_create_layout.gif)

### VoilaTile.Snapper

#### Quick Start

1. Launch the overlay by pressing **Win + Shift + Space** (or your configured shortcut) while the target window is in focus  
2. Type in the hint for the desired tile or a valid combination of tiles  
3. Press **Space** or **Enter** to snap the target window to the desired location

#### Other Features

- **All valid combinations** — all combinations of tiles that can form a rectangle are enumerated  
- **Clear hints** — hints are positioned in the visual center of the respective tile or their combination  
- **Visual highlights** — the snapping position is highlighted as you type  
- **Input feedback** — your input is visible at the bottom of each overlay (because we all mistype sometimes)  
- **Robust** — forgot to create a layout for one of your monitors? The Snapper will prompt you to launch the Configurator and fix your layouts  

![Quickstart Snapping](assets/quickstart_snapping.gif)

## Contributing

Pull requests are welcome.  
Open an [issue](https://github.com/IgorBodnar/VoilaTile/issues) for bugs, ideas, or feature requests.

## Support

If you find VoilaTile useful, please **star the repository** — it helps others discover it.  
Have a suggestion or found a bug? Let us know via the [issues tab](https://github.com/IgorBodnar/VoilaTile/issues).
