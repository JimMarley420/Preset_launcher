using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PresetLauncher.Models;

namespace PresetLauncher.Services;

public class LauncherService
{
    // Helpers for safe process path retrieval
    [Flags]
    private enum ProcessAccessFlags : uint
    {
        QueryLimitedInformation = 0x1000
    }

    public async Task LaunchPresetMissingAsync(Preset preset)
    {
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in GetRunningProcesses())
        {
            if (!string.IsNullOrEmpty(p.Path))
            {
                try
                {
                    // Normalize the full path for consistent comparison
                    var normalizedPath = System.IO.Path.GetFullPath(p.Path);
                    running.Add(normalizedPath);
                }
                catch
                {
                    // If normalization fails, add as-is
                    running.Add(p.Path);
                }
            }
            running.Add(System.IO.Path.GetFileName(p.Path));
        }

        var orderedExes = new List<ExecutableItem>(preset.Executables);
        orderedExes.Sort((a, b) => a.Order.CompareTo(b.Order));

        foreach (var exe in orderedExes)
        {
            if (string.IsNullOrWhiteSpace(exe.Path)) continue;
            
            var file = System.IO.Path.GetFileName(exe.Path);
            var exePath = exe.Path;
            
            // Try to normalize the executable path for comparison
            try
            {
                exePath = System.IO.Path.GetFullPath(exe.Path);
            }
            catch
            {
                // If normalization fails, use the original path
            }

            if (running.Contains(exePath) || running.Contains(file))
            {
                continue; // already running
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exe.Path,
                    Arguments = exe.Arguments,
                    UseShellExecute = true
                };

                if (exe.RunAsAdmin)
                    startInfo.Verb = "runas";

                Process.Start(startInfo);
                exe.LaunchCount++;
                exe.LastLaunched = DateTime.Now;
                App.Data.UpdateUsageStats(exe.Path);
            }
            catch
            {
                // ignore
            }
        }
    }

    public List<ProcessInfo> GetRunningProcessSummaries()
    {
        var processes = new List<ProcessInfo>();
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    processes.Add(new ProcessInfo
                    {
                        Name = p.ProcessName,
                        Path = string.Empty,
                        Id = p.Id
                    });
                }
                catch
                {
                    // ignore per-process failures
                }
            }
        }
        catch
        {
            // ignore overall failures
        }

        return processes;
    }

    public bool TryGetExecutablePath(int pid, out string path)
    {
        return TryGetProcessPath(pid, out path);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, out int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcesses([Out] uint[] lpidProcess, int cb, out int lpcbNeeded);

    private static bool TryGetProcessPath(int pid, out string path)
    {
        path = string.Empty;
        try
        {
            var hProcess = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, pid);
            if (hProcess == IntPtr.Zero) return false;
            try
            {
                var sb = new StringBuilder(1024);
                if (QueryFullProcessImageName(hProcess, 0, sb, out var size))
                {
                    path = sb.ToString();
                    return true;
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch
        {
        }

        // fallback: try Process.MainModule
        try
        {
            var proc = Process.GetProcessById(pid);
            if (!string.IsNullOrEmpty(proc.MainModule?.FileName))
            {
                path = proc.MainModule.FileName;
                return true;
            }
        }
        catch
        {
            // ignore — process may have elevated privileges or be inaccessible
        }

        return false;
    }

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<ExecutionHistory>? ExecutionCompleted;

    public async Task<ExecutionHistory> LaunchPresetAsync(Preset preset)
    {
        var history = new ExecutionHistory
        {
            PresetId = preset.Id,
            PresetName = preset.Name,
            ExecutedAt = DateTime.Now,
            ExecutablesCount = preset.Executables.Count
        };

        var startTime = DateTime.Now;
        var orderedExes = new List<ExecutableItem>(preset.Executables);
        orderedExes.Sort((a, b) => a.Order.CompareTo(b.Order));

        foreach (var exe in orderedExes)
        {
            StatusChanged?.Invoke(this, App.Localization.Get("execution.launching", exe.Name));

            var detail = new ExecutionDetail
            {
                ExecutableName = exe.Name,
                ExecutablePath = exe.Path,
                StartedAt = DateTime.Now
            };

            try
            {
                if (exe.DelaySeconds > 0)
                {
                    await Task.Delay(exe.DelaySeconds * 1000);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exe.Path,
                    Arguments = exe.Arguments,
                    UseShellExecute = true
                };

                if (exe.RunAsAdmin)
                {
                    startInfo.Verb = "runas";
                }

                Process.Start(startInfo);
                
                detail.Success = true;
                history.SuccessCount++;
                
                exe.LaunchCount++;
                exe.LastLaunched = DateTime.Now;
                
                App.Data.UpdateUsageStats(exe.Path);
            }
            catch (Exception ex)
            {
                detail.Success = false;
                detail.ErrorMessage = ex.Message;
                history.FailedCount++;
            }

            history.Details.Add(detail);
        }

        history.Duration = DateTime.Now - startTime;
        
        preset.LaunchCount++;
        preset.LastLaunched = DateTime.Now;

        App.Data.AddHistory(history);
        App.Data.Save();

        StatusChanged?.Invoke(this, history.FailedCount == 0 
            ? App.Localization.Get("execution.success") 
            : App.Localization.Get("execution.failed"));

        ExecutionCompleted?.Invoke(this, history);

        return history;
    }

    public List<ProcessInfo> GetRunningProcesses()
    {
        var processes = new List<ProcessInfo>();

        try
        {
            var pids = new uint[1024];
            if (!EnumProcesses(pids, pids.Length * sizeof(uint), out var bytes))
                return processes;

            var count = bytes / sizeof(uint);
            for (int i = 0; i < count; i++)
            {
                var pid = (int)pids[i];
                if (pid == 0) continue;

                try
                {
                    if (TryGetProcessPath(pid, out var path) && !string.IsNullOrEmpty(path))
                    {
                        string name = System.IO.Path.GetFileNameWithoutExtension(path);
                        processes.Add(new ProcessInfo
                        {
                            Name = name,
                            Path = path,
                            Id = pid
                        });
                    }
                }
                catch
                {
                    // ignore per-process failures
                }
            }
        }
        catch
        {
            // ignore overall failures
        }

        return processes;
    }

    public async Task<int> ClosePresetProcessesAsync(Preset preset)
    {
        var closedCount = 0;

        try
        {
            var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var exe in preset.Executables)
            {
                if (!string.IsNullOrWhiteSpace(exe.Path))
                {
                    try
                    {
                        var full = System.IO.Path.GetFullPath(exe.Path);
                        targetPaths.Add(full);
                    }
                    catch
                    {
                        targetPaths.Add(exe.Path);
                    }
                }
            }

            try
            {
                var pids = new uint[1024];
                if (!EnumProcesses(pids, pids.Length * sizeof(uint), out var bytes))
                    return 0;

                var count = bytes / sizeof(uint);
                for (int i = 0; i < count; i++)
                {
                    var pid = (int)pids[i];
                    if (pid == 0) continue;

                    try
                    {
                        if (!TryGetProcessPath(pid, out var mainPath) || string.IsNullOrEmpty(mainPath))
                            continue;

                        if (targetPaths.Contains(mainPath) || targetPaths.Contains(System.IO.Path.GetFileName(mainPath)))
                        {
                            try
                            {
                                var proc = Process.GetProcessById(pid);
                                if (proc.CloseMainWindow())
                                {
                                    await Task.Delay(500);
                                    if (!proc.HasExited)
                                    {
                                        proc.Kill();
                                    }
                                }
                                else
                                {
                                    proc.Kill();
                                }

                                closedCount++;
                            }
                            catch
                            {
                                // ignore individual process failures
                            }
                        }
                    }
                    catch
                    {
                        // ignore per-pid failures
                    }
                }
            }
            catch
            {
                // ignore overall failures
            }
        }
        catch
        {
            // ignore overall failures
        }

        return closedCount;
    }
}

public class ProcessInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Id { get; set; }

    public override string ToString()
    {
        try
        {
            var file = string.IsNullOrEmpty(Path) ? "" : System.IO.Path.GetFileName(Path);
            if (!string.IsNullOrEmpty(file))
            {
                return $"{Name} — {file} ({Id})";
            }
            return $"{Name} ({Id})";
        }
        catch
        {
            return Name;
        }
    }
}

