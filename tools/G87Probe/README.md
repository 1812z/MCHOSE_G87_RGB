# G87 descriptor inventory

Run with .NET 10: `dotnet run --project tools/G87Probe/G87Probe.csproj -c Release`.
This tool only reads device identity/report descriptors. It does not open HID
streams or transmit lighting commands.

## Observed 2026-10-21

- Keyboard receiver: VID `41E4`, PID `2001`, product `MCHOSE 2.4G Wireless`, manufacturer `Compx`.
- Vendor collection `MI_01/COL01`: usage `FF020002`, input/output maximum 20 bytes.
- Vendor collection `MI_01/COL06`: usage `FF040002`, feature maximum 8 bytes.
- No 65-byte output collection was exposed by this receiver.
- VID `3837`, PID `4245` is the separate `MCHOSE G3 V2` mouse, not the keyboard.

## Reference

https://github.com/mdsaad23/mchose_mix87_signalrgb_plugin/blob/main/MCHOSE_Mix87.js

Reference MIX87 identity: `41E4:2122`, interface 1. Its code sends seven
65-byte output reports per frame: report ID 0, header `55 DD 00`, checksum,
56-byte payload length, little-endian byte offset, zero byte, RGB payload.
The checksum is the low byte of the sum of packet bytes 5 through 64.
The RGB buffer is 392 bytes. Reported extra TKL indices are partly estimated;
source comments conflict with portions of the current map, so mapping needs
physical verification.

The observed wireless transport does not match the reference. Replacing its
PID alone is not a working adaptation. Next evidence needed is a USB capture
of the official client's GIF lighting over this receiver, or a descriptor
inventory in wired mode. No lighting compatibility has been demonstrated.
