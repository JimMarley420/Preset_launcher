using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using PresetLauncher.Models;

namespace PresetLauncher.Services
{
    public class LauncherService
    {
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
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(process.MainModule?.FileName))
                        {
                            processes.Add(new ProcessInfo
                            {
                                Name = process.ProcessName,
                                Path = process.MainModule.FileName,
                                Id = process.Id
                            });
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return processes;
        }
    }

    public class ProcessInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Id { get; set; }
    }
}
