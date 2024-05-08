using Fidelity.Rendering.Enums;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Fidelity.Rendering.Resources;

public unsafe class GpuBuffer(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Buffer buffer;
  private DeviceMemory memory;

  public bool Allocated { get; private set; } = false;
  public ulong SizeBytes { get; private set; } = 0;
  public Buffer Buffer { get { return buffer; } }

  public void Allocate(BufferType gpuBufferType, ulong sizeBytes)
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
      MemoryTypeIndex = Utility.FindMemoryType(physicalDevice, memRequirements.MemoryTypeBits, MapMemoryProperty(gpuBufferType)),
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
    SizeBytes = sizeBytes;
  }

  public void WriteData<T>(T data) where T : struct
  {
    if (!Allocated)
    {
      throw new Exception("Cannot write data. Buffer has not been allocated yet.");
    }

    ulong bufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();
    if (SizeBytes < bufferSize)
    {
      throw new Exception($"Insufficient memory allocated. Allocated: {SizeBytes} bytes. Requested: {bufferSize} bytes.");
    }

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
    if (SizeBytes < bufferSize)
    {
      throw new Exception($"Insufficient memory allocated. Allocated: {SizeBytes} bytes. Requested: {bufferSize} bytes.");
    }

    void* mappedMemory = MapRange(0, bufferSize);

    data.AsSpan().CopyTo(new Span<T>(mappedMemory, data.Length));
    Unmap();
  }

  public void CopyBufferToImage(Buffer buffer, Image image, uint width, uint height, CommandPool commandPool, Queue graphicsQueue)
  {
    CommandBuffer commandBuffer = Utility.BeginSingleTimeCommands(commandPool, device);

    BufferImageCopy region = new()
    {
      BufferOffset = 0,
      BufferRowLength = 0,
      BufferImageHeight = 0,
      ImageSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
      ImageOffset = new Offset3D(0, 0, 0),
      ImageExtent = new Extent3D(width, height, 1),

    };

    vk!.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.TransferDstOptimal, 1, region);

    Utility.EndSingleTimeCommands(graphicsQueue, commandBuffer, device, commandPool);
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

    vk!.CmdCopyBuffer(commandBuffer, Buffer, destination!.Buffer, 1, copyRegion);

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

  private BufferUsageFlags MapBufferUsage(BufferType gpuBufferType)
  {
    return gpuBufferType switch
    {
      BufferType.Vertex => BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
      BufferType.Index => BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
      BufferType.Staging => BufferUsageFlags.TransferSrcBit,
      BufferType.Uniform => BufferUsageFlags.UniformBufferBit,
      _ => throw new Exception($"Could not map to {nameof(BufferUsageFlags)}. Unsupported {nameof(BufferType)}: {gpuBufferType}."),
    };
  }

  private MemoryPropertyFlags MapMemoryProperty(BufferType gpuBufferType)
  {
    return gpuBufferType switch
    {
      BufferType.Vertex => MemoryPropertyFlags.DeviceLocalBit,
      BufferType.Index => MemoryPropertyFlags.DeviceLocalBit,
      BufferType.Staging => MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
      BufferType.Uniform => MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
      _ => throw new Exception($"Could not map to {nameof(MemoryPropertyFlags)}. Unsupported {nameof(BufferType)}: {gpuBufferType}."),
    };
  }

  public void Deallocate()
  {
    Dispose();
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