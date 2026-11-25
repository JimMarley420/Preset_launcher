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

    public (int SuccessCount, int FailedCount) LaunchPresetMissing(Preset preset)
    {
        preset = PresetDraft.Copy(preset);
        var started = new List<string>();
        int failed = 0;
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

        var orderedExes = preset.Executables.OrderBy(item => item.Order);

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

                using var launched = Process.Start(startInfo);
                if (exe.LaunchCount < int.MaxValue) exe.LaunchCount++;
                exe.LastLaunched = DateTime.Now;
                started.Add(exe.Path);
            }
            catch
            {
                failed++;
            }
        }
        if (started.Count > 0)
            App.Data.RecordLaunch(preset, started, null, 0);
        return (started.Count, failed);
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
                    // A process can exit during enumeration.
                }
                finally { p.Dispose(); }
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Debug.WriteLine(ex.GetType().Name);
        }

        return processes;
    }

    public bool TryGetExecutablePath(int pid, out string path)
    {
        return TryGetProcessPath(pid, out path);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

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
                var size = sb.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
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
            using var proc = Process.GetProcessById(pid);
            if (!string.IsNullOrEmpty(proc.MainModule?.FileName))
            {
                path = proc.MainModule.FileName;
                return true;
            }
        }
        catch
        {
            // The process may have exited or may be inaccessible.
        }

        return false;
    }

    public event EventHandler<string>? StatusChanged;

    public async Task<ExecutionHistory> LaunchPresetAsync(Preset preset)
    {
        preset = PresetDraft.Copy(preset);
        var started = new List<string>();
        var history = new ExecutionHistory
        {
            PresetId = preset.Id,
            PresetName = preset.Name,
            ExecutedAt = DateTime.Now,
            ExecutablesCount = preset.Executables.Count
        };

        var stopwatch = Stopwatch.StartNew();
        var orderedExes = preset.Executables.OrderBy(item => item.Order);

        foreach (var exe in orderedExes)
        {
            StatusChanged?.Invoke(this, exe.Name);

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

                using var launched = await Task.Run(() => Process.Start(startInfo));
                
                detail.Success = true;
                history.SuccessCount++;
                
                if (exe.LaunchCount < int.MaxValue) exe.LaunchCount++;
                exe.LastLaunched = DateTime.Now;
                
                started.Add(exe.Path);
            }
            catch (Exception ex)
            {
                detail.Success = false;
                detail.ErrorMessage = ex.Message;
                history.FailedCount++;
            }

            history.Details.Add(detail);
        }

        history.Duration = stopwatch.Elapsed;
        
        if (preset.LaunchCount < int.MaxValue) preset.LaunchCount++;
        preset.LastLaunched = DateTime.Now;

        App.Data.RecordLaunch(preset, started, history, App.Settings.CurrentSettings.MaxHistoryEntries);

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
                                using var proc = Process.GetProcessById(pid);
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
}
