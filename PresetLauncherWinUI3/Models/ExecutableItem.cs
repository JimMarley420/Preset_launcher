using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresetLauncher.Models
{
    public partial class ExecutableItem : ObservableObject
    {
        [ObservableProperty]
        private string id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string path = string.Empty;

        [ObservableProperty]
        private string arguments = string.Empty;

        [ObservableProperty]
        private bool runAsAdmin = false;

        [ObservableProperty]
        private int delaySeconds = 0;

        [ObservableProperty]
        private int order = 0;

        [ObservableProperty]
        private bool isValid = true;

        [ObservableProperty]
        private int launchCount = 0;

        [ObservableProperty]
        private DateTime? lastLaunched;

        public ExecutableItem()
        {
        }

        public ExecutableItem(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public void ValidatePath()
        {
            IsValid = System.IO.File.Exists(Path);
        }
    }
}
