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

---

## Nintendo DS Action Replay

### Format

```
TTAAAAAA VVVVVVVV
```

- `TT` — code type (first byte)
- `AAAAAA` — memory address
- `VVVVVVVV` — value (32-bit)

### Code Types

| Type | Description |
|---|---|
| `0` | 32-bit write |
| `1` | 16-bit write |
| `2` | 8-bit write |
| `3` | If less than (32-bit) |
| `4` | If greater than (32-bit) |
| `5` | If equal (32-bit) |
| `6` | If not equal (32-bit) |
| `7` | If less than (16-bit) |
| `8` | If greater than (16-bit) |
| `9` | If equal (16-bit) |
| `A` | If not equal (16-bit) |
| `B` | Load offset |
| `C` | Loop |
| `D` | Various sub-types (end if/loop, next offset, end code, etc.) |
| `E` | Patch code (multi-byte write) |
| `F` | Memory copy |

Supports 16 code types total. The `D` type contains several sub-type variations based on the address field.

---

## Sega Saturn Action Replay

### Format

```
TTAAAAAA VVVV
```

- `TT` — code type
- `AAAAAA` — 24-bit address
- `VVVV` — 16-bit value

### Code Types

| Type | Description |
|---|---|
| `16` | 16-bit write to address |
| `36` | 8-bit write to address |

The code type byte determines the write width. Address is in the Saturn's memory map.

---

## Sega Dreamcast CodeBreaker

### Format

```
XXXXXXXX YYYYYYYY
```

16 hex characters split into two 32-bit words. The first word encodes the code type and address, the second contains the value.

### Code Types

| Type | Description |
|---|---|
| `0` | 8-bit write |
| `1` | 16-bit write |
| `2` | 32-bit write |
| `3` | Serial / repeat write |
| `9` | Conditional codes |

---

## Game Boy GameShark

### Format

```
AABBCCDD
```

8-character hex code with scrambled address and value encoding, similar to the GBC GameShark format. Address bytes are reordered from the raw code.

---

## Neo Geo Raw

### Format

```
AAAAAAAA VVVV
```

- `AAAAAAAA` — 32-bit memory address
- `VVVV` — 16-bit value

Direct address:value writes for the Neo Geo's 68000-based memory map. Values are 16-bit (word-sized) to match the platform's word-aligned bus.

---

## PlayStation GameShark

### Format

```
TTAAAAAA VVVV
```

or

```
TTAAAAAA VVVVVVVV
```

- `TT` — code type
- `AAAAAA` — 24-bit address
- `VVVV` / `VVVVVVVV` — 16-bit or 32-bit value

### Code Types

| Type | Description |
|---|---|
| `30` | 8-bit constant write |
| `80` | 16-bit constant write |
| `10` | 16-bit increment |
| `11` | 16-bit decrement |
| `20` | 8-bit increment |
| `21` | 8-bit decrement |
| `D0` | 16-bit equal conditional |
| `D1` | 16-bit not-equal conditional |
| `D2` | 16-bit less-than conditional |
| `D3` | 16-bit greater-than conditional |
| `E0` | 8-bit equal conditional |
| `E1` | 8-bit not-equal conditional |
| `E2` | 8-bit less-than conditional |
| `E3` | 8-bit greater-than conditional |
| `50` | Repeat / serial code |
| `C0` | Delay activator |
| `C1` | Button activator |
| `C2` | Master code enable |

Supports 18 code types total. Conditional codes gate the execution of subsequent codes.
