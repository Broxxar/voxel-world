using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class VoxelChunkManager : MonoBehaviour
{
    [SerializeField] private Material _worldMaterial;
    [SerializeField] [Range(0.0f, 1.0f)] private float _terrainGenCutoff = 0.5f;
    [SerializeField] private float _terrainGenNoiseScale = 0.01f;
    [SerializeField] private float _terrainGenPower = 1.0f;

    private Mesh.MeshDataArray _meshDataArray;
    private NativeArray<JobHandle> _voxelChunkTerrainUpdateJobs;
    private NativeArray<JobHandle> _voxelChunkMeshUpdateJobs;
    private VoxelChunk _nullChunk;
    private readonly Dictionary<int3, VoxelChunk> _voxelChunks = new Dictionary<int3, VoxelChunk>();
    private readonly List<VoxelChunkRenderer> _voxelChunkRenderers = new List<VoxelChunkRenderer>();
    private readonly List<Mesh> _voxelChunkMeshes = new List<Mesh>();
    private readonly List<VoxelChunk> _chunksToUpdate = new List<VoxelChunk>();

    private void OnEnable()
    {
        VoxelChunkRenderer.VertexAttributeDescriptors = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Persistent);
        VoxelChunkRenderer.VertexAttributeDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
        VoxelChunkRenderer.VertexAttributeDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
        VoxelChunkRenderer.VertexAttributeDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float16, 4);
        VoxelChunkRenderer.WorldMaterial = _worldMaterial;

        _nullChunk = new VoxelChunk(int3.zero);

        const int GRID_SIZE = 2;

        for (var x = 0; x < GRID_SIZE; x++)
        {
            for (var y = 0; y < GRID_SIZE; y++)
            {
                for (var z = 0; z < GRID_SIZE; z++)
                {
                    var pos = new int3(x, y, z);
                    var voxelChunk = new VoxelChunk(pos);
                    _voxelChunks.Add(pos, voxelChunk);
                }
            }
        }

        foreach (var voxelChunk in _voxelChunks.Values)
        {
            var voxelChunkRenderer = new VoxelChunkRenderer();
            voxelChunkRenderer.SetChunk(voxelChunk);
            _voxelChunkRenderers.Add(voxelChunkRenderer);
            _voxelChunkMeshes.Add(voxelChunkRenderer.Mesh);
            voxelChunkRenderer.SetNeighbor(GetChunk(voxelChunk.Position + new int3(1, 0, 0)), 0);
            voxelChunkRenderer.SetNeighbor(GetChunk(voxelChunk.Position - new int3(1, 0, 0)), 1);
            voxelChunkRenderer.SetNeighbor(GetChunk(voxelChunk.Position + new int3(0, 1, 0)), 2);
            voxelChunkRenderer.SetNeighbor(GetChunk(voxelChunk.Position - new int3(0, 1, 0)), 3);
            voxelChunkRenderer.SetNeighbor(GetChunk(voxelChunk.Position + new int3(0, 0, 1)), 4);
            voxelChunkRenderer.SetNeighbor(GetChunk(voxelChunk.Position - new int3(0, 0, 1)), 5);
        }

        ScheduleUpdateForAllLoadedChunks();
    }

    private VoxelChunk GetChunk(int3 position) => _voxelChunks.TryGetValue(position, out var voxelChunk) ? voxelChunk : _nullChunk;

    private void ScheduleUpdateForAllLoadedChunks()
    {
        _chunksToUpdate.Clear();
        _chunksToUpdate.AddRange(_voxelChunks.Values);
    }

    private void Update()
    {
        if (_chunksToUpdate.Count > 0)
        {
            var meshUpdateCount = _voxelChunkRenderers.Count;
            _voxelChunkTerrainUpdateJobs = new NativeArray<JobHandle>(_voxelChunks.Count, Allocator.TempJob);
            _voxelChunkMeshUpdateJobs = new NativeArray<JobHandle>(meshUpdateCount, Allocator.TempJob);
            _meshDataArray = Mesh.AllocateWritableMeshData(meshUpdateCount);

            // Schedule all terrain updates.
            for (var i = 0; i < _chunksToUpdate.Count; i++)
            {
                var chunk = _chunksToUpdate[i];
                var voxelTerrainGenJob = new VoxelTerrainGenJob
                {
                    Chunk = chunk,
                    Cutoff = _terrainGenCutoff,
                    Offset = new float3(math.PI, 0.0f, 0.0f),
                    Scale = _terrainGenNoiseScale * Mathf.PI * 0.01f,
                    Power = _terrainGenPower
                };
                _voxelChunkTerrainUpdateJobs[i] = voxelTerrainGenJob.Schedule(VoxelChunk.CHUNK_LENGTH, VoxelChunk.STRIDE_Z);
            }

            // Combine terrain updates into a single handle as a dependency for mesh generation.
            var terrainUpdatesHandle = JobHandle.CombineDependencies(_voxelChunkTerrainUpdateJobs);

            // Schedule all chunk mesh updates.
            for (var i = 0; i < _voxelChunkRenderers.Count; i++)
            {
                var voxelChunkRenderer = _voxelChunkRenderers[i];
                _voxelChunkMeshUpdateJobs[i] = voxelChunkRenderer.ScheduleMeshUpdate(_meshDataArray[i], terrainUpdatesHandle);
            }
        }
    }

    private void LateUpdate()
    {
        if (_chunksToUpdate.Count > 0)
        {
            JobHandle.CompleteAll(_voxelChunkMeshUpdateJobs);
            Mesh.ApplyAndDisposeWritableMeshData(_meshDataArray, _voxelChunkMeshes);
            _voxelChunkTerrainUpdateJobs.Dispose();
            _voxelChunkMeshUpdateJobs.Dispose();
            _chunksToUpdate.Clear();
        }

        DrawChunkRenderers();
    }

    private void DrawChunkRenderers()
    {
        foreach (var chunkRenderer in _voxelChunkRenderers)
        {
            chunkRenderer.Draw();
        }
    }

    private void OnDisable()
    {
        VoxelChunkRenderer.VertexAttributeDescriptors.Dispose();
        _nullChunk.Dispose();

        foreach (var voxelChunk in _voxelChunks.Values)
        {
            voxelChunk.Dispose();
        }

        foreach (var chunkRenderer in _voxelChunkRenderers)
        {
            chunkRenderer.Dispose();
        }

        _voxelChunks.Clear();
        _voxelChunkRenderers.Clear();
        _voxelChunkMeshes.Clear();
        _chunksToUpdate.Clear();
    }

    private void OnValidate()
    {
        ScheduleUpdateForAllLoadedChunks();
    }
}
