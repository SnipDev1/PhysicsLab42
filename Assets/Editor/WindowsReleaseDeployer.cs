#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// One-click automated builder and publisher for Windows Standalone builds to GitHub Releases.
/// Creates a 64-bit Windows executable, packages it into a distribution zip, and uploads it
/// directly to GitHub Releases with release notes.
/// </summary>
public class WindowsReleaseDeployer : EditorWindow
{
    public const string PrimaryTokenPrefKey = "GitHub_PersonalAccessToken";
    public const string WebGLTokenPrefKey = "WebGLDeployer_GitHubToken";
    public const string DefaultExeName = "PhysicsLab42.exe";
    public const string DefaultZipPrefix = "PhysicsLab42-Windows";

    private static string _statusMessage = "";
    private static bool _isDeploying = false;

    private string _tokenInput = "";
    private string _tagInput = "";
    private string _titleInput = "";
    private string _notesInput = "Автоматический релиз сборки виртуального стенда ЛР №42 под Windows x64.";
    private bool _isDraft = false;
    private bool _isPrerelease = false;

    [MenuItem("Build/📦 Build and Release Windows to GitHub Releases", false, 10)]
    public static void BuildAndRelease()
    {
        ExecuteRelease(interactive: true, rebuild: true);
    }

    [MenuItem("Build/Windows Release Window", false, 11)]
    public static void ShowWindow()
    {
        var window = GetWindow<WindowsReleaseDeployer>("Windows Release");
        window.minSize = new Vector2(480, 520);
        window.Show();
    }

    /// <summary>
    /// Headless entry point for CI/CD or command line:
    /// Unity -batchmode -nographics -executeMethod WindowsReleaseDeployer.BuildAndReleaseHeadless -quit
    /// </summary>
    public static void BuildAndReleaseHeadless()
    {
        ExecuteRelease(interactive: false, rebuild: true);
    }

    private void OnEnable()
    {
        _tokenInput = GetSavedToken();
        if (string.IsNullOrEmpty(_tagInput))
        {
            string ver = PlayerSettings.bundleVersion;
            _tagInput = string.IsNullOrEmpty(ver) ? "v1.0.0" : (ver.StartsWith("v") ? ver : $"v{ver}");
        }
        if (string.IsNullOrEmpty(_titleInput))
        {
            _titleInput = $"PhysicsLab42 {_tagInput}";
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Windows Standalone -> GitHub Releases Deployer", EditorStyles.boldLabel);
        GUILayout.Space(5);

        bool winSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        if (!winSupported)
        {
            EditorGUILayout.HelpBox(
                "Модуль Windows Build Support не установлен для этой версии Unity!\n\n" +
                "Установите его через Unity Hub:\n" +
                "Installs -> 6000.3.27f1 -> ⋮ (три точки) -> Add modules -> Windows Build Support (Mono).",
                MessageType.Error
            );
        }

        string remoteUrl = GetRemoteOriginUrl();
        bool hasRepo = TryParseGitHubRepo(remoteUrl, out string owner, out string repo);

        EditorGUILayout.LabelField("Remote URL:", string.IsNullOrEmpty(remoteUrl) ? "(origin not configured)" : remoteUrl);
        EditorGUILayout.LabelField("Repository:", hasRepo ? $"{owner}/{repo}" : "(invalid GitHub remote)");
        EditorGUILayout.LabelField("Target Architecture:", "Windows 64-bit (x86_64)");

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Release Configuration", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _tagInput = EditorGUILayout.TextField("Tag Name:", _tagInput);
        if (GUILayout.Button("Bump +0.0.1", GUILayout.Width(95)))
        {
            _tagInput = BumpVersion(_tagInput);
            _titleInput = $"PhysicsLab42 {_tagInput}";
        }
        EditorGUILayout.EndHorizontal();

        _titleInput = EditorGUILayout.TextField("Release Title:", _titleInput);

        EditorGUILayout.LabelField("Release Notes (Markdown):");
        _notesInput = EditorGUILayout.TextArea(_notesInput, GUILayout.Height(65));

        EditorGUILayout.BeginHorizontal();
        _isDraft = EditorGUILayout.ToggleLeft("Draft", _isDraft, GUILayout.Width(80));
        _isPrerelease = EditorGUILayout.ToggleLeft("Pre-release", _isPrerelease, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("GitHub Authentication (Personal Access Token)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        string input = EditorGUILayout.PasswordField("GitHub Token:", _tokenInput);
        if (input != _tokenInput)
        {
            _tokenInput = input;
            SaveToken(_tokenInput);
        }
        if (GUILayout.Button("Save", GUILayout.Width(60)))
        {
            SaveToken(_tokenInput);
            GUI.FocusControl(null);
            _statusMessage = "Токен сохранён!";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Для создания релизов и загрузки файлов на GitHub нужен токен (PAT) с правом 'repo'.",
            MessageType.None
        );

        if (GUILayout.Button("🔑 Create Token on GitHub (opens pre-filled link)"))
        {
            Application.OpenURL("https://github.com/settings/tokens/new?scopes=repo&description=UnityWindowsRelease");
        }

        GUILayout.Space(15);
        GUI.enabled = !_isDeploying && hasRepo;

        string buildExePath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows", DefaultExeName);
        bool hasExistingBuild = File.Exists(buildExePath);

        if (GUILayout.Button("📦 Build & Publish Windows Release to GitHub", GUILayout.Height(38)))
        {
            ExecuteRelease(interactive: true, rebuild: true, customTag: _tagInput, customTitle: _titleInput, customNotes: _notesInput, isDraft: _isDraft, isPrerelease: _isPrerelease);
        }

        if (hasExistingBuild)
        {
            GUILayout.Space(5);
            if (GUILayout.Button("⚡ Zip & Publish Existing Build (Fast)", GUILayout.Height(30)))
            {
                ExecuteRelease(interactive: true, rebuild: false, customTag: _tagInput, customTitle: _titleInput, customNotes: _notesInput, isDraft: _isDraft, isPrerelease: _isPrerelease);
            }
        }

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
        }

        if (hasRepo)
        {
            GUILayout.Space(5);
            if (GUILayout.Button("🌐 Open Releases Page in Browser"))
            {
                Application.OpenURL($"https://github.com/{owner}/{repo}/releases");
            }
        }
    }

    public static void ExecuteRelease(
        bool interactive,
        bool rebuild,
        string customTag = null,
        string customTitle = null,
        string customNotes = null,
        bool isDraft = false,
        bool isPrerelease = false)
    {
        if (_isDeploying)
        {
            Debug.LogWarning("[WindowsReleaseDeployer] Deployment already in progress.");
            return;
        }

        bool winSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        if (rebuild && !winSupported)
        {
            string err = "Модуль Windows Build Support не установлен в этой версии Unity.\n" +
                         "Установите его через Unity Hub: Add Modules -> Windows Build Support.";
            _statusMessage = err;
            Debug.LogError($"[WindowsReleaseDeployer] {err}");
            if (interactive) EditorUtility.DisplayDialog("Windows Module Missing", err, "OK");
            return;
        }

        string remoteUrl = GetRemoteOriginUrl();
        if (!TryParseGitHubRepo(remoteUrl, out string owner, out string repo))
        {
            string err = "Git remote 'origin' is not a valid GitHub repository URL.";
            _statusMessage = err;
            Debug.LogError($"[WindowsReleaseDeployer] {err}");
            if (interactive) EditorUtility.DisplayDialog("Configuration Error", err, "OK");
            return;
        }

        string token = GetSavedToken();
        if (string.IsNullOrEmpty(token))
        {
            ShowWindow();
            string msg = "Для создания релиза на GitHub требуется Personal Access Token (PAT) с правом 'repo'.\n\n" +
                         "Укажите токен в открывшемся окне Windows Release.";
            _statusMessage = msg;
            if (interactive) EditorUtility.DisplayDialog("GitHub Token Required", msg, "OK");
            return;
        }

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (rebuild && scenes.Length == 0)
        {
            string err = "No enabled scenes found in EditorBuildSettings. Please add scenes to Build Settings.";
            _statusMessage = err;
            if (interactive) EditorUtility.DisplayDialog("Build Error", err, "OK");
            return;
        }

        string tagName = customTag;
        if (string.IsNullOrEmpty(tagName))
        {
            string ver = PlayerSettings.bundleVersion;
            tagName = string.IsNullOrEmpty(ver) ? "v1.0.0" : (ver.StartsWith("v") ? ver : $"v{ver}");
        }

        string releaseTitle = string.IsNullOrEmpty(customTitle) ? $"PhysicsLab42 {tagName}" : customTitle;
        string releaseNotes = string.IsNullOrEmpty(customNotes)
            ? $"Автоматический релиз сборки виртуального стенда ЛР №42 ({tagName}) под Windows x64."
            : customNotes;

        string projectPath = Directory.GetCurrentDirectory();
        string buildsRoot = Path.Combine(projectPath, "Builds");
        string winBuildDir = Path.Combine(buildsRoot, "Windows");
        string exePath = Path.Combine(winBuildDir, DefaultExeName);
        string zipFileName = $"{DefaultZipPrefix}-{tagName}.zip";
        string zipPath = Path.Combine(buildsRoot, zipFileName);

        _isDeploying = true;
        _statusMessage = "Starting release process...";

        try
        {
            // 1. Build Windows Standalone Player
            if (rebuild)
            {
                if (interactive) EditorUtility.DisplayProgressBar("Windows Release", "Building Windows x64 executable...", 0.2f);

                if (Directory.Exists(winBuildDir))
                {
                    Directory.Delete(winBuildDir, true);
                }
                Directory.CreateDirectory(winBuildDir);

                var buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };

                Debug.Log($"[WindowsReleaseDeployer] Starting build: {exePath}");
                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

                if (report.summary.result != BuildResult.Succeeded || !File.Exists(exePath))
                {
                    string err = "Windows build failed or executable not generated. Check Unity Console for errors.";
                    _statusMessage = err;
                    Debug.LogError($"[WindowsReleaseDeployer] {err}");
                    if (interactive) EditorUtility.DisplayDialog("Build Error", err, "OK");
                    return;
                }

                Debug.Log("[WindowsReleaseDeployer] Windows player built successfully.");
            }
            else if (!File.Exists(exePath))
            {
                string err = $"No existing build found at {exePath}. Please choose 'Build & Publish'.";
                _statusMessage = err;
                if (interactive) EditorUtility.DisplayDialog("Build Missing", err, "OK");
                return;
            }

            // 2. Package into Zip Archive
            if (interactive) EditorUtility.DisplayProgressBar("Windows Release", $"Packaging {zipFileName}...", 0.5f);

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(winBuildDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

            if (!File.Exists(zipPath) || new FileInfo(zipPath).Length == 0)
            {
                string err = "Failed to create distribution zip archive.";
                _statusMessage = err;
                Debug.LogError($"[WindowsReleaseDeployer] {err}");
                if (interactive) EditorUtility.DisplayDialog("Packaging Error", err, "OK");
                return;
            }

            long zipBytes = new FileInfo(zipPath).Length;
            double zipMb = zipBytes / (1024.0 * 1024.0);
            Debug.Log($"[WindowsReleaseDeployer] Created zip archive: {zipFileName} ({zipMb:0.1} MB)");

            // 3. GitHub API: Create/Get Release & Upload Asset
            if (interactive) EditorUtility.DisplayProgressBar("Windows Release", "Connecting to GitHub Releases API...", 0.7f);

            string releaseUrl = PublishToGitHubReleases(
                owner, repo, token, tagName, releaseTitle, releaseNotes, zipPath, zipFileName, isDraft, isPrerelease,
                progressCallback: (phase, pct) =>
                {
                    if (interactive) EditorUtility.DisplayProgressBar("Windows Release", phase, pct);
                });

            _statusMessage = $"Release '{tagName}' published! URL: {releaseUrl}";
            Debug.Log($"[WindowsReleaseDeployer] {_statusMessage}");

            if (interactive)
            {
                bool open = EditorUtility.DisplayDialog(
                    "Release Published Successfully!",
                    $"Windows build was successfully packaged and released on GitHub!\n\n" +
                    $"Repository: {owner}/{repo}\n" +
                    $"Tag: {tagName}\n" +
                    $"Asset: {zipFileName} ({zipMb:0.1} MB)\n\n" +
                    $"Release Page:\n{releaseUrl}",
                    "Open Release Page", "Close"
                );

                if (open && !string.IsNullOrEmpty(releaseUrl))
                {
                    Application.OpenURL(releaseUrl);
                }
            }
        }
        catch (Exception ex)
        {
            string safeMsg = MaskToken(ex.Message, token);
            _statusMessage = $"Error: {safeMsg}";
            Debug.LogError($"[WindowsReleaseDeployer] {safeMsg}\n{ex.StackTrace}");
            if (interactive) EditorUtility.DisplayDialog("Release Failed", safeMsg, "OK");
        }
        finally
        {
            if (interactive) EditorUtility.ClearProgressBar();
            _isDeploying = false;
        }
    }

    private static string PublishToGitHubReleases(
        string owner,
        string repo,
        string token,
        string tagName,
        string title,
        string body,
        string zipPath,
        string assetFileName,
        bool draft,
        bool prerelease,
        Action<string, float> progressCallback)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(15);
        client.DefaultRequestHeaders.Add("User-Agent", "Unity-WindowsReleaseDeployer");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string apiBase = $"https://api.github.com/repos/{owner}/{repo}/releases";

        // 1. Check if release already exists for this tag
        long releaseId = 0;
        string htmlUrl = "";

        string getTagUrl = $"{apiBase}/tags/{Uri.EscapeDataString(tagName)}";
        var tagResp = client.GetAsync(getTagUrl).GetAwaiter().GetResult();
        if (tagResp.IsSuccessStatusCode)
        {
            string existingJson = tagResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            releaseId = ParseReleaseId(existingJson);
            htmlUrl = ParseHtmlUrl(existingJson);
            Debug.Log($"[WindowsReleaseDeployer] Found existing release '{tagName}' (ID: {releaseId})");
        }
        else
        {
            // 2. Create new release
            progressCallback?.Invoke("Creating new GitHub release...", 0.75f);
            var payload = new CreateReleasePayload
            {
                tag_name = tagName,
                target_commitish = "main",
                name = title,
                body = body,
                draft = draft,
                prerelease = prerelease
            };

            string jsonBody = JsonUtility.ToJson(payload);
            using var reqContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var createResp = client.PostAsync(apiBase, reqContent).GetAwaiter().GetResult();
            string createRespStr = createResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!createResp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"GitHub release creation failed ({createResp.StatusCode}): {createRespStr}");
            }

            releaseId = ParseReleaseId(createRespStr);
            htmlUrl = ParseHtmlUrl(createRespStr);
            Debug.Log($"[WindowsReleaseDeployer] Created new release '{tagName}' (ID: {releaseId})");
        }

        if (releaseId == 0)
        {
            throw new InvalidOperationException("Could not determine GitHub release ID.");
        }

        // 3. Remove previous asset with the same name if present
        progressCallback?.Invoke("Checking existing release assets...", 0.8f);
        string assetsListUrl = $"{apiBase}/{releaseId}/assets";
        var assetsResp = client.GetAsync(assetsListUrl).GetAwaiter().GetResult();
        if (assetsResp.IsSuccessStatusCode)
        {
            string assetsJson = assetsResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            long existingAssetId = ParseExistingAssetId(assetsJson, assetFileName);
            if (existingAssetId > 0)
            {
                Debug.Log($"[WindowsReleaseDeployer] Deleting old asset {assetFileName} (ID: {existingAssetId})...");
                string deleteUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/assets/{existingAssetId}";
                client.DeleteAsync(deleteUrl).GetAwaiter().GetResult();
            }
        }

        // 4. Upload zip asset
        progressCallback?.Invoke($"Uploading {assetFileName}...", 0.85f);
        string uploadUrl = $"https://uploads.github.com/repos/{owner}/{repo}/releases/{releaseId}/assets?name={Uri.EscapeDataString(assetFileName)}";

        using var fileStream = File.OpenRead(zipPath);
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        streamContent.Headers.ContentLength = fileStream.Length;

        var uploadResp = client.PostAsync(uploadUrl, streamContent).GetAwaiter().GetResult();
        string uploadRespStr = uploadResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (!uploadResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Asset upload failed ({uploadResp.StatusCode}): {uploadRespStr}");
        }

        Debug.Log($"[WindowsReleaseDeployer] Successfully uploaded asset {assetFileName} to release.");
        return string.IsNullOrEmpty(htmlUrl) ? $"https://github.com/{owner}/{repo}/releases/tag/{tagName}" : htmlUrl;
    }

    public static string GetSavedToken()
    {
        string token = EditorPrefs.GetString(PrimaryTokenPrefKey, "").Trim();
        if (string.IsNullOrEmpty(token))
            token = EditorPrefs.GetString(WebGLTokenPrefKey, "").Trim();
        return token;
    }

    public static void SaveToken(string token)
    {
        token = token?.Trim() ?? "";
        EditorPrefs.SetString(PrimaryTokenPrefKey, token);
        EditorPrefs.SetString(WebGLTokenPrefKey, token);
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

    public static bool TryParseGitHubRepo(string remoteUrl, out string owner, out string repo)
    {
        owner = "";
        repo = "";
        if (string.IsNullOrEmpty(remoteUrl)) return false;

        var match = Regex.Match(remoteUrl.Trim(), @"github\.com[:/]([^/]+)/([^/\.]+)(?:\.git)?");
        if (!match.Success) return false;

        owner = match.Groups[1].Value;
        repo = match.Groups[2].Value;
        return true;
    }

    public static long ParseReleaseId(string json)
    {
        var m = Regex.Match(json, @"""id""\s*:\s*(\d+)");
        return m.Success && long.TryParse(m.Groups[1].Value, out long id) ? id : 0;
    }

    public static string ParseHtmlUrl(string json)
    {
        var m = Regex.Match(json, @"""html_url""\s*:\s*""([^""]+)""");
        return m.Success ? m.Groups[1].Value : "";
    }

    public static long ParseExistingAssetId(string json, string assetName)
    {
        var matches = Regex.Matches(json, @"\{[^{}]*\}");
        foreach (Match match in matches)
        {
            string obj = match.Value;
            if (obj.Contains($"\"{assetName}\""))
            {
                var idMatch = Regex.Match(obj, @"""id""\s*:\s*(\d+)");
                if (idMatch.Success && long.TryParse(idMatch.Groups[1].Value, out long id))
                    return id;
            }
        }
        return 0;
    }

    public static string BumpVersion(string currentTag)
    {
        if (string.IsNullOrEmpty(currentTag)) return "v1.0.0";
        bool hasV = currentTag.StartsWith("v");
        string numPart = hasV ? currentTag.Substring(1) : currentTag;
        string[] parts = numPart.Split('.');

        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int lastNum))
        {
            parts[parts.Length - 1] = (lastNum + 1).ToString();
            string newVer = string.Join(".", parts);
            return hasV ? $"v{newVer}" : newVer;
        }

        return $"{currentTag}.1";
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

    private static string MaskToken(string text, string token)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(text)) return text;
        return text.Replace(token, "ghp_***");
    }

    [Serializable]
    private class CreateReleasePayload
    {
        public string tag_name;
        public string target_commitish;
        public string name;
        public string body;
        public bool draft;
        public bool prerelease;
    }
}
#endif
