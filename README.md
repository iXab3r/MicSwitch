![](https://img.shields.io/github/release-date/iXab3r/MicSwitch.svg) ![](https://img.shields.io/github/downloads/iXab3r/MicSwitch/total.svg) ![](https://img.shields.io/github/last-commit/iXab3r/MicSwitch.svg)
[![Discord Chat](https://img.shields.io/discord/513749321162686471.svg)](https://discord.gg/pFHHebM)  

# Intro
There are dozens of different audio chat apps like Discord, TeamSpeak, Ventrilo, Skype, in-game audio chats, etc. And all of them have DIFFERENT ways of handling push-to-talk and always-on microphone functionality. I bet many of you know how distracting it could be when someone forgets to turn off a microphone. I will try to explain what I mean using a feature matrix.

| App  | Microphone status overlay | Keyboard support | Mouse buttons support | Audio notification |
| -------------: | :-------------: | :-------------: | :-------------: | :-------------: |
| MicSwitch |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported") |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported") |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported") |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")
| Discord  |  In-game only  |   ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")   |  ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported") |
| TeamSpeak  |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")   |  ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |   ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |
| Ventrilo  | ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported")  |   ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |  [Has a bug dating 2012](http://forum.ventrilo.com/showthread.php?t=61203 "Has a bug dating 2012")  |   ![Supported](https://i.imgur.com/GOuQvrh.png "Supported")  |
| Skype  | ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported")  |  Hard-coded Ctrl+M  |  ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported")  |  ![Not supported](https://i.imgur.com/AxsV1yJ.png "Not supported") |

MicSwitch allows you to mute/unmute your system microphone using a predefined system-wide hotkey which will affect any program that uses microphone (no more heavy breathing during Skype conferences, hooray!)
Also it supports configurable mute/unmute sounds(similar to TeamSpeak/Ventrilo) and a configurable overlay with scaling/transparency support. All these features allow you to seamlessly switch between chat apps and use THE SAME input system with overlay and notifications support.

# Features / Bugfixes priority (click to vote or post feature/bug request)
[![Requests](https://feathub.com/iXab3r/MicSwitch?format=svg)](https://feathub.com/iXab3r/MicSwitch)

# Installation
- You can download the latest version of installer here - [download](https://github.com/iXab3r/MicSwitch/releases/latest).
- After initial installation application will periodically check Github for updates

## Features
- Multiple microphones support (useful for streamers) - ALL microphones in your system could be muted/unmuted by a single key press
- System-wide hotkeys (supports mouse XButtons)
- Always-on-top configurable (scale, transparency) Overlay - could be disable if not needed
- Mute/unmute audio notification (with custom audio files support)
- Customizable tray and overlay icons
- Multiple hotkeys support
- Auto-startup (could be Minimized by default)
- Three Audio modes: Push-to-talk, Push-to-mute and Toggle mute
- Overlay visibility could be linked to microphone state, i.e. it will be shown only when Muted/Unmuted
- Auto-updates via Github

## Media
![UI](https://i.imgur.com/Fz0nTZP.png)

### Overlay with configurable size/opacity
![Overlay with configurable size/opacity](https://i.imgur.com/1Jf1RrH.gif)

### Configurable Audio notification when microphone is muted/unmuted
![Configurable Audio notification when microphone is muted/unmuted](https://i.imgur.com/TmvJizg.png) 

### Customizable overlay/tray icons
![Customizable overlay/tray icons](https://i.imgur.com/Bq0yHnK.png)

### Auto-update via Github
![Auto-update via Github](https://i.imgur.com/O4SIuDy.gif)

## How to build application
* I am extensively using [git-submodules](https://git-scm.com/docs/git-submodule "git-submodules") so you may have to run extra commands (git submodule update) if your git-client does not fully support this tech. I would highly recommend to use [Git Extensions](https://gitextensions.github.io/ "Git Extensions") which is awesome, free and open-source and makes submodules integration seamless
* The main "catch-up-moment" is that you need to run InitSymlinks.cmd before building an application - this is due to the fact that git symlinks are not supported on some older versions of Windows and I am using them to create links to submodules
* I am usually using [Jetbrains Rider](https://www.jetbrains.com/rider/ "Jetbrains Rider") so there MAY be some issues if you are using Microsoft Visual Studio, although I am trying to keep things compatible

### Build from command line
1. git clone https://github.com/iXab3r/MicSwitch.git
2. cd MicSwitch
3. git submodule init
5. git submodule update --checkout
5. InitSymlinks.cmd
6. dotnet build

That's it. Working version will be in **Sources/bin** folder

## Contacts
- Feel free to contact me via PM in Discord *Xab3r#3780* or [Reddit](https://www.reddit.com/user/Xab3r) 
- [Discord chat](https://discord.gg/BExRm22 "Discord chat")
- [Issues tracker](https://github.com/iXab3r/MicSwitch/issues)
