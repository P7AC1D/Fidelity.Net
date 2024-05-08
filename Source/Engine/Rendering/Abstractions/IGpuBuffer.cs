using Fidelity.Rendering.Enums;
using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Vulkan.Abstractions;

public interface IGpuBuffer
{
  bool Allocated { get; }
  ulong SizeBytes { get; }

  void Allocate(BufferType gpuBufferType, ulong sizeBytes);

  void WriteData<T>(T data) where T : struct;
  void WriteDataArray<T>(T[] data) where T : struct;

  void CopyData(IGpuBuffer destination, ulong sizeBytes, CommandPool commandPool, Queue graphicsQueue);

  void Deallocate();
}