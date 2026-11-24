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
    private const string TokenPrefKey = "WebGLDeployer_GitHubToken";
    private static string _statusMessage = "";
    private static bool _isDeploying = false;
    private static string _tokenInput = "";

    [MenuItem("Build/🚀 Build and Deploy WebGL to GitHub Pages", false, 1)]
    public static void BuildAndDeploy()
    {
        ExecuteDeploy(interactive: true, rebuild: true);
    }

    [MenuItem("Build/WebGL Deploy Window", false, 2)]
    public static void ShowWindow()
    {
        var window = GetWindow<WebGLDeployer>("WebGL Deploy");
        window.minSize = new Vector2(460, 360);
        window.Show();
    }

    private void OnEnable()
    {
        _tokenInput = WindowsReleaseDeployer.GetSavedToken();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("WebGL GitHub Pages Deployer", EditorStyles.boldLabel);
        GUILayout.Space(5);

        bool webglSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        if (!webglSupported)
        {
            EditorGUILayout.HelpBox(
                "Модуль WebGL Build Support не установлен для этой версии Unity!\n\n" +
                "Установите его через Unity Hub:\n" +
                "Installs -> 6000.3.27f1 -> ⋮ (три точки) -> Add modules -> Web Build Support.",
                MessageType.Error
            );
        }

        string remoteUrl = GetRemoteOriginUrl();
        string pagesUrl = GetGitHubPagesUrl(remoteUrl);

        EditorGUILayout.LabelField("Remote URL:", string.IsNullOrEmpty(remoteUrl) ? "(origin not configured)" : remoteUrl);
        EditorGUILayout.LabelField("Target Branch:", TargetBranch);
        EditorGUILayout.LabelField("Live Game URL:", string.IsNullOrEmpty(pagesUrl) ? "(unknown)" : pagesUrl);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("GitHub Authentication (Personal Access Token)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        string input = EditorGUILayout.PasswordField("GitHub Token:", _tokenInput);
        if (input != _tokenInput)
        {
            _tokenInput = input;
            WindowsReleaseDeployer.SaveToken(_tokenInput);
        }
        if (GUILayout.Button("Save", GUILayout.Width(60)))
        {
            WindowsReleaseDeployer.SaveToken(_tokenInput);
            GUI.FocusControl(null);
            _statusMessage = "Токен сохранён!";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Для пуша в GitHub нужен токен (PAT) с галочкой 'repo'. Обычный пароль аккаунта GitHub не принимает.",
            MessageType.None
        );

        if (GUILayout.Button("🔑 Create Token on GitHub (opens pre-filled link)"))
        {
            Application.OpenURL("https://github.com/settings/tokens/new?scopes=repo&description=UnityDeploy");
        }

        GUILayout.Space(15);
        GUI.enabled = !_isDeploying && !string.IsNullOrEmpty(remoteUrl);

        string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "WebGL");
        bool hasExistingBuild = File.Exists(Path.Combine(buildPath, "index.html"));

        if (GUILayout.Button("🚀 Build & Deploy to GitHub Pages", GUILayout.Height(38)))
        {
            ExecuteDeploy(interactive: true, rebuild: true);
        }

        if (hasExistingBuild)
        {
            GUILayout.Space(5);
            if (GUILayout.Button("⚡ Push Existing Build to GitHub Pages (Fast)", GUILayout.Height(30)))
            {
                ExecuteDeploy(interactive: true, rebuild: false);
            }
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

    public static void ExecuteDeploy(bool interactive, bool rebuild)
    {
        if (_isDeploying)
        {
            Debug.LogWarning("[WebGLDeployer] Deployment already in progress.");
            return;
        }

        // Run self-check assertions before proceeding
        SelfCheck();

        bool webglSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        if (rebuild && !webglSupported)
        {
            string err = "Модуль WebGL Build Support не установлен для этой версии Unity!\n\n" +
                         "Установите его через Unity Hub:\n" +
                         "Installs -> 6000.3.27f1 -> ⋮ (три точки) -> Add modules -> Web Build Support.";
            _statusMessage = err;
            Debug.LogError($"[WebGLDeployer] {err}");
            if (interactive) EditorUtility.DisplayDialog("WebGL Module Missing", err, "OK");
            return;
        }

        string remoteUrl = GetRemoteOriginUrl();
        if (string.IsNullOrEmpty(remoteUrl))
        {
            string err = "Git remote 'origin' is not configured. Please set a remote URL before deploying.";
            _statusMessage = err;
            if (interactive) EditorUtility.DisplayDialog("Deployment Error", err, "OK");
            return;
        }

        string token = WindowsReleaseDeployer.GetSavedToken();
        if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(_tokenInput))
        {
            token = _tokenInput.Trim();
            WindowsReleaseDeployer.SaveToken(token);
        }
        if (string.IsNullOrEmpty(token) && remoteUrl.StartsWith("https://"))
        {
            ShowWindow();
            string msg = "Для первой отправки на GitHub укажите Personal Access Token (PAT) в окне WebGL Deploy.\n\n" +
                         "Нажмите кнопку 'Create Token on GitHub' в окне, создайте токен с галочкой 'repo' и вставьте его.";
            _statusMessage = msg;
            if (interactive) EditorUtility.DisplayDialog("GitHub Token Required", msg, "OK");
            return;
        }

        string pagesUrl = GetGitHubPagesUrl(remoteUrl);
        string projectPath = Directory.GetCurrentDirectory();
        string buildPath = Path.Combine(projectPath, "Builds", "WebGL");

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (rebuild && scenes.Length == 0)
        {
            string err = "No enabled scenes found in EditorBuildSettings. Please add scenes to Build Settings.";
            _statusMessage = err;
            if (interactive) EditorUtility.DisplayDialog("Deployment Error", err, "OK");
            return;
        }

        _isDeploying = true;

        try
        {
            if (rebuild)
            {
                _statusMessage = "Building WebGL...";

                // Configure WebGL player settings for seamless GitHub Pages hosting
                PlayerSettings.WebGL.decompressionFallback = true;
                AssetDatabase.SaveAssets();

                if (Directory.Exists(buildPath))
                {
                    Directory.Delete(buildPath, true);
                }
                Directory.CreateDirectory(buildPath);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };

                Debug.Log($"[WebGLDeployer] Starting WebGL build to: {buildPath}");
                BuildReport report = BuildPipeline.BuildPlayer(options);

                if (report.summary.result != BuildResult.Succeeded || !File.Exists(Path.Combine(buildPath, "index.html")))
                {
                    string err = "WebGL build failed or did not produce index.html. Check Console for errors.";
                    _statusMessage = err;
                    Debug.LogError($"[WebGLDeployer] {err}");
                    if (interactive) EditorUtility.DisplayDialog("Build Error", err, "OK");
                    return;
                }

                Debug.Log("[WebGLDeployer] WebGL build succeeded. Preparing deployment...");
            }

            _statusMessage = "Pushing build to GitHub...";

            // Create .nojekyll so GitHub Pages correctly serves Unity files (e.g. wasm, data)
            File.WriteAllText(Path.Combine(buildPath, ".nojekyll"), "");

            // Build push URL with token if available
            string pushUrl = remoteUrl;
            if (!string.IsNullOrEmpty(token) && remoteUrl.StartsWith("https://"))
            {
                pushUrl = remoteUrl.Replace("https://", $"https://{token}@");
            }

            // shortcut: uses git push -f directly from the build folder into gh-pages branch, upgrade to GitHub Actions if team branch protection is enforced
            DeployBuildFolder(buildPath, pushUrl);

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
            string sanitizedMsg = MaskToken(ex.Message, token);
            _statusMessage = $"Error: {sanitizedMsg}";
            Debug.LogError($"[WebGLDeployer] {sanitizedMsg}");
            if (interactive) EditorUtility.DisplayDialog("Deploy Failed", sanitizedMsg, "OK");
        }
        finally
        {
            _isDeploying = false;
        }
    }

    private static void DeployBuildFolder(string buildPath, string pushUrl)
    {
        string gitDir = Path.Combine(buildPath, ".git");
        try
        {
            RunGit(buildPath, "init");
            RunGit(buildPath, $"checkout -B {TargetBranch}");
            RunGit(buildPath, "add -A");
            string commitMessage = $"Deploy WebGL build - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            RunGit(buildPath, $"commit -m \"{commitMessage}\"");
            RunGit(buildPath, $"remote add origin \"{pushUrl}\"");
            RunGit(buildPath, $"push -f origin {TargetBranch}");
        }
        finally
        {
            if (Directory.Exists(gitDir))
            {
                try
                {
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

    private static string MaskToken(string text, string token)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(text)) return text;
        return text.Replace(token, "ghp_***");
    }

    private static void SelfCheck()
    {
        string httpsTest = GetGitHubPagesUrl("https://github.com/SnipDev1/PhysicsLab42.git");
        Debug.Assert(httpsTest == "https://SnipDev1.github.io/PhysicsLab42/", $"SelfCheck failed on HTTPS: {httpsTest}");

        string sshTest = GetGitHubPagesUrl("git@github.com:SnipDev1/PhysicsLab42.git");
        Debug.Assert(sshTest == "https://SnipDev1.github.io/PhysicsLab42/", $"SelfCheck failed on SSH: {sshTest}");

        string masked = MaskToken("https://ghp_secretToken123@github.com", "ghp_secretToken123");
        Debug.Assert(masked == "https://ghp_***@github.com", "MaskToken failed");

        string gitVer = RunGit(Directory.GetCurrentDirectory(), "--version");
        Debug.Assert(!string.IsNullOrEmpty(gitVer) && gitVer.StartsWith("git version"), "SelfCheck git CLI missing");
    }
}
#endif
