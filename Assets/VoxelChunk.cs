using System;
using Unity.Collections;
using Unity.Mathematics;

public struct VoxelChunk : IDisposable
{
    public const int CHUNK_SIZE = 64;
    public const int STRIDE_Y = CHUNK_SIZE;
    public const int STRIDE_Z = CHUNK_SIZE * CHUNK_SIZE;
    public const int CHUNK_LENGTH = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE;

    public static readonly int3 Size = new int3(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);
    public static readonly int3 Stride = new int3(1, STRIDE_Y, STRIDE_Z);

    public readonly int3 Position;

    private NativeArray<VoxelData> _data;

    public float3 WorldPosition => Position * Size;

    public static int3 IndexToPosition(int i)
    {
        var x = i % STRIDE_Z % STRIDE_Y;
        var y = i % STRIDE_Z / STRIDE_Y;
        var z = i / STRIDE_Z;
        return new int3(x, y, z);
    }

    public VoxelChunk(int3 position)
    {
        _data = new NativeArray<VoxelData>(CHUNK_LENGTH, Allocator.Persistent);
        Position = position;
    }

    public VoxelData this[int x, int y, int z]
    {
        get => _data[x + y * STRIDE_Y + z * STRIDE_Z];
        set => _data[x + y * STRIDE_Y + z * STRIDE_Z] = value;
    }

    public VoxelData this[int i]
    {
        get => _data[i];
        set => _data[i] = value;
    }

    public VoxelData this[int3 pos]
    {
        get => _data[math.dot(pos, Stride)];
        set => _data[math.dot(pos, Stride)] = value;
    }

    public override int GetHashCode() => Position.GetHashCode();

    public void Dispose()
    {
        _data.Dispose();
    }
}
