namespace Edgeless.Core.Config;

/// <summary>Converts between <see cref="HotkeyConfig"/> and RegisterHotKey modifiers / virtual-key codes.</summary>
public static class HotkeyBinding
{
    public const uint MOD_ALT = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;
    public const uint MOD_WIN = 0x8;
    public const uint MOD_NOREPEAT = 0x4000;

    private const uint VK_F1 = 0x70;
    private const int MaxFunctionKey = 24;

    private static readonly (string Name, uint Flag)[] ModifierNames =
        [("Ctrl", MOD_CONTROL), ("Alt", MOD_ALT), ("Shift", MOD_SHIFT), ("Win", MOD_WIN)];

    /// <summary>Requires at least one modifier and a key in A-Z, 0-9 or F1-F24. Result includes MOD_NOREPEAT.</summary>
    public static bool TryParse(HotkeyConfig config, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        foreach (var raw in config.Modifiers)
        {
            var flag = ModifierNames.FirstOrDefault(m => m.Name.Equals(raw.Trim(), StringComparison.OrdinalIgnoreCase)).Flag;
            if (flag == 0)
            {
                return false;
            }

            modifiers |= flag;
        }

        if (modifiers == 0 || !TryParseKey(config.Key.Trim(), out virtualKey))
        {
            modifiers = 0;
            return false;
        }

        modifiers |= MOD_NOREPEAT;
        return true;
    }

    public static HotkeyConfig FromNative(uint modifiers, uint virtualKey)
        => new()
        {
            Modifiers = [.. ModifierNames.Where(m => (modifiers & m.Flag) != 0).Select(m => m.Name)],
            Key = KeyName(virtualKey),
        };

    private static bool TryParseKey(string key, out uint virtualKey)
    {
        virtualKey = 0;
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
        {
            virtualKey = char.ToUpperInvariant(key[0]);
            return true;
        }

        if (key.Length >= 2 && (key[0] == 'F' || key[0] == 'f')
            && int.TryParse(key.AsSpan(1), out var number) && number is >= 1 and <= MaxFunctionKey)
        {
            virtualKey = VK_F1 + (uint)(number - 1);
            return true;
        }

        return false;
    }

    private static string KeyName(uint virtualKey)
        => virtualKey >= VK_F1 && virtualKey < VK_F1 + MaxFunctionKey
            ? "F" + (virtualKey - VK_F1 + 1)
            : ((char)virtualKey).ToString();
}
