using System;
using System.Collections.Generic;
using System.ComponentModel;
using PresetLauncher.Services;

namespace PresetLauncher.Models
{
    public class ExecutionHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PresetId { get; set; } = string.Empty;
        public string PresetName { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; }
        public int ExecutablesCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public TimeSpan? Duration { get; set; }
        public List<ExecutionDetail> Details { get; set; } = new();
        
        [Newtonsoft.Json.JsonIgnore]
        public string SuccessText => LocalizationService.Instance.Get(
            FailedCount > 0 ? "history.failure" : "history.success", SuccessCount, ExecutablesCount, FailedCount);
    }

    public class ExecutionDetail
    {
        public string ExecutableName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? Success { get; set; }
        public string? ErrorMessage { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? NativeErrorCode { get; set; }
        public DateTime StartedAt { get; set; }

        internal void RecordFailure(Exception exception)
        {
            Success = false;
            ErrorMessage = exception.Message;
            NativeErrorCode = (exception as Win32Exception)?.NativeErrorCode;
        }

        internal bool? RecordedResult => Success ??
            (NativeErrorCode.HasValue || !string.IsNullOrWhiteSpace(ErrorMessage) ? false : (bool?)null);

        internal LaunchFailure Failure => NativeErrorCode switch
        {
            // Windows SDK WinError.h. ERROR_CANCELLED does not identify which prompt was cancelled.
            2 => LaunchFailure.FileNotFound,       // ERROR_FILE_NOT_FOUND
            3 => LaunchFailure.PathNotFound,       // ERROR_PATH_NOT_FOUND
            5 => LaunchFailure.AccessDenied,       // ERROR_ACCESS_DENIED
            193 => LaunchFailure.InvalidFormat,    // ERROR_BAD_EXE_FORMAT
            216 => LaunchFailure.Incompatible,     // ERROR_EXE_MACHINE_TYPE_MISMATCH
            1155 => LaunchFailure.NoAssociation,   // ERROR_NO_ASSOCIATION
            1223 => LaunchFailure.Cancelled,       // ERROR_CANCELLED
            _ => LaunchFailure.Unknown
        };
    }

    internal enum LaunchFailure
    {
        FileNotFound, PathNotFound, AccessDenied, InvalidFormat, Incompatible, NoAssociation, Cancelled, Unknown
    }
}
