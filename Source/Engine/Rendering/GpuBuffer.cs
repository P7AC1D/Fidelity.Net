using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Fidelity;

public unsafe class GpuBuffer(Device device, PhysicalDevice physicalDevice) : IDisposable, IGpuBuffer
{
  private readonly Vk vk = Vk.GetApi();
  private Buffer buffer = default;
  private DeviceMemory memory = default;

  public bool Allocated { get; private set; } = false;
  public Buffer Buffer { get { return buffer; } }

  public void Allocate(GpuBufferType gpuBufferType, ulong sizeBytes)
  {
    if (Allocated)
    {
      throw new Exception("Buffer has already been allocated.");
    }

    BufferCreateInfo bufferInfo = new()
    {
      SType = StructureType.BufferCreateInfo,
      Size = sizeBytes,
      Usage = MapBufferUsage(gpuBufferType),
      SharingMode = SharingMode.Exclusive,
    };

    fixed (Buffer* bufferPtr = &buffer)
    {
      if (vk!.CreateBuffer(device, bufferInfo, null, bufferPtr) != Result.Success)
      {
        throw new Exception("Failed to create buffer.");
      }
    }

    MemoryRequirements memRequirements = new();
    vk!.GetBufferMemoryRequirements(device, buffer, out memRequirements);

    MemoryAllocateInfo allocateInfo = new()
    {
      SType = StructureType.MemoryAllocateInfo,
      AllocationSize = memRequirements.Size,
      MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MapMemoryProperty(gpuBufferType)),
    };

    fixed (DeviceMemory* bufferMemoryPtr = &memory)
    {
      if (vk!.AllocateMemory(device, allocateInfo, null, bufferMemoryPtr) != Result.Success)
      {
        throw new Exception("Failed to allocate buffer memory.");
      }
    }

    vk!.BindBufferMemory(device, buffer, memory, 0);

    Allocated = true;
  }

  public void WriteData<T>(T data) where T : struct
  {
    if (!Allocated)
    {
      throw new Exception("Cannot write data. Buffer has not been allocated yet.");
    }

    ulong bufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();
    void* mappedMemory = MapRange(0, bufferSize);
    new Span<T>(mappedMemory, 1)[0] = data;
    Unmap();
  }

  public void WriteDataArray<T>(T[] data) where T : struct
  {
    if (!Allocated)
    {
      throw new Exception("Cannot write data. Buffer has not been allocated yet.");
    }

    if (data == null || data!.Length == 0)
    {
      return;
    }

    ulong bufferSize = (ulong)(Unsafe.SizeOf<T>() * data!.Length);
    void* mappedMemory = MapRange(0, bufferSize);

    data.AsSpan().CopyTo(new Span<T>(mappedMemory, data.Length));
    Unmap();
  }

  public void CopyData(GpuBuffer destination, ulong sizeBytes, CommandPool commandPool, Queue graphicsQueue)
  {
    CommandBufferAllocateInfo allocateInfo = new()
    {
      SType = StructureType.CommandBufferAllocateInfo,
      Level = CommandBufferLevel.Primary,
      CommandPool = commandPool,
      CommandBufferCount = 1,
    };

    vk!.AllocateCommandBuffers(device, allocateInfo, out CommandBuffer commandBuffer);

    CommandBufferBeginInfo beginInfo = new()
    {
      SType = StructureType.CommandBufferBeginInfo,
      Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
    };

    vk!.BeginCommandBuffer(commandBuffer, beginInfo);

    BufferCopy copyRegion = new()
    {
      Size = sizeBytes,
    };

    vk!.CmdCopyBuffer(commandBuffer, Buffer, destination.Buffer, 1, copyRegion);

    vk!.EndCommandBuffer(commandBuffer);

    SubmitInfo submitInfo = new()
    {
      SType = StructureType.SubmitInfo,
      CommandBufferCount = 1,
      PCommandBuffers = &commandBuffer,
    };

    vk!.QueueSubmit(graphicsQueue, 1, submitInfo, default);
    vk!.QueueWaitIdle(graphicsQueue);

    vk!.FreeCommandBuffers(device, commandPool, 1, commandBuffer);
  }

  private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
  {
    vk!.GetPhysicalDeviceMemoryProperties(physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

    for (int i = 0; i < memProperties.MemoryTypeCount; i++)
    {
      if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
      {
        return (uint)i;
      }
    }

    throw new Exception("Failed to find suitable memory.");
  }

  private void* MapRange(ulong offsetbytes, ulong sizeBytes)
  {
    if (!Allocated)
    {
      throw new Exception("Buffer memory has not been allocated.");
    }

    void* data;
    vk!.MapMemory(device, memory, offsetbytes, sizeBytes, 0, &data);
    return data;
  }

  private void Unmap()
  {
    vk!.UnmapMemory(device, memory);
  }

  private BufferUsageFlags MapBufferUsage(GpuBufferType gpuBufferType)
  {
    return gpuBufferType switch
    {
      GpuBufferType.Vertex => BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
      GpuBufferType.Index => BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
      GpuBufferType.Staging => BufferUsageFlags.TransferSrcBit,
      GpuBufferType.Uniform => BufferUsageFlags.UniformBufferBit,
      _ => throw new Exception($"Could not map to {nameof(BufferUsageFlags)}. Unsupported {nameof(GpuBufferType)}: {gpuBufferType}."),
    };
  }

  private MemoryPropertyFlags MapMemoryProperty(GpuBufferType gpuBufferType)
  {
    return gpuBufferType switch
    {
      GpuBufferType.Vertex => MemoryPropertyFlags.DeviceLocalBit,
      GpuBufferType.Index => MemoryPropertyFlags.DeviceLocalBit,
      GpuBufferType.Staging => MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
      GpuBufferType.Uniform => MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
      _ => throw new Exception($"Could not map to {nameof(MemoryPropertyFlags)}. Unsupported {nameof(GpuBufferType)}: {gpuBufferType}."),
    };
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (disposing)
    {
      vk!.DestroyBuffer(device, buffer, null);
      vk!.FreeMemory(device, memory, null);
    }
  }
}