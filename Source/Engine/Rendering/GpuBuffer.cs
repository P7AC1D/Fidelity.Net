using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Fidelity;

public unsafe class GpuBuffer(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Buffer buffer;
  private DeviceMemory memory;

  public bool Allocated { get; private set; } = false;

  public void Allocate(BufferUsageFlags usage, MemoryPropertyFlags properties, ulong sizeBytes)
  {
    if (Allocated)
    {
      throw new Exception("Buffer has already been allocated.");
    }

    BufferCreateInfo bufferInfo = new()
    {
      SType = StructureType.BufferCreateInfo,
      Size = sizeBytes,
      Usage = usage,
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
      MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties),
    };

    fixed (DeviceMemory* bufferMemoryPtr = &memory)
    {
      if (vk!.AllocateMemory(device, allocateInfo, null, bufferMemoryPtr) != Result.Success)
      {
        throw new Exception("Failed to allocate buffer memory.");
      }
    }

    Allocated = true;
  }

  public void WriteData<T>(T[] data) where T : struct
  {
    if (!Allocated)
    {
      throw new Exception("Cannot write data. Buffer has not been allocated yet.");
    }

    if (data == null || data!.Length == 0)
    {
      return;
    }

    ulong bufferSize = (ulong)(Unsafe.SizeOf<Vertex>() * data!.Length);
    void* mappedMemory = MapRange(0, bufferSize);

    data.AsSpan().CopyTo(new Span<T>(mappedMemory, data.Length));
    Unmap();
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