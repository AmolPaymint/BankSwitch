#!/usr/bin/env python3
"""Send a v21 development ISO 8583 0200 request to the source gateway.
The script uses the same ASCII MTI + hex bitmap + field data framing as Iso8583AsciiBitmapFormatter.
"""
import socket
import struct
import sys

SPECS = {
    2: ('ll', 19), 3: ('fixed', 6), 4: ('fixed', 12), 7: ('fixed', 10),
    11: ('fixed', 6), 14: ('fixed', 4), 22: ('fixed', 3), 37: ('fixed', 12),
    39: ('fixed', 2), 41: ('fixed', 8), 49: ('fixed', 3), 64: ('fixed', 16), 90: ('fixed', 42), 123: ('lll', 999)
}

def _bitmap_range(fields, start):
    bits = [0] * 64
    for f in fields:
        if start <= f <= start + 63:
            bits[f - start] = 1
    out = bytearray(8)
    for i, b in enumerate(bits):
        if b:
            out[i // 8] |= 0x80 >> (i % 8)
    return out.hex().upper()

def bitmap(fields):
    fields = set(fields)
    if any(f > 64 for f in fields):
        fields.add(1)
        return _bitmap_range(fields, 1) + _bitmap_range(fields, 65)
    return _bitmap_range(fields, 1)

def enc_field(n, v):
    kind, size = SPECS[n]
    if kind == 'fixed':
        if len(v) != size: raise ValueError(f'field {n} must be {size} chars')
        return v
    if kind == 'll':
        return f'{len(v):02d}' + v
    return f'{len(v):03d}' + v

def build():
    fields = {
        2: '5399838383838381',
        3: '000000',
        4: '000000001000',
        7: '0528061300',
        11: '123456',
        14: '3001',
        22: '051',
        37: '123456789012',
        41: 'TERM0001',
        49: '566',
        123: '000000000000001',
    }
    body = '0200' + bitmap(fields.keys()) + ''.join(enc_field(k, fields[k]) for k in sorted(fields))
    raw = body.encode('ascii')
    return struct.pack('!H', len(raw)) + raw

def main():
    host = sys.argv[1] if len(sys.argv) > 1 else '127.0.0.1'
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 5050
    with socket.create_connection((host, port), timeout=10) as s:
        s.sendall(build())
        header = s.recv(2)
        if len(header) != 2: raise RuntimeError('no response header')
        length = struct.unpack('!H', header)[0]
        response = s.recv(length)
        print(response.decode('ascii'))

if __name__ == '__main__':
    main()
