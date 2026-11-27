using System.Buffers.Binary;

namespace PresetLauncher.Services;

internal static class UpdateHandoffProtocol
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
    internal const byte Ready = 1;
    internal const byte Commit = 2;
    internal const byte Accepted = 3;

    // The helper cannot install until the parent has actually received Accepted.
    // Unlike a final pipe write, setting this local event has no acknowledgement gap.
    internal static string AuthorizationName(string pipeId) => @"Local\PresetLauncher.Update.Authorization." + pipeId;

    internal static EventWaitHandle CreateAuthorization(string pipeId)
    {
        var signal = new EventWaitHandle(false, EventResetMode.ManualReset, AuthorizationName(pipeId), out var created);
        if (created) return signal;
        signal.Dispose();
        throw new IOException("Update authorization already exists.");
    }

    internal static void WaitForAuthorization(EventWaitHandle signal, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (WaitHandle.WaitAny([signal, cancellation.WaitHandle]) != 0)
            cancellation.ThrowIfCancellationRequested();
    }

    internal static async Task<byte[]> ReadReleaseAsync(Stream pipe, CancellationToken cancellation)
    {
        var header = new byte[sizeof(int)];
        await pipe.ReadExactlyAsync(header, cancellation).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > UpdateService.MaxReleaseBytes)
            throw new InvalidDataException("Invalid signed release length.");
        var release = new byte[length];
        await pipe.ReadExactlyAsync(release, cancellation).ConfigureAwait(false);
        return release;
    }

    internal static async Task WriteReleaseAsync(Stream pipe, byte[] release, CancellationToken cancellation)
    {
        if (release.Length is <= 0 or > UpdateService.MaxReleaseBytes)
            throw new InvalidDataException("Invalid signed release length.");
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, release.Length);
        await pipe.WriteAsync(header, cancellation).ConfigureAwait(false);
        await pipe.WriteAsync(release, cancellation).ConfigureAwait(false);
    }

    internal static ValueTask SendAsync(Stream pipe, byte value, CancellationToken cancellation) =>
        pipe.WriteAsync(new byte[] { value }, cancellation);

    internal static async Task ExpectAsync(Stream pipe, byte expected, CancellationToken cancellation)
    {
        var reply = new byte[1];
        await pipe.ReadExactlyAsync(reply, cancellation).ConfigureAwait(false);
        if (reply[0] != expected)
            throw new InvalidDataException("Unexpected update handoff response.");
    }
}
