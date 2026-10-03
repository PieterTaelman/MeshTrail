using System.Globalization;

namespace Meshtrail.Mesh;

/// <summary>Node numbers are uint32; people see them as "!" + 8 hex digits, e.g. 4044729068 = "!f115aaec".</summary>
public static class NodeIds
{
    /// <summary>"To" address meaning "everyone on the channel".</summary>
    public const uint Broadcast = uint.MaxValue;

    public static string Format(uint nodeNum) => $"!{nodeNum:x8}";

    public static bool TryParse(string? nodeId, out uint nodeNum)
    {
        nodeNum = 0;
        return nodeId is { Length: 9 } && nodeId[0] == '!'
            && uint.TryParse(nodeId.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out nodeNum);
    }
}
