param([string]$Directory, [int]$Machine)
$ErrorActionPreference = 'Stop'
foreach ($name in 'PresetLauncherWinUI3.exe', 'PresetLauncher.Update.exe', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'Microsoft.UI.Xaml.dll') {
    $path = Join-Path $Directory $name
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($path))
    try {
        if ($reader.ReadUInt16() -ne 0x5a4d) { throw "Missing DOS header: $path" }
        $reader.BaseStream.Position = 0x3c
        $reader.BaseStream.Position = $reader.ReadUInt32()
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne $Machine) {
            throw "Unexpected PE machine in $path (expected $Machine)"
        }
    }
    finally { $reader.Dispose() }
}
