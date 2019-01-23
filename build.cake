//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

#tool "GitReleaseManager"
#tool "GitVersion.CommandLine"
#tool "nuget:?package=vswhere"
#tool "nuget:?package=OpenCover"
#tool "nuget:?package=ReportGenerator"
#tool "nuget:?package=xunit.runner.console"

#addin nuget:?package=Cake.Coverlet

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
if (string.IsNullOrWhiteSpace(target))
{
    target = "Default";
}

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Should MSBuild & GitLink treat any errors as warnings?
var treatWarningsAsErrors = false;

// Build configuration
var local = BuildSystem.IsLocalBuild;
var isRunningOnUnix = IsRunningOnUnix();
var isRunningOnWindows = IsRunningOnWindows();

var isRunningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;
var isRepository = StringComparer.OrdinalIgnoreCase.Equals("reactiveui/punchclock", AppVeyor.Environment.Repository.Name);

var isReleaseBranch = StringComparer.OrdinalIgnoreCase.Equals("master", AppVeyor.Environment.Repository.Branch);
var isTagged = AppVeyor.Environment.Repository.Tag.IsTag;

var configuration = "Release";

var githubOwner = "reactiveui";
var githubRepository = "punchclock";
var githubUrl = string.Format("https://github.com/{0}/{1}", githubOwner, githubRepository);

//var msBuildPath = VSWhereLatest().CombineWithFilePath("./MSBuild/15.0/Bin/MSBuild.exe");

// Version
var gitVersion = GitVersion();
var majorMinorPatch = gitVersion.MajorMinorPatch;
var informationalVersion = gitVersion.InformationalVersion;
var nugetVersion = gitVersion.NuGetVersion;

// Artifacts
// Artifacts
var artifactDirectory = "./artifacts/";
var testsArtifactDirectory = artifactDirectory + "tests/";
var binariesArtifactDirectory = artifactDirectory + "binaries/";
var packagesArtifactDirectory = artifactDirectory + "packages/";
var packageWhitelist = new[] { "Punchclock" };

// Test coverage files.
var testCoverageOutputFile = testsArtifactDirectory + "OpenCover.xml";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup((context) =>
{
    Information("Building version {0} of punchclock. (isTagged: {1})", informationalVersion, isTagged);
    
    CreateDirectory(artifactDirectory);
    CleanDirectories(artifactDirectory);
    CreateDirectory(testsArtifactDirectory);
    CreateDirectory(binariesArtifactDirectory);
    CreateDirectory(packagesArtifactDirectory);
});

Teardown((context) =>
{
    // Executed AFTER the last task.
});

//////////////////////////////////////////////////////////////////////
// HELPER METHODS
//////////////////////////////////////////////////////////////////////
Action<string, string, bool> Build = (projectFile, packageOutputPath, forceUseFullDebugType) =>
{
    Information("Building {0} using {1}, forceUseFullDebugType = {2}", projectFile, "", forceUseFullDebugType);


        var msBuildSettings = new MSBuildSettings() {
                //ToolPath = msBuildPath,
                ArgumentCustomization = args => args.Append("/bl:punchclock.binlog"),
                MaxCpuCount = 0,
                Restore = true
            }
            .WithTarget("restore;build;pack")
            .WithProperty("PackageOutputPath",  MakeAbsolute(Directory(artifactDirectory)).ToString())
            .WithProperty("TreatWarningsAsErrors", treatWarningsAsErrors.ToString())
            .SetConfiguration(configuration)
            // Due to https://github.com/NuGet/Home/issues/4790 and https://github.com/NuGet/Home/issues/4337 we
            // have to pass a version explicitly
            .WithProperty("Version", nugetVersion.ToString())
            .SetVerbosity(Verbosity.Normal)
            .UseToolVersion(MSBuildToolVersion.VS2017)
            .SetNodeReuse(false);

        if (forceUseFullDebugType)
        {
            msBuildSettings = msBuildSettings.WithProperty("DebugType",  "full");
            msBuildSettings.SetConfiguration("Debug");
        }

        if (!string.IsNullOrWhiteSpace(packageOutputPath))
        {
            msBuildSettings = msBuildSettings.WithProperty("PackageOutputPath",  MakeAbsolute(Directory(packageOutputPath)).ToString().Quote());
        }

        MSBuild(projectFile, msBuildSettings);
    };

Action Clean = () =>
{
    CleanDirectories(string.Format("./src/**/obj/{0}", "Release"));
    CleanDirectories(string.Format("./src/**/bin/{0}", "Release"));
    CleanDirectories(string.Format("./src/**/obj/{0}", "Debug"));
    CleanDirectories(string.Format("./src/**/bin/{0}", "Debug"));
};

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
  .Does(() =>
  {
      Clean();
  });

Task("Build")
    .IsDependentOn("Clean")
    .Does (() =>
{
    Build("./src/Punchclock/punchclock.csproj", null, false);
});

Task("RunUnitTests")
    .IsDependentOn("Build")
    .Does(() =>
{

    var testSettings = new DotNetCoreTestSettings {
        NoBuild = true,
        Configuration = "Debug",
        ResultsDirectory = testsArtifactDirectory,
        Logger = $"trx;LogFileName=testresults.trx",
    };

    var coverletSettings = new CoverletSettings {
        CollectCoverage = true,
        CoverletOutputFormat = CoverletOutputFormat.opencover,
        CoverletOutputDirectory = testsArtifactDirectory + "Report/",
        CoverletOutputName = testCoverageOutputFile
    };

    var projectName = "./src/Punchclock.Tests/Punchclock.Tests.csproj";
    Build(projectName, null, true);
    DotNetCoreTest(projectName, testSettings, coverletSettings);

}).ReportError(exception =>
{
    //var apiApprovals = GetFiles("./**/ApiApprovalTests.*");
   // CopyFiles(apiApprovals, artifactDirectory);
});


Task("PublishPackages")
    .IsDependentOn("Build")
    .IsDependentOn("RunUnitTests")
    .WithCriteria(() => !local)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isRepository)
    .Does (() =>
{
    if (isReleaseBranch && !isTagged)
    {
        Information("Packages will not be published as this release has not been tagged.");
        return;
    }

    // Resolve the API key.
    var apiKey = EnvironmentVariable("NUGET_APIKEY");
    if (string.IsNullOrEmpty(apiKey))
    {
        throw new Exception("The NUGET_APIKEY environment variable is not defined.");
    }

    var source = EnvironmentVariable("NUGET_SOURCE");
    if (string.IsNullOrEmpty(source))
    {
        throw new Exception("The NUGET_SOURCE environment variable is not defined.");
    }

    // only push whitelisted packages.
    foreach(var package in packageWhitelist)
    {
        // only push the package which was created during this build run.
        var packagePath = artifactDirectory + File(string.Concat(package, ".", nugetVersion, ".nupkg"));

        // Push the package.
        NuGetPush(packagePath, new NuGetPushSettings {
            Source = source,
            ApiKey = apiKey
        });
    }
});

Task("CreateRelease")
    .IsDependentOn("Build")
    .IsDependentOn("RunUnitTests")
    .WithCriteria(() => !local)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isRepository)
    .WithCriteria(() => isReleaseBranch)
    .WithCriteria(() => !isTagged)
    .Does (() =>
{
    var username = EnvironmentVariable("GITHUB_USERNAME");
    if (string.IsNullOrEmpty(username))
    {
        throw new Exception("The GITHUB_USERNAME environment variable is not defined.");
    }

    var token = EnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(token))
    {
        throw new Exception("The GITHUB_TOKEN environment variable is not defined.");
    }

    GitReleaseManagerCreate(username, token, githubOwner, githubRepository, new GitReleaseManagerCreateSettings {
        Milestone         = majorMinorPatch,
        Name              = majorMinorPatch,
        Prerelease        = true,
        TargetCommitish   = "master"
    });
});

Task("PublishRelease")
    .IsDependentOn("Build")
    .IsDependentOn("RunUnitTests")
    .WithCriteria(() => !local)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isRepository)
    .WithCriteria(() => isReleaseBranch)
    .WithCriteria(() => isTagged)
    .Does (() =>
{
    var username = EnvironmentVariable("GITHUB_USERNAME");
    if (string.IsNullOrEmpty(username))
    {
        throw new Exception("The GITHUB_USERNAME environment variable is not defined.");
    }

    var token = EnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(token))
    {
        throw new Exception("The GITHUB_TOKEN environment variable is not defined.");
    }

    // only push whitelisted packages.
    foreach(var package in packageWhitelist)
    {
        // only push the package which was created during this build run.
        var packagePath = artifactDirectory + File(string.Concat(package, ".", nugetVersion, ".nupkg"));

        GitReleaseManagerAddAssets(username, token, githubOwner, githubRepository, majorMinorPatch, packagePath);
    }

    GitReleaseManagerClose(username, token, githubOwner, githubRepository, majorMinorPatch);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("CreateRelease")
    .IsDependentOn("PublishPackages")
    .IsDependentOn("PublishRelease")
    .Does (() =>
{
});


//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
