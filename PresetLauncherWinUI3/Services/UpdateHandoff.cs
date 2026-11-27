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
        using var timeout = new CancellationTokenSource(UpdateHandoffProtocol.Timeout);
        var start = new ProcessStartInfo(destination) { WorkingDirectory = Environment.SystemDirectory, UseShellExecute = false };
        start.ArgumentList.Add(pipeId);
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(App.Localization.Language);
        using var helper = Process.Start(start) ?? throw new InvalidOperationException("Update helper did not start.");
        try
        {
            var connecting = pipe.WaitForConnectionAsync(timeout.Token);
            var exited = helper.WaitForExitAsync(timeout.Token);
            if (await Task.WhenAny(connecting, exited) == exited && !connecting.IsCompleted)
            {
                timeout.Cancel();
                try { await connecting; }
                catch (OperationCanceledException ex) { throw new IOException("Update helper exited before connecting.", ex); }
                throw new IOException("Update helper exited before connecting.");
            }
            await connecting;
            if (!UpdateStaging.IsClient(pipe, helper.Id))
                throw new IOException("Unexpected update helper connection.");

            await UpdateHandoffProtocol.WriteReleaseAsync(pipe, json, timeout.Token);
            await UpdateHandoffProtocol.ExpectAsync(pipe, UpdateHandoffProtocol.Ready, timeout.Token);
            await UpdateHandoffProtocol.SendAsync(pipe, UpdateHandoffProtocol.Commit, timeout.Token);
            await UpdateHandoffProtocol.ExpectAsync(pipe, UpdateHandoffProtocol.Accepted, timeout.Token);
        }
        catch
        {
            // Disconnect first so a pre-commit helper can release the staging lease.
            pipe.Dispose();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await helper.WaitForExitAsync(cleanup.Token); }
            catch (OperationCanceledException ex) { Debug.WriteLine(ex); }
            throw;
        }
        finally { timeout.Cancel(); }
    }
}
