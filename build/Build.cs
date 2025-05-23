using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.NuGet.NuGetTasks;

[GitHubActions(
    "main",
    GitHubActionsImage.WindowsLatest,
    OnPushBranches = ["main"],
    Lfs = true,
    Submodules = GitHubActionsSubmodules.Recursive,
    InvokedTargets = [nameof(Deploy)],
    ImportSecrets = [nameof(vvvvOrgNugetKey)])]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Pack);

    [Parameter]
    [Secret]
    readonly string vvvvOrgNugetKey;

    string SatoriVersion => "2025.516.0";
    string PackageVersion => "1.0.0-preview";

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

    Target Pack => _ => _
        .DependsOn(Clean)
        .Executes(async () =>
        {
            string[] rids = [ "win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64"];
            foreach (var rid in rids)
            {
                var fileName = $"{rid}.zip";
                var zipFile = DownloadDirectory / fileName;
                await HttpTasks.HttpDownloadFileAsync($"https://github.com/ppy/Satori/releases/download/{SatoriVersion}/{fileName}", zipFile);

                var unzipPath = DownloadDirectory / rid;
                zipFile.UnZipTo(unzipPath);

                foreach (var file in (unzipPath / rid).GetFiles())
                    file.Move(AssetsDirectory / rid / file.Name);
            }

            var nuspecFile = RootDirectory / "VL.Satori.nuspec";
            nuspecFile.CopyToDirectory(ArtifactsDirectory);

            var targetsFile = RootDirectory / "build" / "VL.Satori.targets";
            targetsFile.CopyToDirectory(ArtifactsDirectory / "build");

            NuGetPack(s => s
                .SetTargetPath(ArtifactsDirectory / "VL.Satori.nuspec")
                .SetOutputDirectory(ArtifactsDirectory)
                .SetVersion(PackageVersion)
            );
        });

    Target Deploy => _ => _
        .DependsOn(Pack)
        .Requires(() => vvvvOrgNugetKey)
        .Executes(() =>
        {
            foreach (var file in ArtifactsDirectory.GetFiles("*.nupkg"))
            {
                NuGetPush(_ => _
                    .SetTargetPath(file)
                    .SetApiKey(vvvvOrgNugetKey)
                    .SetSource("nuget.org"));
            }
        });
}
