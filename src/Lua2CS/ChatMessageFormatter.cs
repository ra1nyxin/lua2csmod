namespace Lua2CS;

internal static class ChatMessageFormatter
{
    private const string SupportedColorCodes = "\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0A\x0B\x0C\x0E\x0F\x10";

    internal static string Normalize(string message) =>
        message.Length > 0 && SupportedColorCodes.IndexOf(message[0]) >= 0
            ? $" {message}"
            : message;
}
