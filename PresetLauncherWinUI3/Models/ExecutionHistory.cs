using System;
using System.Collections.Generic;

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
