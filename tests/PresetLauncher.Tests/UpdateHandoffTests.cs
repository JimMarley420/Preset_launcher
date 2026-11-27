using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresetLauncher.Services;

namespace PresetLauncher.Tests;

[TestClass]
public sealed class UpdateHandoffTests
{
    [TestMethod]
    public void ApplicationLeasesShareAccessAndExcludeTheInstallerInBothDirections()
    {
        var workspace = UpdateStaging.Create();
        var path = Path.Combine(workspace.FullName, UpdateStartup.LockFileName);
        try
        {
            using (UpdateStartup.OpenApplicationLease(workspace.FullName))
            using (UpdateStartup.OpenApplicationLease(workspace.FullName))
                Assert.ThrowsExactly<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None));

            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var sharing = Assert.ThrowsExactly<Win32Exception>(() => UpdateStartup.OpenApplicationLease(workspace.FullName));
                Assert.AreEqual(32, sharing.NativeErrorCode);
            }

            using var afterSetup = UpdateStartup.OpenApplicationLease(workspace.FullName);
            Assert.IsTrue(afterSetup.CanRead);
        }
        finally
        {
            File.Delete(path);
            workspace.Delete();
        }
    }

    [TestMethod]
    public async Task CompleteExchangeRequiresTheHelperToAcknowledgeCommit()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = CreateServer(out var name);
        using var client = await ConnectAsync(server, name, timeout.Token);
        byte[] release = [1, 2, 3];
        var received = UpdateHandoffProtocol.ReadReleaseAsync(client, timeout.Token);
        await UpdateHandoffProtocol.WriteReleaseAsync(server, release, timeout.Token);
        CollectionAssert.AreEqual(release, await received);
        var ready = UpdateHandoffProtocol.ExpectAsync(server, UpdateHandoffProtocol.Ready, timeout.Token);
        await UpdateHandoffProtocol.SendAsync(client, UpdateHandoffProtocol.Ready, timeout.Token);
        await ready;
        var commit = UpdateHandoffProtocol.ExpectAsync(client, UpdateHandoffProtocol.Commit, timeout.Token);
        await UpdateHandoffProtocol.SendAsync(server, UpdateHandoffProtocol.Commit, timeout.Token);
        await commit;

        var acknowledged = UpdateHandoffProtocol.ExpectAsync(server, UpdateHandoffProtocol.Accepted, timeout.Token);
        Assert.IsFalse(acknowledged.IsCompleted);
        await UpdateHandoffProtocol.SendAsync(client, UpdateHandoffProtocol.Accepted, timeout.Token);
        await acknowledged;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellingDeadlineReleasesPartialHeaderOrPayload(bool hasFullHeader)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = CreateServer(out var name);
        using var client = await ConnectAsync(server, name, timeout.Token);
        byte[] incomplete = hasFullHeader ? [10, 0, 0, 0, 1, 2] : [10, 0];
        var pending = UpdateHandoffProtocol.ReadReleaseAsync(client, timeout.Token);
        await server.WriteAsync(incomplete, timeout.Token);
        Assert.IsFalse(pending.IsCompleted);
        timeout.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
    }

    [TestMethod]
    [DataRow(UpdateHandoffProtocol.Ready)]
    [DataRow(UpdateHandoffProtocol.Commit)]
    [DataRow(UpdateHandoffProtocol.Accepted)]
    public async Task MissingControlByteCancelsWithoutWaitingForPeerExit(byte control)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = CreateServer(out var name);
        using var client = await ConnectAsync(server, name, timeout.Token);
        var pending = UpdateHandoffProtocol.ExpectAsync(client, control, timeout.Token);
        Assert.IsFalse(pending.IsCompleted);
        timeout.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
    }

    [TestMethod]
    public async Task DisconnectAndOversizedReleaseFailBeforeCommit()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = CreateServer(out var name);
        using var client = await ConnectAsync(server, name, timeout.Token);
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, UpdateService.MaxReleaseBytes + 1);
        var oversized = UpdateHandoffProtocol.ReadReleaseAsync(client, timeout.Token);
        await server.WriteAsync(header, timeout.Token);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => oversized);
        var pending = UpdateHandoffProtocol.ExpectAsync(client, UpdateHandoffProtocol.Commit, timeout.Token);
        server.Dispose();
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => pending);
    }

    [TestMethod]
    public void StartupNeedsReadinessAndRecognizesAnExistingReadyInstanceAfterSuccessfulRedirect()
    {
        using var current = Process.GetCurrentProcess();
        var path = current.MainModule!.FileName;
        using var ready = UpdateStartup.OpenReadyEvent(current);
        Assert.AreEqual(UpdateLaunchResult.Unconfirmed, UpdateStartup.WaitForLaunch(current, path, TimeSpan.Zero));
        ready.Set();
        Assert.AreEqual(UpdateLaunchResult.Started, UpdateStartup.WaitForLaunch(current, path, TimeSpan.Zero));

        using var redirected = ExitProcess(0);
        Assert.AreEqual(UpdateLaunchResult.AlreadyRunning, UpdateStartup.WaitForLaunch(redirected, path, TimeSpan.FromSeconds(5)));
        using var failed = ExitProcess(1);
        Assert.AreEqual(UpdateLaunchResult.Unconfirmed, UpdateStartup.WaitForLaunch(failed, path, TimeSpan.FromSeconds(5)));
        ready.Reset();
        Assert.AreEqual(UpdateLaunchResult.Unconfirmed, UpdateStartup.WaitForLaunch(redirected, path, TimeSpan.Zero));
    }

    private static Process ExitProcess(int exitCode)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("/d");
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add($"exit {exitCode}");
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start test process.");
    }

    private static NamedPipeServerStream CreateServer(out string name)
    {
        name = Guid.NewGuid().ToString("N");
        return new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    private static async Task<NamedPipeClientStream> ConnectAsync(NamedPipeServerStream server, string name, CancellationToken cancellation)
    {
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await Task.WhenAll(server.WaitForConnectionAsync(cancellation), client.ConnectAsync(cancellation));
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
