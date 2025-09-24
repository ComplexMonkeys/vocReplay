using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(vocReplay.ReplayableAudioMod), vocReplay.BuildInfo.Name, vocReplay.BuildInfo.Version, vocReplay.BuildInfo.Author, vocReplay.BuildInfo.DownloadLink)]
[assembly: MelonGame("Voice of Cards: Utilities", null)]

[assembly: AssemblyTitle(vocReplay.BuildInfo.Description)]
[assembly: AssemblyDescription(vocReplay.BuildInfo.Description)]
[assembly: AssemblyProduct(vocReplay.BuildInfo.Name)]
[assembly: AssemblyCopyright("Made by " + vocReplay.BuildInfo.Author)]
[assembly: AssemblyVersion(vocReplay.BuildInfo.Version)]
[assembly: AssemblyFileVersion(vocReplay.BuildInfo.Version)]
[assembly: MelonColor()]
