namespace Lua2CS.Tests;

public sealed class ChatMessageFormatterTests
{
    public static TheoryData<string> SupportedColorMessages => new()
    {
        "\u0001default",
        "\u0002dark red",
        "\u0003light purple",
        "\u0004green",
        "\u0005olive",
        "\u0006lime",
        "\u0007red",
        "\u0008grey",
        "\u0009yellow",
        "\u000Asilver",
        "\u000Bblue",
        "\u000Cdark blue",
        "\u000Epurple",
        "\u000Flight red",
        "\u0010gold"
    };

    [Theory]
    [MemberData(nameof(SupportedColorMessages))]
    public void NormalizeAddsLeadingSpaceBeforeSupportedColorCode(string message) =>
        Assert.Equal($" {message}", ChatMessageFormatter.Normalize(message));

    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("plain\u0004green")]
    [InlineData("\u000Dunsupported")]
    public void NormalizeLeavesOtherMessagesUnchanged(string message) =>
        Assert.Equal(message, ChatMessageFormatter.Normalize(message));
}
