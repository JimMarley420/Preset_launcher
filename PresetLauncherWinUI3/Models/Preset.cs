using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PresetLauncher.Services;

namespace PresetLauncher.Models
{
    public partial class Preset : ObservableObject
    {
        [Newtonsoft.Json.JsonIgnore]
        public string ProgramsText => LocalizationService.Instance.Get(
            Executables.Count == 1 ? "exe.programOne" : "exe.programMany", Executables.Count);
        [ObservableProperty]
        private string id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string iconPath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ExecutableItem> executables = new();

        [ObservableProperty]
        private bool isLocked = false;

        [ObservableProperty]
        private string passwordHash = string.Empty;

        [ObservableProperty]
        private DateTime createdAt = DateTime.Now;

        [ObservableProperty]
        private DateTime? lastModified;

        [ObservableProperty]
        private int launchCount = 0;

        [ObservableProperty]
        private DateTime? lastLaunched;

        public Preset()
        {
        }

        public Preset(string name)
        {
            Name = name;
        }

        public void ValidateAllPaths()
        {
            foreach (var exe in Executables)
            {
                exe.ValidatePath();
            }
        }

        public Preset Clone()
        {
            var clone = new Preset
            {
                Name = $"{Name} (copie)",
                Description = Description,
                IconPath = IconPath,
                IsLocked = false,
                PasswordHash = string.Empty
            };

            foreach (var exe in Executables)
            {
                clone.Executables.Add(new ExecutableItem
                {
                    Name = exe.Name,
                    Path = exe.Path,
                    Arguments = exe.Arguments,
                    RunAsAdmin = exe.RunAsAdmin,
                    DelaySeconds = exe.DelaySeconds,
                    Order = exe.Order
                });
            }

            return clone;
        }
    }
}
