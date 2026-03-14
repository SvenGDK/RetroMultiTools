namespace RetroMultiTools.Utilities;

/// <summary>
/// Encodes and decodes Game Genie, Pro Action Replay, and GameShark cheat codes
/// for NES, SNES, Game Boy, Sega Genesis/Mega Drive, Game Gear, Master System,
/// Sega CD, PC Engine, and Neo Geo Pocket systems.
/// </summary>
public static class CheatCodeConverter
{
    public enum CheatSystem
    {
        NesGameGenie,
        SnesGameGenie,
        GameBoyGameGenie,
        GenesisGameGenie,
        SnesProActionReplay,
        GenesisProActionReplay,
        GameBoyProActionReplay,
        N64GameShark,
        GameGearGameGenie,
        MasterSystemProActionReplay,
        GbaGameShark,
        GameBoyColorGameGenie,
        GameBoyColorGameShark,
        Sega32XProActionReplay,
        SegaCDProActionReplay,
        PCEngineRaw,
        NeoGeoPocketGameShark
    }

    public static string GetSystemName(CheatSystem system) => system switch
    {
        CheatSystem.NesGameGenie => "NES Game Genie",
        CheatSystem.SnesGameGenie => "SNES Game Genie",
        CheatSystem.GameBoyGameGenie => "Game Boy Game Genie",
        CheatSystem.GenesisGameGenie => "Genesis Game Genie",
        CheatSystem.SnesProActionReplay => "SNES Pro Action Replay",
        CheatSystem.GenesisProActionReplay => "Genesis Pro Action Replay",
        CheatSystem.GameBoyProActionReplay => "Game Boy Pro Action Replay",
        CheatSystem.N64GameShark => "N64 GameShark",
        CheatSystem.GameGearGameGenie => "Game Gear Game Genie",
        CheatSystem.MasterSystemProActionReplay => "Master System Pro Action Replay",
        CheatSystem.GbaGameShark => "GBA GameShark/Action Replay",
        CheatSystem.GameBoyColorGameGenie => "Game Boy Color Game Genie",
        CheatSystem.GameBoyColorGameShark => "Game Boy Color GameShark",
        CheatSystem.Sega32XProActionReplay => "Sega 32X Pro Action Replay",
        CheatSystem.SegaCDProActionReplay => "Sega CD Pro Action Replay",
        CheatSystem.PCEngineRaw => "PC Engine Raw",
        CheatSystem.NeoGeoPocketGameShark => "Neo Geo Pocket GameShark",
        _ => system.ToString()
    };

    // NES Game Genie alphabet
    private const string NesGgAlphabet = "APZLGITYEOXUKSVN";

    // Genesis Game Genie alphabet
    private const string GenesisGgAlphabet = "ABCDEFGHJKLMNPRSTVWXYZ0123456789";

    // SNES Game Genie alphabet
    private const string SnesGgAlphabet = "DF2367BCAY01JKLMNPQRSTUVWXZ5EGHIR4";

    /// <summary>
    /// Decodes a cheat code into its raw address and value components.
    /// </summary>
    public static CheatCodeResult Decode(string code, CheatSystem system)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be empty.", nameof(code));

        code = code.Trim().ToUpperInvariant().Replace("-", "");

        return system switch
        {
            CheatSystem.NesGameGenie => DecodeNesGameGenie(code),
            CheatSystem.SnesGameGenie => DecodeSnesGameGenie(code),
            CheatSystem.GameBoyGameGenie => DecodeGameBoyGameGenie(code),
            CheatSystem.GenesisGameGenie => DecodeGenesisGameGenie(code),
            CheatSystem.SnesProActionReplay => DecodeSnesProActionReplay(code),
            CheatSystem.GenesisProActionReplay => DecodeGenesisProActionReplay(code),
            CheatSystem.GameBoyProActionReplay => DecodeGameBoyProActionReplay(code),
            CheatSystem.N64GameShark => DecodeN64GameShark(code),
            CheatSystem.GameGearGameGenie => DecodeGenesisGameGenie(code),
            CheatSystem.MasterSystemProActionReplay => DecodeMasterSystemProActionReplay(code),
            CheatSystem.GbaGameShark => DecodeGbaGameShark(code),
            CheatSystem.GameBoyColorGameGenie => DecodeGameBoyGameGenie(code),
            CheatSystem.GameBoyColorGameShark => DecodeGameBoyProActionReplay(code),
            CheatSystem.Sega32XProActionReplay => DecodeGenesisProActionReplay(code),
            CheatSystem.SegaCDProActionReplay => DecodeGenesisProActionReplay(code),
            CheatSystem.PCEngineRaw => DecodeRawAddressValue(code, "PC Engine raw cheat"),
            CheatSystem.NeoGeoPocketGameShark => DecodeRawAddressValue(code, "Neo Geo Pocket GameShark"),
            _ => throw new InvalidOperationException($"Unsupported cheat system: {system}")
        };
    }

    /// <summary>
    /// Encodes raw address/value into a cheat code.
    /// </summary>
    public static string Encode(uint address, ushort value, CheatSystem system, byte? compareValue = null)
    {
        return system switch
        {
            CheatSystem.NesGameGenie => EncodeNesGameGenie(address, (byte)value, compareValue),
            CheatSystem.SnesGameGenie => EncodeSnesGameGenie(address, (byte)value),
            CheatSystem.GameBoyGameGenie => EncodeGameBoyGameGenie(address, (byte)value, compareValue),
            CheatSystem.GenesisGameGenie => EncodeGenesisGameGenie(address, value),
            CheatSystem.SnesProActionReplay => EncodeSnesProActionReplay(address, (byte)value),
            CheatSystem.GenesisProActionReplay => EncodeGenesisProActionReplay(address, value),
            CheatSystem.GameBoyProActionReplay => EncodeGameBoyProActionReplay(address, (byte)value),
            CheatSystem.N64GameShark => EncodeN64GameShark(address, value),
            CheatSystem.GameGearGameGenie => EncodeGenesisGameGenie(address, value),
            CheatSystem.MasterSystemProActionReplay => EncodeMasterSystemProActionReplay(address, (byte)value),
            CheatSystem.GbaGameShark => EncodeGbaGameShark(address, value),
            CheatSystem.GameBoyColorGameGenie => EncodeGameBoyGameGenie(address, (byte)value, compareValue),
            CheatSystem.GameBoyColorGameShark => EncodeGameBoyProActionReplay(address, (byte)value),
            CheatSystem.Sega32XProActionReplay => EncodeGenesisProActionReplay(address, value),
            CheatSystem.SegaCDProActionReplay => EncodeGenesisProActionReplay(address, value),
            CheatSystem.PCEngineRaw => EncodeRawAddressValue(address, (byte)value),
            CheatSystem.NeoGeoPocketGameShark => EncodeRawAddressValue(address, (byte)value),
            _ => throw new InvalidOperationException($"Unsupported cheat system: {system}")
        };
    }

    #region NES Game Genie

    private static CheatCodeResult DecodeNesGameGenie(string code)
    {
        if (code.Length != 6 && code.Length != 8)
            throw new ArgumentException("NES Game Genie code must be 6 or 8 characters.");

        int[] values = new int[code.Length];
        for (int i = 0; i < code.Length; i++)
        {
            int idx = NesGgAlphabet.IndexOf(code[i]);
            if (idx < 0)
                throw new ArgumentException($"Invalid NES Game Genie character: {code[i]}");
            values[i] = idx;
        }

        // Decode address (15 bits) and value (8 bits)
        uint address = (uint)(
            ((values[3] & 7) << 12) |
            ((values[5] & 7) << 8) |
            ((values[4] & 8) << 8) |
            ((values[2] & 7) << 4) |
            ((values[1] & 8) << 4) |
            (values[4] & 7) |
            (values[3] & 8));

        address = (address & 0x7FFF) | 0x8000;

        byte value = (byte)(
            ((values[1] & 7) << 4) |
            ((values[0] & 8) << 4) |
            (values[0] & 7) |
            (values[5] & 8));

        byte? compareValue = null;
        if (code.Length == 8)
        {
            compareValue = (byte)(
                ((values[7] & 7) << 4) |
                ((values[6] & 8) << 4) |
                (values[6] & 7) |
                (values[2] & 8));
        }

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            CompareValue = compareValue,
            Description = compareValue.HasValue
                ? $"Address: ${address:X4}, Value: ${value:X2}, Compare: ${compareValue:X2}"
                : $"Address: ${address:X4}, Value: ${value:X2}"
        };
    }

    private static string EncodeNesGameGenie(uint address, byte value, byte? compareValue)
    {
        address = (address & 0x7FFF) | 0x8000;

        int[] values = new int[compareValue.HasValue ? 8 : 6];

        values[0] = ((value >> 4) & 8) | (value & 7);
        values[1] = (int)((address >> 4) & 8) | ((value >> 4) & 7);
        values[2] = ((compareValue.HasValue ? (compareValue.Value & 8) : 0)) | (int)((address >> 4) & 7);
        values[3] = (int)((address & 8) | ((address >> 12) & 7));
        values[4] = (int)(((address >> 8) & 8) | (address & 7));
        values[5] = (value & 8) | (int)((address >> 8) & 7);

        if (compareValue.HasValue)
        {
            byte cv = compareValue.Value;
            values[6] = ((cv >> 4) & 8) | (cv & 7);
            values[7] = (int)((address >> 4) & 8) | ((cv >> 4) & 7);
            // Fix: bit 3 of values[2] should come from compareValue
            values[2] = (cv & 8) | (int)((address >> 4) & 7);
        }

        char[] result = new char[values.Length];
        for (int i = 0; i < values.Length; i++)
            result[i] = NesGgAlphabet[values[i] & 0xF];

        return new string(result);
    }

    #endregion

    #region SNES Game Genie

    private static CheatCodeResult DecodeSnesGameGenie(string code)
    {
        if (code.Length != 8)
            throw new ArgumentException("SNES Game Genie code must be 8 data characters (e.g., XXXX-XXXX).");

        int[] values = new int[8];
        for (int i = 0; i < 8; i++)
        {
            int idx = SnesGgAlphabet.IndexOf(code[i]);
            if (idx < 0)
                throw new ArgumentException($"Invalid SNES Game Genie character: {code[i]}");
            values[i] = idx;
        }

        // Decode the 24-bit address and 8-bit value
        // Mask each value to 4 bits since the 34-char alphabet has indices 0-33
        // but the encoding uses only the low nibble of each position
        uint rawAddress = (uint)(
            ((values[0] & 0xF) << 20) |
            ((values[1] & 0xF) << 16) |
            ((values[2] & 0xF) << 12) |
            ((values[3] & 0xF) << 8) |
            ((values[4] & 0xF) << 4) |
            (values[5] & 0xF));

        byte value = (byte)(((values[6] & 0xF) << 4) | (values[7] & 0xF));

        // SNES GG uses bit rotation on address
        uint address = ((rawAddress >> 2) & 0x3FFFFF) | ((rawAddress & 0x3) << 22);

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X6}, Value: ${value:X2}"
        };
    }

    private static string EncodeSnesGameGenie(uint address, byte value)
    {
        // Reverse the bit rotation
        uint rawAddress = ((address << 2) & 0xFFFFFF) | ((address >> 22) & 0x3);

        int[] values =
        [
            (int)((rawAddress >> 20) & 0xF),
            (int)((rawAddress >> 16) & 0xF),
            (int)((rawAddress >> 12) & 0xF),
            (int)((rawAddress >> 8) & 0xF),
            (int)((rawAddress >> 4) & 0xF),
            (int)(rawAddress & 0xF),
            (value >> 4) & 0xF,
            value & 0xF,
        ];
        char[] result = new char[9];
        for (int i = 0; i < 4; i++)
            result[i] = SnesGgAlphabet[values[i]];
        result[4] = '-';
        for (int i = 4; i < 8; i++)
            result[i + 1] = SnesGgAlphabet[values[i]];

        return new string(result);
    }

    #endregion

    #region Game Boy Game Genie

    private static CheatCodeResult DecodeGameBoyGameGenie(string code)
    {
        if (code.Length != 6 && code.Length != 8)
            throw new ArgumentException("Game Boy Game Genie code must be 6 or 8 hex characters (XXX-XXX or XXX-XXX-XX).");

        if (!IsHexString(code))
            throw new ArgumentException("Game Boy Game Genie code must contain only hex characters.");

        byte value = Convert.ToByte(code[..2], 16);
        uint address = Convert.ToUInt16(code.Substring(2, 4), 16);

        // Unscramble address: rotate nibbles ABCD -> DABC
        address = ((address & 0x000F) << 12) | ((address & 0xF000) >> 4) |
                  ((address & 0x0F00) >> 4) | ((address & 0x00F0) >> 4);

        // XOR with 0xF000
        address ^= 0xF000;

        byte? compareValue = null;
        if (code.Length == 8)
        {
            byte raw = Convert.ToByte(code.Substring(6, 2), 16);
            // Unscramble compare value: rotate and XOR
            compareValue = (byte)(((raw >> 2) | (raw << 6)) ^ 0xBA);
        }

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            CompareValue = compareValue,
            Description = compareValue.HasValue
                ? $"Address: ${address:X4}, Value: ${value:X2}, Compare: ${compareValue:X2}"
                : $"Address: ${address:X4}, Value: ${value:X2}"
        };
    }

    private static string EncodeGameBoyGameGenie(uint address, byte value, byte? compareValue)
    {
        // XOR address with 0xF000
        address ^= 0xF000;

        // Scramble address: reverse of ABCD -> DABC, so DABC -> ABCD
        uint scrambled = ((address & 0xF000) >> 12) | ((address & 0x0F00) << 4) |
                         ((address & 0x00F0) << 4) | ((address & 0x000F) << 4);

        string result = $"{value:X2}{scrambled:X4}";

        if (compareValue.HasValue)
        {
            byte cv = (byte)((compareValue.Value ^ 0xBA));
            cv = (byte)((cv << 2) | (cv >> 6));
            result += $"{cv:X2}";
            return $"{result[..3]}-{result[3..6]}-{result[6..]}";
        }

        return $"{result[..3]}-{result[3..]}";
    }

    #endregion

    #region Genesis Game Genie

    private static CheatCodeResult DecodeGenesisGameGenie(string code)
    {
        if (code.Length != 8)
            throw new ArgumentException("Genesis Game Genie code must be 8 data characters (XXXX-XXXX).");

        int[] values = new int[8];
        for (int i = 0; i < 8; i++)
        {
            int idx = GenesisGgAlphabet.IndexOf(code[i]);
            if (idx < 0)
                throw new ArgumentException($"Invalid Genesis Game Genie character: {code[i]}");
            values[i] = idx;
        }

        // Decode 24-bit address and 16-bit value (8 chars × 5 bits = 40 bits total)
        ulong encoded = 0;
        for (int i = 0; i < 8; i++)
            encoded = (encoded << 5) | (uint)values[i];

        // Genesis GG interleaves address and value bits
        uint address = (uint)((encoded >> 16) & 0xFFFFFF);
        ushort value = (ushort)(encoded & 0xFFFF);

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X6}, Value: ${value:X4}"
        };
    }

    private static string EncodeGenesisGameGenie(uint address, ushort value)
    {
        ulong encoded = ((ulong)(address & 0xFFFFFF) << 16) | value;

        int[] vals = new int[8];
        for (int i = 7; i >= 0; i--)
        {
            vals[i] = (int)(encoded & 0x1F);
            encoded >>= 5;
        }

        char[] code = new char[9];
        for (int i = 0; i < 4; i++)
            code[i] = GenesisGgAlphabet[vals[i]];
        code[4] = '-';
        for (int i = 4; i < 8; i++)
            code[i + 1] = GenesisGgAlphabet[vals[i]];

        return new string(code);
    }

    #endregion

    #region Pro Action Replay

    private static CheatCodeResult DecodeSnesProActionReplay(string code)
    {
        string clean = code.Replace(" ", "").Replace("-", "").Replace(":", "");
        if (clean.Length != 8)
            throw new ArgumentException("SNES Pro Action Replay code must be 8 hex characters (XXXXXXXX).");

        if (!IsHexString(clean))
            throw new ArgumentException("SNES Pro Action Replay code must contain only hex characters.");

        // Format: AAAAAA:VV (6-digit address + 2-digit value)
        uint address = Convert.ToUInt32(clean[..6], 16);
        byte value = Convert.ToByte(clean[6..8], 16);

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X6}, Value: ${value:X2}"
        };
    }

    private static string EncodeSnesProActionReplay(uint address, byte value)
    {
        return $"{address:X6}{value:X2}";
    }

    private static CheatCodeResult DecodeGenesisProActionReplay(string code)
    {
        string clean = code.Replace(" ", "").Replace("-", "").Replace(":", "");
        if (clean.Length != 10)
            throw new ArgumentException("Genesis Pro Action Replay code must be 10 hex characters (AAAAAA:VVVV).");

        if (!IsHexString(clean))
            throw new ArgumentException("Genesis Pro Action Replay code must contain only hex characters.");

        // Format: AAAAAA:VVVV (6-digit address + 4-digit value)
        uint address = Convert.ToUInt32(clean[..6], 16);
        ushort value = Convert.ToUInt16(clean[6..10], 16);

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X6}, Value: ${value:X4}"
        };
    }

    private static string EncodeGenesisProActionReplay(uint address, ushort value)
    {
        return $"{address:X6}{value:X4}";
    }

    private static CheatCodeResult DecodeGameBoyProActionReplay(string code)
    {
        string clean = code.Replace(" ", "").Replace("-", "").Replace(":", "");
        if (clean.Length != 8)
            throw new ArgumentException("Game Boy Pro Action Replay code must be 8 hex characters (0TVVAAAA).");

        if (!IsHexString(clean))
            throw new ArgumentException("Game Boy Pro Action Replay code must contain only hex characters.");

        // Format: 0TVVAAAA (T=type, VV=value, AAAA=address)
        byte type = Convert.ToByte(clean[1..2], 16);
        byte value = Convert.ToByte(clean[2..4], 16);
        uint address = Convert.ToUInt16(clean[4..8], 16);

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X4}, Value: ${value:X2}, Type: {type:X1}"
        };
    }

    private static string EncodeGameBoyProActionReplay(uint address, byte value)
    {
        return $"01{value:X2}{address:X4}";
    }

    #endregion

    #region Master System Pro Action Replay

    private static CheatCodeResult DecodeMasterSystemProActionReplay(string code)
    {
        string clean = code.Replace(" ", "").Replace("-", "").Replace(":", "");
        if (clean.Length != 6)
            throw new ArgumentException("Master System Pro Action Replay code must be 6 hex characters (XXXX-XX).");

        if (!IsHexString(clean))
            throw new ArgumentException("Master System Pro Action Replay code must contain only hex characters.");

        // Format: XXXX-VV (4-digit address + 2-digit value)
        uint address = Convert.ToUInt16(clean[..4], 16);
        byte value = Convert.ToByte(clean[4..6], 16);

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X4}, Value: ${value:X2}"
        };
    }

    private static string EncodeMasterSystemProActionReplay(uint address, byte value)
    {
        return $"{address:X4}-{value:X2}";
    }

    #endregion

    #region N64 GameShark

    private static CheatCodeResult DecodeN64GameShark(string code)
    {
        string clean = code.Replace(" ", "").Replace("-", "").Replace(":", "");
        if (clean.Length != 12)
            throw new ArgumentException("N64 GameShark code must be 12 hex characters (TTAAAAAA VVVV).");

        if (!IsHexString(clean))
            throw new ArgumentException("N64 GameShark code must contain only hex characters.");

        // Format: TTAAAAAA VVVV
        // TT = code type (80 = 8-bit write, 81 = 16-bit write, etc.)
        byte codeType = Convert.ToByte(clean[..2], 16);
        uint address = Convert.ToUInt32(clean[2..8], 16);
        ushort value = Convert.ToUInt16(clean[8..12], 16);

        string typeName = codeType switch
        {
            0x80 => "Write 8-bit",
            0x81 => "Write 16-bit",
            0xA0 => "Write 8-bit (uncached)",
            0xA1 => "Write 16-bit (uncached)",
            0x50 => "Repeat/Serial",
            0xD0 => "If equal 8-bit",
            0xD1 => "If equal 16-bit",
            0xD2 => "If not equal 8-bit",
            0xD3 => "If not equal 16-bit",
            _ => $"Type 0x{codeType:X2}"
        };

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X6}, Value: ${value:X4}, Type: {typeName} (0x{codeType:X2})"
        };
    }

    private static string EncodeN64GameShark(uint address, ushort value)
    {
        // N64 GameShark format supports 24-bit addresses (6 hex digits) only.
        // Auto-selects type 81 (16-bit write) if value > 0xFF, otherwise type 80 (8-bit write).
        // For other code types (D0/D1 conditionals, 50 repeat, A0/A1 uncached writes),
        // users should construct the code string directly.
        byte codeType = value > 0xFF ? (byte)0x81 : (byte)0x80;

        return $"{codeType:X2}{address & 0xFFFFFF:X6} {value:X4}";
    }

    #endregion

    #region GBA GameShark / Action Replay

    private static CheatCodeResult DecodeGbaGameShark(string code)
    {
        string clean = code.Replace(" ", "").Replace("-", "").Replace(":", "");
        if (clean.Length != 16)
            throw new ArgumentException("GBA GameShark/Action Replay code must be 16 hex characters (XXXXXXXX YYYYYYYY).");

        if (!IsHexString(clean))
            throw new ArgumentException("GBA GameShark/Action Replay code must contain only hex characters.");

        // Format: TTAAAAAA VVVVVVVV
        // First 8 hex chars = code type (2) + address (6)
        // Last 8 hex chars = value (up to 32 bits depending on code type)
        uint firstWord = Convert.ToUInt32(clean[..8], 16);
        uint secondWord = Convert.ToUInt32(clean[8..16], 16);

        byte codeType = (byte)((firstWord >> 24) & 0xFF);
        uint address = firstWord & 0x0FFFFFFF;
        // Store lower 16 bits in Value for encode round-trip (8-bit and 16-bit writes);
        // the full 32-bit value is shown in the Description for informational purposes.
        ushort value = (ushort)(secondWord & 0xFFFF);

        string typeName = codeType switch
        {
            0x00 => "32-bit Write",
            0x02 => "16-bit Write",
            0x03 => "8-bit Write",
            0x04 => "Slide Code",
            0x08 => "16-bit Write (ROM Patch)",
            0x80 => "8-bit GS Button Write",
            0x82 => "16-bit GS Button Write",
            0xD0 => "16-bit If Equal",
            0xD2 => "16-bit If Not Equal",
            0xE0 => "8-bit If Equal",
            0xE2 => "8-bit If Not Equal",
            _ => $"Type 0x{codeType:X2}"
        };

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X8}, Value: ${secondWord:X8}, Type: {typeName} (0x{codeType:X2})"
        };
    }

    private static string EncodeGbaGameShark(uint address, ushort value)
    {
        // Default to type 0x03 (8-bit write) for values <= 0xFF, 0x02 (16-bit write) otherwise.
        byte codeType = value > 0xFF ? (byte)0x02 : (byte)0x03;
        uint firstWord = (uint)(codeType << 24) | (address & 0x0FFFFFFF);

        return $"{firstWord:X8} {(uint)value:X8}";
    }

    #endregion

    #region Raw Address:Value (PC Engine, Neo Geo Pocket)

    private static CheatCodeResult DecodeRawAddressValue(string code, string systemName)
    {
        string clean = code.Replace(" ", "").Replace("-", "").Replace(":", "");
        if (clean.Length != 8)
            throw new ArgumentException($"{systemName} code must be 8 hex characters (AAAAAA:VV).");

        if (!IsHexString(clean))
            throw new ArgumentException($"{systemName} code must contain only hex characters.");

        // Format: AAAAAA:VV (6-digit address + 2-digit value)
        uint address = Convert.ToUInt32(clean[..6], 16);
        byte value = Convert.ToByte(clean[6..8], 16);

        return new CheatCodeResult
        {
            Address = address,
            Value = value,
            Description = $"Address: ${address:X6}, Value: ${value:X2}"
        };
    }

    private static string EncodeRawAddressValue(uint address, byte value)
    {
        return $"{address & 0xFFFFFF:X6}:{value:X2}";
    }

    #endregion

    private static bool IsHexString(string s) =>
        !string.IsNullOrEmpty(s) &&
        s.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));
}

public class CheatCodeResult
{
    public uint Address { get; set; }
    public ushort Value { get; set; }
    public byte? CompareValue { get; set; }
    public string Description { get; set; } = string.Empty;
}
