using System;
using System.Collections.Generic;
using PresetLauncher.Services;

namespace PresetLauncher.Models
{
    public class ExecutionHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PresetId { get; set; } = string.Empty;
        public string PresetName { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; } = DateTime.Now;
        public int ExecutablesCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ExecutionDetail> Details { get; set; } = new();
        
        [Newtonsoft.Json.JsonIgnore]
        public string SuccessText => LocalizationService.Instance.Get(
            FailedCount > 0 ? "history.failure" : "history.success", SuccessCount, ExecutablesCount, FailedCount);
    }

    public class ExecutionDetail
    {
        public string ExecutableName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; }
    }
}
