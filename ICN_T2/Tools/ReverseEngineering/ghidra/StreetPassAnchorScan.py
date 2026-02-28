#@category YoKaiWatch2
#@menupath Tools.YW2 StreetPass Anchor Scan

"""
Ghidra helper script for YW2 StreetPass reverse engineering.

Purpose:
- Locate anchor strings (cecd:u, cecd:s, common_enc_0.03a, yokaispot_common_0.03c)
- Print direct references to each anchor
- Locate likely IPC header constants in memory

Usage:
1. Load/decompile 00040000001B2A00.code.bin in Ghidra.
2. Run this script from Script Manager.
3. Use printed addresses as starting points for call graph tracing.
"""

from ghidra.program.model.symbol import RefType


ANCHOR_STRINGS = [
    "cecd:u",
    "cecd:s",
    "data/res/battle/common_enc_0.03a.cfg.bin",
    "data/res/map/yokaispot_common_0.03c.cfg.bin",
    "data/res/map/yokaispot_common_menu_0.03c.cfg.bin",
]

IPC_HEADERS = [
    0x000D0082,
    0x00100042,
    0x00110104,
]


def find_string_data(text):
    listing = currentProgram.getListing()
    it = listing.getDefinedData(True)
    results = []
    while it.hasNext():
        data = it.next()
        try:
            if data.hasStringValue():
                value = data.getValue()
                if value is not None and text in str(value):
                    results.append(data)
        except:
            pass
    return results


def print_string_refs(label, data_items):
    print("")
    print("=== {} ===".format(label))
    if len(data_items) == 0:
        print("  (not found)")
        return
    refman = currentProgram.getReferenceManager()
    for data in data_items:
        addr = data.getAddress()
        print("  STRING @ {}".format(addr))
        refs = refman.getReferencesTo(addr)
        count = 0
        for ref in refs:
            from_addr = ref.getFromAddress()
            print("    XREF <- {} ({})".format(from_addr, ref.getReferenceType()))
            count += 1
        if count == 0:
            print("    (no direct references)")


def find_u32_constant(value):
    mem = currentProgram.getMemory()
    pattern = bytearray([
        value & 0xFF,
        (value >> 8) & 0xFF,
        (value >> 16) & 0xFF,
        (value >> 24) & 0xFF
    ])
    addr = currentProgram.getMinAddress()
    hits = []
    while True:
        found = mem.findBytes(addr, bytes(pattern), None, True, monitor)
        if found is None:
            break
        hits.append(found)
        addr = found.next()
    return hits


def main():
    print("YW2 StreetPass Anchor Scan starting...")

    for token in ANCHOR_STRINGS:
        items = find_string_data(token)
        print_string_refs(token, items)

    print("")
    print("=== IPC Header Constants (Little Endian) ===")
    for header in IPC_HEADERS:
        hits = find_u32_constant(header)
        print("  {} count={}".format(hex(header), len(hits)))
        for addr in hits[:20]:
            print("    {}".format(addr))

    print("")
    print("Done. Next step: identify caller chains reaching SVC SendSyncRequest wrappers.")


main()
