using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class DiceBuild
{
    private const string DefaultOutDir = "Builds/Windows/DiceDemo";
    private const string DefaultExeName = "DiceDemo.exe";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Dice/Build Windows (x64)")] 
    public static void BuildWindows()
    {
        Build(false);
    }

    [MenuItem("Tools/Dice/Build Windows (x64) - Development")] 
    public static void BuildWindowsDev()
    {
        Build(true);
    }

    private static void Build(bool development)
    {
        // Collect scenes from Build Settings; if none, fallback to SampleScene
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToList();

        if (scenes.Count == 0)
        {
            if (File.Exists(SampleScenePath))
            {
                scenes.Add(SampleScenePath);
                UnityEngine.Debug.Log("DiceBuild: BuildSettings empty; falling back to SampleScene.");
            }
            else
            {
                throw new System.Exception("No scenes in Build Settings and SampleScene not found.");
            }
        }

        // Ensure output directory
        Directory.CreateDirectory(DefaultOutDir);

        var options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None;
        var buildPlayerOptions = new BuildPlayerOptions
        {
            target = BuildTarget.StandaloneWindows64,
            scenes = scenes.ToArray(),
            locationPathName = Path.Combine(DefaultOutDir, DefaultExeName),
            options = options
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception($"Build failed: {report.summary.result} - {report.summary.totalErrors} errors");
        }
        else
        {
            UnityEngine.Debug.Log($"Build succeeded: {buildPlayerOptions.locationPathName}\nSize: {report.summary.totalSize / (1024f * 1024f):F1} MB");
            EditorUtility.RevealInFinder(buildPlayerOptions.locationPathName);
        }
    }
}

