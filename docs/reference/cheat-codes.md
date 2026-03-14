# Cheat Code Reference

Detailed reference for all cheat code formats supported by the Cheat Code Converter.

---

## Game Genie

### NES Game Genie

| Format | Length | Fields |
|---|---|---|
| `AAAAAA` | 6 chars | Address + Value |
| `AAAAAAAA` | 8 chars | Address + Value + Compare |

The NES Game Genie uses a custom letter encoding: `APZLGITYEOXUKSVN`.

**Decoding:**
- 6-letter codes modify a single byte at a specific address.
- 8-letter codes include a compare value — the byte is only replaced if the current value matches.

### SNES Game Genie

| Format | Length | Fields |
|---|---|---|
| `AAAA-AAAA` | 8 chars + hyphen | Address + Value |

Uses a different letter set from NES. The address is scrambled with a bit-rotation algorithm.

### Game Boy / GBC Game Genie

| Format | Length | Fields |
|---|---|---|
| `AAA-AAA` | 6 chars | Address + Value |
| `AAA-AAA-AAA` | 9 chars | Address + Value + Compare |

### Genesis Game Genie

| Format | Length | Fields |
|---|---|---|
| `AAAA-AAAA` | 8 chars + hyphen | Address + Value |

### Game Gear Game Genie

| Format | Length | Fields |
|---|---|---|
| `AAA-AAA` | 6 chars | Address + Value |
| `AAA-AAA-AAA` | 9 chars (with compare) | Address + Value + Compare |

---

## Pro Action Replay

### Format

All Pro Action Replay codes use an `AAAAAA:VVVV` format (hex address and hex value), though the exact address width varies by system.

### Supported Systems

| System | Address Width | Value Width |
|---|---|---|
| SNES | 24-bit | 8-bit |
| Genesis | 24-bit | 16-bit |
| Game Boy / GBC | 16-bit | 8-bit |
| Master System | 16-bit | 8-bit |
| Sega 32X | 24-bit | 16-bit |
| Sega CD | 24-bit | 16-bit |

---

## N64 GameShark

### Format

```
TTAAAAAA VVVV
```

- `TT` — code type
- `AAAAAA` — memory address
- `VVVV` — value

### Code Types

| Type | Description |
|---|---|
| `80` | Write 8-bit value to address |
| `81` | Write 16-bit value to address |
| `A0` | Write 8-bit value (uncached address space) |
| `A1` | Write 16-bit value (uncached address space) |
| `D0` | 8-bit equal conditional — next code only activates if `[address] == value` |
| `D1` | 16-bit equal conditional |
| `D2` | 8-bit not-equal conditional |
| `D3` | 16-bit not-equal conditional |
| `50` | Repeat/serial — applies the next code multiple times with incrementing addresses |

---

## GBA GameShark / Action Replay

### Format

```
TTAAAAAA VVVVVVVV
```

Supports 12 code types including:

- 8-bit and 16-bit constant writes
- Conditional codes (if equal, if not equal, if less than, if greater than)
- Multi-line patches
- Master code enable
- Slow-motion toggles

---

## GBC GameShark

### Format

```
AABBCCDD
```

8-character hex code encoding address and value for Game Boy Color GameShark devices.

---

## PC Engine

### Format

```
AAAA:VV
```

Raw address:value pairs in hexadecimal. The address is a direct memory location and the value is the byte to write.

---

## Neo Geo Pocket GameShark

### Format

```
AAAAAA:VV
```

24-bit address with 8-bit value in hexadecimal.
