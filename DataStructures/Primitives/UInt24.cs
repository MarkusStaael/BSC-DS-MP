using System;
using System.Collections.Generic;
using System.Text;

namespace BSC_DS_MP.DataStructures.Primitives;
// SRC: https://stackoverflow.com/questions/12549197/are-there-any-int24-implementations-in-c
public readonly struct UInt24 {
    private readonly Byte b0;
    private readonly Byte b1;
    private readonly Byte b2;

    public UInt24(UInt32 value) {
        this.b0 = (byte)((value) & 0xFF);
        this.b1 = (byte)((value >> 8) & 0xFF);
        this.b2 = (byte)((value >> 16) & 0xFF);
    }

    //public Byte* Byte0 { get { return &b0; } }

    public UInt32 Value { get { return (uint)(b0 | (b1 << 8) | (b2 << 16)); } }
}