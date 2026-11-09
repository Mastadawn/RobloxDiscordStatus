# Roblox Discord Status

Shows what you're doing on Roblox as your Discord status, including what you're building in Roblox Studio.

It works by reading the log files Roblox and Studio already write on your PC, so there's nothing to install inside Roblox and nothing that touches your account.

## What it shows

| You're... | Your status |
| --- | --- |
| In a game | The game's name and icon, your avatar in the corner, how many people are playing, a timer, and **Join** / **View Game** buttons |
| In Studio with a place open | **Roblox Studio**, whether you're *Building in*, *Scripting in* or *Testing* the game, and how many AI agents are connected |
| In Studio on the start page | **Roblox Studio** · No game open |
| On the Roblox home page | **Roblox** · No game open |
| Not running Roblox at all | Nothing, your status gets cleared |

Hover over the pictures for a bit more: the game icon shows who owns the game, and in Studio your avatar shows whether it's a Team Create session.

Private games you're still working on show their real name too, not "[TITLE UNAVAILABLE]".

## Getting started

1. Download `RobloxDiscordStatus.exe` from the [Releases](../../releases) page and put it somewhere it can stay (not your Downloads folder, if you tend to clear it out).
2. Make sure the Discord **desktop app** is open (the browser version can't show statuses from other programs).
3. Run the .exe. That's it.

It lives in your **system tray** (the little icons by the clock, you might need to click the ^ arrow to see it):

- **Double-click** the icon to open the window.
- **Right-click** it for **Open**, **Settings**, **Start with Windows**, and **Quit**.
- **Closing the window doesn't stop the app.** It keeps your status going in the background. Use **Quit** from the tray menu to actually stop it, which also clears your status.

It turns on **Start with Windows** the first time you run it, so it's always there when you log in (tucked away in the tray, no window popping up). You can switch that off in Settings or the tray menu, or in Task Manager's Startup tab.

## The window

The top shows a preview of your Discord status, the same way other people see it, plus whether the app is connected to Discord.

Under **Settings** you can change:

- **Roblox username**: leave it empty and the app figures it out from your logs, or just type it in.
- **Discord App ID**: the name of this Discord app is what shows at the top of your status. Want your own name there? Make an app at [discord.com/developers/applications](https://discord.com/developers/applications) and paste its Application ID here.
- **Show Join and View Game buttons**, **Show how many people are playing**, and **Start when Windows starts**.

Hit **Save** and the changes kick in straight away. They're kept in `%APPDATA%\RobloxDiscordStatus\config.json`.

**Recent activity** at the bottom shows what the app has been doing, which is handy if something isn't showing up the way you expect.

## Good to know

- **You can't see your own buttons.** Discord only shows them to other people.
- **Join might not land in your exact server.** The button asks Roblox for your server, but Roblox sometimes ignores that and puts people in a different server of the same game.
- **"Agents connected" means connected, not working.** The app counts how many copies of `StudioMCP.exe` (what AI agents use to talk to Studio) are running. Roblox doesn't log what agents actually do, so there's no way to tell when one is busy.
- **Script names aren't shown.** Studio doesn't write the open script's name anywhere the app can read it.
- **Only one copy runs at a time.** Opening the .exe again just brings up the window of the one that's already running.

## Building it yourself

You'll need the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```
cd src
dotnet run
```

To make the single .exe that goes on the Releases page:

```
cd src
dotnet publish -c Release
```

It ends up in `src/bin/Release/net8.0-windows/win-x64/publish/`.

## How the code is laid out

| File | What it does |
| --- | --- |
| `Program.cs` | Starts the app, and makes sure only one copy runs |
| `TrayApp.cs` | The tray icon and its menu. Starts the background work and owns the window |
| `StatusService.cs` | The background loop that watches Roblox and keeps your Discord status up to date |
| `MainWindow.cs` | The window with the status preview, settings and recent activity |
| `StatusCard.cs` | The Discord-style status preview at the top of the window |
| `Theme.cs` | Colors, fonts, the toggle switches and rounded boxes |
| `RobloxLogs.cs` | Reads the Roblox and Studio logs and keeps track of what you're doing |
| `RobloxApi.cs` | Looks up game names, icons, player counts and avatars on Roblox's website |
| `StatusBuilder.cs` | Turns all of that into what Discord actually shows |
| `RunningPrograms.cs` | Checks whether Roblox, Studio and AI agents are running |
| `UsernameFinder.cs` | Figures out your Roblox username from your logs |
| `StartWithWindows.cs` | Turns starting with Windows on and off |
| `Settings.cs` | Loads and saves your settings |
| `Log.cs` | Keeps the list of recent activity |

Talking to Discord is handled by [discord-rpc-csharp](https://github.com/Lachee/discord-rpc-csharp).

## License

MIT, see [LICENSE](LICENSE).
