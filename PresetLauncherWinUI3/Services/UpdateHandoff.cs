using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PresetLauncher.Services;

internal static class UpdateHandoff
{
    internal static void CheckInstallation()
    {
        UpdateInstallation.RequireNonElevated();
        UpdateInstallation.Find(Environment.ProcessPath!, RuntimeInformation.ProcessArchitecture);
    }

    internal static async Task StartAsync(ReleaseUpdate update, string directory)
    {
        var json = Encoding.UTF8.GetBytes(update.SignedReleaseJson ?? throw new InvalidDataException("Missing signed release."));
        if (json.Length > UpdateService.MaxReleaseBytes)
            throw new InvalidDataException("Signed release exceeds the handoff limit.");

        var source = Path.Combine(AppContext.BaseDirectory, UpdateStaging.HelperName);
        var destination = Path.Combine(directory, UpdateStaging.HelperName);
        using var sourcePin = UpdateStaging.PinFile(source);
        byte[] helperDigest;
        using (var copy = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await sourcePin.CopyToAsync(copy);
            sourcePin.Position = 0;
            helperDigest = await SHA256.HashDataAsync(sourcePin);
        }
        using var destinationPin = UpdateStaging.PinFile(destination);
        var copiedDigest = await SHA256.HashDataAsync(destinationPin);
        if (!CryptographicOperations.FixedTimeEquals(helperDigest, copiedDigest))
            throw new InvalidDataException("The staged update helper changed.");

        var pipeId = Guid.NewGuid().ToString("N");
        using var pipe = new NamedPipeServerStream(UpdateStaging.PipePrefix + pipeId, PipeDirection.InOut,
            1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var start = new ProcessStartInfo(destination) { WorkingDirectory = Environment.SystemDirectory, UseShellExecute = false };
        start.ArgumentList.Add(pipeId);
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(App.Localization.Language);
        using var helper = Process.Start(start) ?? throw new InvalidOperationException("Update helper did not start.");
        var connecting = pipe.WaitForConnectionAsync(timeout.Token);
        var exited = helper.WaitForExitAsync(timeout.Token);
        if (await Task.WhenAny(connecting, exited) == exited)
            throw new IOException("Update helper exited before connecting.");
        await connecting;
        if (!UpdateStaging.IsClient(pipe, helper.Id))
            throw new IOException("Unexpected update helper connection.");

        var length = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, json.Length);
        await pipe.WriteAsync(length, timeout.Token);
        await pipe.WriteAsync(json, timeout.Token);
        var reply = new byte[1];
        await pipe.ReadExactlyAsync(reply, timeout.Token);
        if (reply[0] != UpdateStaging.Ready)
            throw new IOException("Update helper rejected the handoff.");
        await pipe.WriteAsync(new byte[] { UpdateStaging.Commit }, timeout.Token);
        timeout.Cancel();
    }
}
