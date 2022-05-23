using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class VoxelChunkRenderer : IDisposable
{
    public static NativeArray<VertexAttributeDescriptor> VertexAttributeDescriptors;
    public static Material WorldMaterial;

    private readonly Mesh _mesh;
    private readonly VoxelChunk[] _neighbors = new VoxelChunk[6];

    private NativeArray<VoxelMesherPrePassData> _prePassDataStream;
    private NativeArray<int> _vertexCount;
    private VoxelChunk _voxelChunk;
    private Matrix4x4 _objectToWorldMatrix;
    
    public Mesh Mesh => _mesh;

    public VoxelChunkRenderer()
    {
        _mesh = new Mesh
        {
            bounds = new Bounds
            {
                min = new Vector3(0, 0, 0),
                max = (float3)VoxelChunk.Size
            }
        };

        _prePassDataStream = new NativeArray<VoxelMesherPrePassData>(VoxelChunk.CHUNK_LENGTH, Allocator.Persistent);
        _vertexCount = new NativeArray<int>(1, Allocator.Persistent);
    }

    public void SetChunk(VoxelChunk voxelChunk)
    {
        _voxelChunk = voxelChunk;
        _objectToWorldMatrix = Matrix4x4.Translate(_voxelChunk.WorldPosition);
    }

    public void SetNeighbor(VoxelChunk voxelChunk, int siblingIndex)
    {
        _neighbors[siblingIndex] = voxelChunk;
    }

    public JobHandle ScheduleMeshUpdate(Mesh.MeshData meshData, JobHandle chunkDependency)
    {
        var visibleFacesJob = new VoxelMesherComputeVisibleFacesJob
        {
            Chunk = _voxelChunk,
            PrePassDataStream = _prePassDataStream,
            VertexCount = _vertexCount,
            ChunkNeigborPosX = _neighbors[0],
            ChunkNeigborNegX = _neighbors[1],
            ChunkNeigborPosY = _neighbors[2],
            ChunkNeigborNegY = _neighbors[3],
            ChunkNeigborPosZ = _neighbors[4],
            ChunkNeigborNegZ = _neighbors[5]
        };

        var meshDataStreamsJob = new VoxelMesherCreateMeshDataStreams
        {
            MeshData = meshData,
            VertexAttributeDescriptors = VertexAttributeDescriptors,
            PrePassDataStream = _prePassDataStream,
            VertexCount = _vertexCount
        };

        var meshGenerationJob = new VoxelMesherMeshGenerationJob
        {
            MeshData = meshData,
            Chunk = _voxelChunk,
            PrePassDataStream = _prePassDataStream
        };

        var meshFinalizeJob = new VoxelMesherFinalizeJob
        {
            MeshData = meshData
        };

        var visibleFacesHandle = visibleFacesJob.Schedule(chunkDependency);
        var meshDataStreamsHandle = meshDataStreamsJob.Schedule(visibleFacesHandle);
        var meshGenerationHandle = meshGenerationJob.Schedule(VoxelChunk.CHUNK_LENGTH, VoxelChunk.STRIDE_Z, meshDataStreamsHandle);
        var meshFinalizeHandle = meshFinalizeJob.Schedule(meshGenerationHandle);

        return meshFinalizeHandle;
    }

    public void Draw()
    {
        Graphics.DrawMesh(_mesh, _objectToWorldMatrix, WorldMaterial, 0);
    }

    public void Dispose()
    {
        Object.DestroyImmediate(_mesh);
        _prePassDataStream.Dispose();
        _vertexCount.Dispose();
    }
}
