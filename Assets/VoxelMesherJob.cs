using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile(CompileSynchronously = true)]
public struct VoxelMesherComputeVisibleFacesJob : IJob
{
    [ReadOnly] public VoxelChunk Chunk;
    public NativeArray<VoxelMesherPrePassData> PrePassDataStream;
    public NativeArray<int> VertexCount;
    [ReadOnly] public VoxelChunk ChunkNeigborPosX;
    [ReadOnly] public VoxelChunk ChunkNeigborNegX;
    [ReadOnly] public VoxelChunk ChunkNeigborPosY;
    [ReadOnly] public VoxelChunk ChunkNeigborNegY;
    [ReadOnly] public VoxelChunk ChunkNeigborPosZ;
    [ReadOnly] public VoxelChunk ChunkNeigborNegZ;

    public void Execute()
    {
        var vertexOffset = 0;
        for (var i = 0; i < PrePassDataStream.Length; i++)
        {
            PrePassDataStream[i] = ComputePrePassData(VoxelChunk.IndexToPosition(i), ref vertexOffset);
        }

        VertexCount[0] = vertexOffset;
    }

    private VoxelMesherPrePassData ComputePrePassData(int3 pos, ref int vertexOffset)
    {
        if (Chunk[pos].IsTransparent)
        {
            return default;
        }

        var prePassData = new VoxelMesherPrePassData
        {
            VertexOffset = vertexOffset,
            VisibleFaces = 0
        };

        prePassData.VisibleFaces |= VisibleFacePositive(pos, new int3(1, 0, 0), 0, 1 << 0, ref vertexOffset, ChunkNeigborPosX);
        prePassData.VisibleFaces |= VisibleFaceNegative(pos, new int3(1, 0, 0), 0, 1 << 1, ref vertexOffset, ChunkNeigborNegX);
        prePassData.VisibleFaces |= VisibleFacePositive(pos, new int3(0, 1, 0), 1, 1 << 2, ref vertexOffset, ChunkNeigborPosY);
        prePassData.VisibleFaces |= VisibleFaceNegative(pos, new int3(0, 1, 0), 1, 1 << 3, ref vertexOffset, ChunkNeigborNegY);
        prePassData.VisibleFaces |= VisibleFacePositive(pos, new int3(0, 0, 1), 2, 1 << 4, ref vertexOffset, ChunkNeigborPosZ);
        prePassData.VisibleFaces |= VisibleFaceNegative(pos, new int3(0, 0, 1), 2, 1 << 5, ref vertexOffset, ChunkNeigborNegZ);

        return prePassData;
    }

    private byte VisibleFacePositive(int3 pos, int3 dir, int idx, byte faceId, ref int vertexCount, VoxelChunk neigbor)
    {
        var adj = pos + dir;
        if (adj[idx] < VoxelChunk.Size[idx])
        {
            if (Chunk[adj].IsTransparent)
            {
                vertexCount += 4;
                return faceId;
            }

            return 0;
        }

        adj[idx] -= VoxelChunk.Size[idx];
        if (neigbor[adj].IsTransparent)
        {
            vertexCount += 4;
            return faceId;
        }

        return 0;
    }

    private byte VisibleFaceNegative(int3 pos, int3 dir, int idx, byte faceId, ref int vertexCount, VoxelChunk neigbor)
    {
        var adj = pos - dir;
        if (adj[idx] >= 0)
        {
            if (Chunk[adj].IsTransparent)
            {
                vertexCount += 4;
                return faceId;
            }

            return 0;
        }

        adj[idx] += VoxelChunk.Size[idx];
        if (neigbor[adj].IsTransparent)
        {
            vertexCount += 4;
            return faceId;
        }

        return 0;
    }
}

public struct VoxelMesherPrePassData
{
    public int VertexOffset;
    public byte VisibleFaces;
}

public struct VoxelMesherVertex
{
    public float3 Position;
    public float3 Normal;
    public half4 Color;
}

[BurstCompile(CompileSynchronously = true)]
public struct VoxelMesherCreateMeshDataStreams : IJob
{
    [ReadOnly] public NativeArray<VertexAttributeDescriptor> VertexAttributeDescriptors;
    [ReadOnly] public NativeArray<VoxelMesherPrePassData> PrePassDataStream;
    [ReadOnly] public NativeArray<int> VertexCount;
    public Mesh.MeshData MeshData;

    public void Execute()
    {
        var vertexCount = VertexCount[0];
        var indexCount = vertexCount + vertexCount / 2;
        MeshData.SetVertexBufferParams(vertexCount, VertexAttributeDescriptors);
        MeshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
    }
}

[BurstCompile(CompileSynchronously = true)]
public struct VoxelMesherMeshGenerationJob : IJobParallelFor
{
    [ReadOnly] public VoxelChunk Chunk;
    [ReadOnly] public NativeArray<VoxelMesherPrePassData> PrePassDataStream;
    [ReadOnly] public Mesh.MeshData MeshData;

    public void Execute(int i)
    {
        var voxelData = Chunk[i];
        if (voxelData.IsTransparent)
        {
            return;
        }

        var vertices = MeshData.GetVertexData<VoxelMesherVertex>();
        var indices = MeshData.GetIndexData<int>();
        var prePassData = PrePassDataStream[i];
        var vertexOffset = prePassData.VertexOffset;
        var indexOffset = vertexOffset + vertexOffset / 2;
        var color = voxelData.VertexColor;
        var pos = (float3)VoxelChunk.IndexToPosition(i);

        // Positive X
        if ((prePassData.VisibleFaces & 1 << 0) == 1 << 0)
        {
            vertices[vertexOffset + 0] = Vertex(pos + new float3(1, 0, 0), new float3(1.0f, 0.0f, 0.0f), color);
            vertices[vertexOffset + 1] = Vertex(pos + new float3(1, 1, 0), new float3(1.0f, 0.0f, 0.0f), color);
            vertices[vertexOffset + 2] = Vertex(pos + new float3(1, 1, 1), new float3(1.0f, 0.0f, 0.0f), color);
            vertices[vertexOffset + 3] = Vertex(pos + new float3(1, 0, 1), new float3(1.0f, 0.0f, 0.0f), color);
            QuadIndices(indices, ref indexOffset, ref vertexOffset);
        }

        // Negative X
        if ((prePassData.VisibleFaces & 1 << 1) == 1 << 1)
        {
            vertices[vertexOffset + 0] = Vertex(pos + new float3(0, 0, 1), new float3(-1.0f, 0.0f, 0.0f), color);
            vertices[vertexOffset + 1] = Vertex(pos + new float3(0, 1, 1), new float3(-1.0f, 0.0f, 0.0f), color);
            vertices[vertexOffset + 2] = Vertex(pos + new float3(0, 1, 0), new float3(-1.0f, 0.0f, 0.0f), color);
            vertices[vertexOffset + 3] = Vertex(pos + new float3(0, 0, 0), new float3(-1.0f, 0.0f, 0.0f), color);
            QuadIndices(indices, ref indexOffset, ref vertexOffset);
        }

        // Positive Y
        if ((prePassData.VisibleFaces & 1 << 2) == 1 << 2)
        {
            vertices[vertexOffset + 0] = Vertex(pos + new float3(0, 1, 1), new float3(0.0f, 1.0f, 0.0f), color);
            vertices[vertexOffset + 1] = Vertex(pos + new float3(1, 1, 1), new float3(0.0f, 1.0f, 0.0f), color);
            vertices[vertexOffset + 2] = Vertex(pos + new float3(1, 1, 0), new float3(0.0f, 1.0f, 0.0f), color);
            vertices[vertexOffset + 3] = Vertex(pos + new float3(0, 1, 0), new float3(0.0f, 1.0f, 0.0f), color);
            QuadIndices(indices, ref indexOffset, ref vertexOffset);
        }

        // Negative Y
        if ((prePassData.VisibleFaces & 1 << 3) == 1 << 3)
        {
            vertices[vertexOffset + 0] = Vertex(pos + new float3(1, 0, 1), new float3(0.0f, -1.0f, 0.0f), color);
            vertices[vertexOffset + 1] = Vertex(pos + new float3(0, 0, 1), new float3(0.0f, -1.0f, 0.0f), color);
            vertices[vertexOffset + 2] = Vertex(pos + new float3(0, 0, 0), new float3(0.0f, -1.0f, 0.0f), color);
            vertices[vertexOffset + 3] = Vertex(pos + new float3(1, 0, 0), new float3(0.0f, -1.0f, 0.0f), color);
            QuadIndices(indices, ref indexOffset, ref vertexOffset);
        }

        // Positive Z
        if ((prePassData.VisibleFaces & 1 << 4) == 1 << 4)
        {
            vertices[vertexOffset + 0] = Vertex(pos + new float3(1, 0, 1), new float3(0.0f, 0.0f, 1.0f), color);
            vertices[vertexOffset + 1] = Vertex(pos + new float3(1, 1, 1), new float3(0.0f, 0.0f, 1.0f), color);
            vertices[vertexOffset + 2] = Vertex(pos + new float3(0, 1, 1), new float3(0.0f, 0.0f, 1.0f), color);
            vertices[vertexOffset + 3] = Vertex(pos + new float3(0, 0, 1), new float3(0.0f, 0.0f, 1.0f), color);
            QuadIndices(indices, ref indexOffset, ref vertexOffset);
        }

        // Negative Z
        if ((prePassData.VisibleFaces & 1 << 5) == 1 << 5)
        {
            vertices[vertexOffset + 0] = Vertex(pos + new float3(0, 0, 0), new float3(0.0f, 0.0f, -1.0f), color);
            vertices[vertexOffset + 1] = Vertex(pos + new float3(0, 1, 0), new float3(0.0f, 0.0f, -1.0f), color);
            vertices[vertexOffset + 2] = Vertex(pos + new float3(1, 1, 0), new float3(0.0f, 0.0f, -1.0f), color);
            vertices[vertexOffset + 3] = Vertex(pos + new float3(1, 0, 0), new float3(0.0f, 0.0f, -1.0f), color);
            QuadIndices(indices, ref indexOffset, ref vertexOffset);
        }
    }

    private VoxelMesherVertex Vertex(float3 posiiton, float3 normal, half4 color)
    {
        return new VoxelMesherVertex
        {
            Position = posiiton,
            Normal = normal,
            Color = color
        };
    }

    private void QuadIndices(NativeArray<int> indices, ref int indexOffset, ref int vertexOffset)
    {
        indices[indexOffset + 0] = vertexOffset + 0;
        indices[indexOffset + 1] = vertexOffset + 1;
        indices[indexOffset + 2] = vertexOffset + 2;
        indices[indexOffset + 3] = vertexOffset + 0;
        indices[indexOffset + 4] = vertexOffset + 2;
        indices[indexOffset + 5] = vertexOffset + 3;
        indexOffset += 6;
        vertexOffset += 4;
    }
}

[BurstCompile(CompileSynchronously = true)]
public struct VoxelMesherFinalizeJob : IJob
{
    public Mesh.MeshData MeshData;

    public void Execute()
    {
        var indexCount = MeshData.vertexCount + MeshData.vertexCount / 2;
        MeshData.subMeshCount = 1;
        MeshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount));
    }
}
