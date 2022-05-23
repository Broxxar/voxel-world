using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(CompileSynchronously = true)]
public struct VoxelTerrainGenJob : IJobParallelFor
{
    public VoxelChunk Chunk;
    public float Cutoff;
    public float Scale;
    public float3 Offset;
    public float Power;

    public void Execute(int i)
    {
        var voxelPosition = VoxelChunk.IndexToPosition(i);
        var noisePosition = Chunk.WorldPosition + voxelPosition + Offset;
        var noiseSample = math.pow(math.abs(noise.snoise(noisePosition.xz * Scale)), Power);

        Chunk[i] = noiseSample * Cutoff * 64.0f >= noisePosition.y ? new float3(0.4f, 1.0f, 0.2f) : new VoxelData();
    }
}
