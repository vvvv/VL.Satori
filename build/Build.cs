using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.PathConstruction;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Package);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    AbsolutePath DownloadDirectory => RootDirectory / "downloads";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath AssetsDirectory => ArtifactsDirectory / "assets";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DownloadDirectory.CreateOrCleanDirectory();
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
        });

    Target Package => _ => _
        .DependsOn(Clean)
        .Executes(async () =>
        {
            var version = "2025.516.0";
            string[] rids = [ "win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64"];
            foreach (var rid in rids)
            {
                var fileName = $"{rid}.zip";
                var zipFile = DownloadDirectory / fileName;
                await HttpTasks.HttpDownloadFileAsync($"https://github.com/ppy/Satori/releases/download/{version}/{fileName}", zipFile);

                var unzipPath = DownloadDirectory / rid;
                zipFile.UnZipTo(unzipPath);

                foreach (var file in (unzipPath / rid).GetFiles())
                    file.Move(AssetsDirectory / rid / file.Name);
            }

            var nuspecFile = RootDirectory / "VL.Satori.nuspec";
            nuspecFile.CopyToDirectory(ArtifactsDirectory);

            var targetsFile = RootDirectory / "build" / "VL.Satori.targets";
            targetsFile.CopyToDirectory(ArtifactsDirectory / "build");

            NuGetTasks.NuGetPack(s => s
                .SetTargetPath(ArtifactsDirectory / "VL.Satori.nuspec")
                .SetOutputDirectory(ArtifactsDirectory)
                .SetVersion(version)
            );
        });

}
