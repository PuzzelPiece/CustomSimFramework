# Custom Sim Framework

An [Erenshor](https://store.steampowered.com/app/2382520/Erenshor/) mod that
opens up the simulated players to custom content. Add your own named sims with
personalities and dialogue, new lines for every generated sim, ambient zone
chatter, and guild chat conversations, all through JSON content packs.

- **Install the mod** from the Github releases or from [Thunderstore](https://thunderstore.io/c/erenshor/) once Thunderstore approves it.
- **Download Pack Studio**, the GUI pack editor, from the
  [Releases page](https://github.com/PuzzelPiece/CustomSimFramework/releases/latest).
  No install needed, just download `PackStudio.exe` and double click it.
  Windows SmartScreen may warn about an unknown publisher the first time.
  That's normal for small unsigned tools, pick "More info" then "Run anyway".

## What's in this repo

- `Code/` is the mod itself (BepInEx plugin). The example pack JSONs in
  `Code/Packs/Paul/` are embedded into the DLL at build time.
- `Tool/` is Pack Studio, a code only WPF app.
- Hand authoring packs instead of using Pack Studio? The JSON schema guide is
  [Code/Packs/README.md](Code/Packs/README.md), and
  [Code/Packs/CATEGORY_REFERENCE.md](Code/Packs/CATEGORY_REFERENCE.md) maps
  where every dialogue category is actually used in the game.

Both projects target .NET Framework 4.8 and build with MSBuild. The mod
references the game's `Assembly-CSharp.dll` and BepInEx 5.4.x, so you'll need
to point the reference paths in `Code/CustomSimFramework.csproj` at your own
Erenshor install to build it.
