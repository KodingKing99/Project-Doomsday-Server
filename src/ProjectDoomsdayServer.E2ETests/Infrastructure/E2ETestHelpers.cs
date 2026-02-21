using System.Text;

namespace ProjectDoomsdayServer.E2ETests.Infrastructure;

public static class E2ETestHelpers
{
    public static string UniqueUserId() => $"e2e-user-{Guid.NewGuid():N}";

    public static byte[] Utf8Bytes(string text) => Encoding.UTF8.GetBytes(text);

    public static byte[] RandomBytes(int length)
    {
        var buf = new byte[length];
        Random.Shared.NextBytes(buf);
        return buf;
    }
}
