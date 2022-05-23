using System;
using Unity.Mathematics;
using UnityEngine;

public struct VoxelData
{
    [Flags]
    private enum VoxelMetaData : byte
    {
        Opaque = 1 << 0,
    }

    private readonly VoxelMetaData MetaData;
    private readonly byte R;
    private readonly byte G;
    private readonly byte B;

    private VoxelData(byte r, byte g, byte b)
    {
        MetaData = VoxelMetaData.Opaque;
        R = r;
        G = g;
        B = b;
    }

    public half4 VertexColor => (half4)new float4(R / 255.0f, G / 255.0f, B / 255.0f, 1.0f);

    public bool IsTransparent => (MetaData & VoxelMetaData.Opaque) != VoxelMetaData.Opaque;

    public static implicit operator VoxelData(Color color) => new VoxelData((byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255));
    public static implicit operator VoxelData(float3 color) => new VoxelData((byte)(color.x * 255), (byte)(color.y * 255), (byte)(color.z * 255));
}
