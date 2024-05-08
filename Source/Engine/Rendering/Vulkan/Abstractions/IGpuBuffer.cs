using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Vulkan.Abstractions;

public interface IGpuBuffer
{
  public bool Allocated { get; }
  public ulong SizeBytes { get; }

  public void Allocate(BufferType gpuBufferType, ulong sizeBytes);

  public void WriteData<T>(T data) where T : struct;
  public void WriteDataArray<T>(T[] data) where T : struct;

  public void CopyData(VkBuffer destination, ulong sizeBytes, CommandPool commandPool, Queue graphicsQueue);
}