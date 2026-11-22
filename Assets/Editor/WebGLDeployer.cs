#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class WebGLDeployer : EditorWindow
{
    private const string TargetBranch = "gh-pages";
    private static string _statusMessage = "";
    private static bool _isDeploying = false;

    [MenuItem("Build/🚀 Build and Deploy WebGL to GitHub Pages", false, 1)]
    public static void BuildAndDeploy()
    {
        ExecuteDeploy(interactive: true);
    }

    [MenuItem("Build/WebGL Deploy Window", false, 2)]
    public static void ShowWindow()
    {
        var window = GetWindow<WebGLDeployer>("WebGL Deploy");
        window.minSize = new Vector2(420, 260);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("WebGL GitHub Pages Deployer", EditorStyles.boldLabel);
        GUILayout.Space(5);

        string remoteUrl = GetRemoteOriginUrl();
        string pagesUrl = GetGitHubPagesUrl(remoteUrl);

        EditorGUILayout.LabelField("Remote URL:", string.IsNullOrEmpty(remoteUrl) ? "(origin not configured)" : remoteUrl);
        EditorGUILayout.LabelField("Target Branch:", TargetBranch);
        EditorGUILayout.LabelField("Live Game URL:", string.IsNullOrEmpty(pagesUrl) ? "(unknown)" : pagesUrl);

        GUILayout.Space(15);
        GUI.enabled = !_isDeploying && !string.IsNullOrEmpty(remoteUrl);

        if (GUILayout.Button("🚀 Build & Deploy to GitHub Pages", GUILayout.Height(42)))
        {
            ExecuteDeploy(interactive: true);
        }

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
        }

        if (!string.IsNullOrEmpty(pagesUrl))
        {
            GUILayout.Space(5);
            if (GUILayout.Button("Open Live URL in Browser"))
            {
                Application.OpenURL(pagesUrl);
            }
        }
    }

    public static void ExecuteDeploy(bool interactive)
    {
        if (_isDeploying)
        {
            Debug.LogWarning("[WebGLDeployer] Deployment already in progress.");
            return;
        }

        // Run self-check assertions before proceeding
        SelfCheck();

        string remoteUrl = GetRemoteOriginUrl();
        if (string.IsNullOrEmpty(remoteUrl))
        {
            string err = "Git remote 'origin' is not configured. Please set a remote URL before deploying.";
            _statusMessage = err;
            if (interactive) EditorUtility.DisplayDialog("Deployment Error", err, "OK");
            return;
        }

        string pagesUrl = GetGitHubPagesUrl(remoteUrl);
        string projectPath = Directory.GetCurrentDirectory();
        string buildPath = Path.Combine(projectPath, "Builds", "WebGL");

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            string err = "No enabled scenes found in EditorBuildSettings. Please add scenes to Build Settings.";
            _statusMessage = err;
            if (interactive) EditorUtility.DisplayDialog("Deployment Error", err, "OK");
            return;
        }

        _isDeploying = true;
        _statusMessage = "Building WebGL...";

        try
        {
            // Configure WebGL player settings for seamless GitHub Pages hosting
            PlayerSettings.WebGL.decompressionFallback = true;
            AssetDatabase.SaveAssets();

            if (Directory.Exists(buildPath))
            {
                Directory.Delete(buildPath, true);
            }
            Directory.CreateDirectory(buildPath);

            // Execute WebGL build
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            Debug.Log($"[WebGLDeployer] Starting WebGL build to: {buildPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                string err = $"WebGL build failed: {report.summary.result}. Check Console for errors.";
                _statusMessage = err;
                Debug.LogError($"[WebGLDeployer] {err}");
                if (interactive) EditorUtility.DisplayDialog("Build Error", err, "OK");
                return;
            }

            Debug.Log("[WebGLDeployer] WebGL build succeeded. Preparing deployment...");
            _statusMessage = "Pushing build to GitHub...";

            // Create .nojekyll so GitHub Pages correctly serves Unity files (e.g. wasm, data)
            File.WriteAllText(Path.Combine(buildPath, ".nojekyll"), "");

            // shortcut: uses git push -f directly from the build folder into gh-pages branch, upgrade to GitHub Actions if team branch protection is enforced
            DeployBuildFolder(buildPath, remoteUrl);

            _statusMessage = $"Deployed to '{TargetBranch}'! Available at: {pagesUrl}";
            Debug.Log($"[WebGLDeployer] {_statusMessage}");

            if (interactive)
            {
                bool open = EditorUtility.DisplayDialog(
                    "Deploy Successful!",
                    $"WebGL build was successfully pushed to branch '{TargetBranch}'!\n\n" +
                    $"Game URL: {pagesUrl}\n\n" +
                    "Make sure GitHub Pages is enabled in repository settings:\n" +
                    "(Settings -> Pages -> Source: 'Deploy from a branch' -> Branch: 'gh-pages' -> /root)",
                    "Open Game", "Close"
                );

                if (open && !string.IsNullOrEmpty(pagesUrl))
                {
                    Application.OpenURL(pagesUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error: {ex.Message}";
            Debug.LogException(ex);
            if (interactive) EditorUtility.DisplayDialog("Deploy Failed", ex.Message, "OK");
        }
        finally
        {
            _isDeploying = false;
        }
    }

    private static void DeployBuildFolder(string buildPath, string remoteUrl)
    {
        string gitDir = Path.Combine(buildPath, ".git");
        try
        {
            RunGit(buildPath, "init");
            RunGit(buildPath, $"checkout -B {TargetBranch}");
            RunGit(buildPath, "add -A");
            string commitMessage = $"Deploy WebGL build - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            RunGit(buildPath, $"commit -m \"{commitMessage}\"");
            RunGit(buildPath, $"remote add origin \"{remoteUrl}\"");
            RunGit(buildPath, $"push -f origin {TargetBranch}");
        }
        finally
        {
            if (Directory.Exists(gitDir))
            {
                try
                {
                    // Clean up nested .git to avoid submodule warnings in the workspace
                    RemoveDirectoryReadOnly(gitDir);
                }
                catch (Exception cleanupEx)
                {
                    Debug.LogWarning($"[WebGLDeployer] Could not remove temp .git: {cleanupEx.Message}");
                }
            }
        }
    }

    public static string GetRemoteOriginUrl()
    {
        try
        {
            string output = RunGit(Directory.GetCurrentDirectory(), "remote get-url origin");
            return output.Trim();
        }
        catch
        {
            return "";
        }
    }

    public static string GetGitHubPagesUrl(string remoteUrl)
    {
        if (string.IsNullOrEmpty(remoteUrl)) return "";

        var match = Regex.Match(remoteUrl, @"github\.com[:/]([^/]+)/([^/\.]+)(?:\.git)?");
        if (!match.Success) return "";

        string user = match.Groups[1].Value;
        string repo = match.Groups[2].Value;
        return $"https://{user}.github.io/{repo}/";
    }

    private static string RunGit(string workingDir, string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Failed to start git process.");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments} failed (exit code {process.ExitCode}): {stderr}");
        }

        return stdout;
    }

    private static void RemoveDirectoryReadOnly(string path)
    {
        var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };
        foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
        {
            info.Attributes = FileAttributes.Normal;
        }
        directory.Delete(true);
    }

    private static void SelfCheck()
    {
        // Assert URL parsing handles HTTPS and SSH remote formats
        string httpsTest = GetGitHubPagesUrl("https://github.com/SnipDev1/PhysicsLab42.git");
        Debug.Assert(httpsTest == "https://SnipDev1.github.io/PhysicsLab42/", $"SelfCheck failed on HTTPS: {httpsTest}");

        string sshTest = GetGitHubPagesUrl("git@github.com:SnipDev1/PhysicsLab42.git");
        Debug.Assert(sshTest == "https://SnipDev1.github.io/PhysicsLab42/", $"SelfCheck failed on SSH: {sshTest}");

        // Assert git CLI is available
        string gitVer = RunGit(Directory.GetCurrentDirectory(), "--version");
        Debug.Assert(!string.IsNullOrEmpty(gitVer) && gitVer.StartsWith("git version"), "SelfCheck git CLI missing");
    }
}
#endif
