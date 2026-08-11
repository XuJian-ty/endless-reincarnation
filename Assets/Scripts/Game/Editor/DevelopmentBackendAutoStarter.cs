using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
internal static class DevelopmentBackendAutoStarter
{
    private const string MenuRoot = "开发后端/";
    private const string AutoStartMenuPath = MenuRoot + "Play 时自动启动";
    private const string AutoStartPreferenceKey = "EndlessReincarnation.AutoStartDevelopmentBackends";
    private const int ProcessTimeoutMilliseconds = 600000;
    private const int DockerStartupTimeoutMilliseconds = 120000;
    private const int BackendHealthTimeoutMilliseconds = 180000;

    private static Task<BackendOperationResult> currentOperation;
    private static BackendOperation currentOperationType;
    private static bool resumePlayAfterStartup;
    private static bool allowNextPlayRequest;

    static DevelopmentBackendAutoStarter()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += PollOperation;
    }

    [MenuItem(MenuRoot + "启动全部后端", priority = 1)]
    private static void StartAllBackends()
    {
        BeginStartup(false);
    }

    [MenuItem(MenuRoot + "停止全部后端", priority = 2)]
    private static void StopAllBackends()
    {
        if (IsOperationRunning())
        {
            Debug.LogWarning("[开发后端] 当前已有后端操作正在执行，请稍候。");
            return;
        }

        string projectRoot = GetProjectRoot();
        currentOperationType = BackendOperation.Stop;
        currentOperation = Task.Run(() => StopComposeServices(projectRoot));
        Debug.Log("[开发后端] 正在停止 Docker Compose 服务……");
    }

    [MenuItem(AutoStartMenuPath, priority = 20)]
    private static void ToggleAutoStart()
    {
        bool enabled = !EditorPrefs.GetBool(AutoStartPreferenceKey, true);
        EditorPrefs.SetBool(AutoStartPreferenceKey, enabled);
        Menu.SetChecked(AutoStartMenuPath, enabled);
        Debug.Log($"[开发后端] Play 时自动启动已{(enabled ? "开启" : "关闭")}。");
    }

    [MenuItem(AutoStartMenuPath, true)]
    private static bool ValidateAutoStart()
    {
        Menu.SetChecked(AutoStartMenuPath, EditorPrefs.GetBool(AutoStartPreferenceKey, true));
        return true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode ||
            !EditorPrefs.GetBool(AutoStartPreferenceKey, true))
        {
            return;
        }

        if (allowNextPlayRequest)
        {
            allowNextPlayRequest = false;
            return;
        }

        if (AreBackendsHealthy())
        {
            return;
        }

        EditorApplication.isPlaying = false;
        BeginStartup(true);
    }

    private static void BeginStartup(bool resumePlay)
    {
        if (AreBackendsHealthy())
        {
            Debug.Log("[开发后端] 两个后端均已运行。");
            if (resumePlay)
            {
                allowNextPlayRequest = true;
                EditorApplication.isPlaying = true;
            }

            return;
        }

        resumePlayAfterStartup |= resumePlay;
        if (IsOperationRunning())
        {
            Debug.Log("[开发后端] 后端正在启动，Unity 将在服务就绪后进入 Play。");
            return;
        }

        string projectRoot = GetProjectRoot();
        currentOperationType = BackendOperation.Start;
        currentOperation = Task.Run(() => StartComposeServices(projectRoot));
        Debug.Log("[开发后端] 正在启动 Docker Desktop、MySQL 和两个后端；服务就绪后将继续进入 Play……");
    }

    private static void PollOperation()
    {
        if (currentOperation == null || !currentOperation.IsCompleted)
        {
            return;
        }

        BackendOperationResult result;
        try
        {
            result = currentOperation.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            result = BackendOperationResult.Failure(exception.GetBaseException().Message);
        }

        BackendOperation completedOperation = currentOperationType;
        currentOperation = null;

        if (!result.Success)
        {
            resumePlayAfterStartup = false;
            Debug.LogError($"[开发后端] 操作失败：{result.Message}");
            EditorUtility.DisplayDialog("开发后端启动失败", result.Message, "确定");
            return;
        }

        Debug.Log($"[开发后端] {result.Message}");
        if (completedOperation != BackendOperation.Start || !resumePlayAfterStartup)
        {
            return;
        }

        resumePlayAfterStartup = false;
        allowNextPlayRequest = true;
        EditorApplication.isPlaying = true;
    }

    private static BackendOperationResult StartComposeServices(string projectRoot)
    {
        string dockerExecutable = FindDockerExecutable();
        BackendOperationResult dockerResult = EnsureDockerReady(dockerExecutable);
        if (!dockerResult.Success)
        {
            return dockerResult;
        }

        string composeFile = Path.Combine(projectRoot, "docker-compose.yml");
        if (!File.Exists(composeFile))
        {
            return BackendOperationResult.Failure($"未找到 Docker Compose 配置：{composeFile}");
        }

        if (!TryCreateAsciiProjectPath(projectRoot, out string composeProjectRoot, out string temporaryDrive, out string mappingError))
        {
            return BackendOperationResult.Failure(mappingError);
        }

        ProcessResult composeResult;
        try
        {
            string composeProjectFile = Path.Combine(composeProjectRoot, "docker-compose.yml");
            string composeArguments =
                $"compose --project-directory {QuoteArgument(composeProjectRoot)} -f {QuoteArgument(composeProjectFile)} up -d --build";
            composeResult = RunProcess(dockerExecutable, composeArguments, ProcessTimeoutMilliseconds);
        }
        finally
        {
            RemoveTemporaryDriveMapping(temporaryDrive);
        }

        if (composeResult.ExitCode != 0)
        {
            return BackendOperationResult.Failure(
                "Docker Compose 启动失败。请确认 5076、5086、5087、5088、3306 端口未被手动启动的旧服务占用。\n\n" +
                Tail(composeResult.Output, 4000));
        }

        DateTime deadline = DateTime.UtcNow.AddMilliseconds(BackendHealthTimeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (AreBackendsHealthy())
            {
                return BackendOperationResult.Successful("MySQL、SocialBackend 和 OnlineDungeonServer 均已就绪。");
            }

            Thread.Sleep(1000);
        }

        return BackendOperationResult.Failure(
            "容器已经启动，但两个后端未在限定时间内通过健康检查。可在终端运行 docker compose logs 查看原因。");
    }

    private static BackendOperationResult StopComposeServices(string projectRoot)
    {
        string dockerExecutable = FindDockerExecutable();
        ProcessResult infoResult = RunProcess(dockerExecutable, "info", 10000);
        if (infoResult.ExitCode != 0)
        {
            return BackendOperationResult.Successful("Docker 当前未运行，无需停止后端。");
        }

        if (!TryCreateAsciiProjectPath(projectRoot, out string composeProjectRoot, out string temporaryDrive, out string mappingError))
        {
            return BackendOperationResult.Failure(mappingError);
        }

        ProcessResult stopResult;
        try
        {
            string composeFile = Path.Combine(composeProjectRoot, "docker-compose.yml");
            string composeArguments =
                $"compose --project-directory {QuoteArgument(composeProjectRoot)} -f {QuoteArgument(composeFile)} stop";
            stopResult = RunProcess(dockerExecutable, composeArguments, 120000);
        }
        finally
        {
            RemoveTemporaryDriveMapping(temporaryDrive);
        }

        return stopResult.ExitCode == 0
            ? BackendOperationResult.Successful("Docker Compose 后端已停止。")
            : BackendOperationResult.Failure("停止后端失败。\n\n" + Tail(stopResult.Output, 4000));
    }

    private static BackendOperationResult EnsureDockerReady(string dockerExecutable)
    {
        if (RunProcess(dockerExecutable, "info", 10000).ExitCode == 0)
        {
            return BackendOperationResult.Successful(string.Empty);
        }

        string dockerDesktop = FindDockerDesktopExecutable();
        if (string.IsNullOrEmpty(dockerDesktop))
        {
            return BackendOperationResult.Failure("未找到 Docker Desktop。请先安装 Docker Desktop，再点击 Unity 的 Play。");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dockerDesktop,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception exception)
        {
            return BackendOperationResult.Failure($"无法启动 Docker Desktop：{exception.Message}");
        }

        DateTime deadline = DateTime.UtcNow.AddMilliseconds(DockerStartupTimeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(2000);
            if (RunProcess(dockerExecutable, "info", 10000).ExitCode == 0)
            {
                return BackendOperationResult.Successful(string.Empty);
            }
        }

        return BackendOperationResult.Failure("Docker Desktop 在两分钟内没有完成启动，请检查 Docker Desktop 状态。");
    }

    private static bool AreBackendsHealthy()
    {
        return IsHealthEndpointReady("http://127.0.0.1:5076/api/health") &&
               IsHealthEndpointReady("http://127.0.0.1:5086/api/health");
    }

    private static bool IsHealthEndpointReady(string url)
    {
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Proxy = null;
            request.Timeout = 500;
            request.ReadWriteTimeout = 500;
            using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            int statusCode = (int)response.StatusCode;
            return statusCode >= 200 && statusCode < 300;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessResult RunProcess(string fileName, string arguments, int timeoutMilliseconds)
    {
        StringBuilder output = new StringBuilder();
        object outputLock = new object();

        try
        {
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            process.OutputDataReceived += (_, eventArgs) => AppendOutput(output, outputLock, eventArgs.Data);
            process.ErrorDataReceived += (_, eventArgs) => AppendOutput(output, outputLock, eventArgs.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                process.Kill();
                return new ProcessResult(-1, "命令执行超时。\n" + output);
            }

            process.WaitForExit();
            return new ProcessResult(process.ExitCode, output.ToString());
        }
        catch (Exception exception)
        {
            return new ProcessResult(-1, exception.Message);
        }
    }

    private static void AppendOutput(StringBuilder output, object outputLock, string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        lock (outputLock)
        {
            output.AppendLine(line);
        }
    }

    private static bool IsOperationRunning()
    {
        return currentOperation != null && !currentOperation.IsCompleted;
    }

    private static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private static string FindDockerExecutable()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string candidate = Path.Combine(programFiles, "Docker", "Docker", "resources", "bin", "docker.exe");
        return File.Exists(candidate) ? candidate : "docker.exe";
    }

    private static string FindDockerDesktopExecutable()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string candidate = Path.Combine(programFiles, "Docker", "Docker", "Docker Desktop.exe");
        return File.Exists(candidate) ? candidate : string.Empty;
    }

    private static bool TryCreateAsciiProjectPath(
        string projectRoot,
        out string composeProjectRoot,
        out string temporaryDrive,
        out string error)
    {
        composeProjectRoot = projectRoot;
        temporaryDrive = string.Empty;
        error = string.Empty;
        if (ContainsOnlyAscii(projectRoot))
        {
            return true;
        }

        string[] usedDrives = Directory.GetLogicalDrives();
        for (char driveLetter = 'Z'; driveLetter >= 'R'; driveLetter--)
        {
            string driveName = $"{driveLetter}:";
            string driveRoot = driveName + "\\";
            if (Array.Exists(usedDrives, drive => string.Equals(drive, driveRoot, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            ProcessResult mappingResult =
                RunProcess("subst.exe", $"{driveName} {QuoteArgument(projectRoot)}", 10000);
            if (mappingResult.ExitCode != 0)
            {
                continue;
            }

            composeProjectRoot = driveRoot;
            temporaryDrive = driveName;
            return true;
        }

        error = "无法为中文项目路径创建临时英文盘符，Docker Compose 无法安全构建镜像。";
        return false;
    }

    private static void RemoveTemporaryDriveMapping(string temporaryDrive)
    {
        if (!string.IsNullOrEmpty(temporaryDrive))
        {
            RunProcess("subst.exe", temporaryDrive + " /D", 10000);
        }
    }

    private static bool ContainsOnlyAscii(string value)
    {
        foreach (char character in value)
        {
            if (character > 127)
            {
                return false;
            }
        }

        return true;
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static string Tail(string value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maximumLength)
        {
            return value ?? string.Empty;
        }

        return value.Substring(value.Length - maximumLength, maximumLength);
    }

    private enum BackendOperation
    {
        Start,
        Stop
    }

    private readonly struct ProcessResult
    {
        public ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public int ExitCode { get; }
        public string Output { get; }
    }

    private readonly struct BackendOperationResult
    {
        private BackendOperationResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }
        public string Message { get; }

        public static BackendOperationResult Successful(string message)
        {
            return new BackendOperationResult(true, message);
        }

        public static BackendOperationResult Failure(string message)
        {
            return new BackendOperationResult(false, message);
        }
    }
}
