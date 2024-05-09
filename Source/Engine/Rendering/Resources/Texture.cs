using Fidelity.Rendering.Extensions;
using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class Texture(Device device, PhysicalDevice physicalDevice)
{
  private readonly Vk vk = Vk.GetApi();
  private Image image;
  private DeviceMemory memory;

  public void Allocate(
    Extent3D extent,
    uint mipLevels,
    SampleCountFlags numSamples,
    Format format,
    ImageTiling tiling,
    ImageUsageFlags usage,
    MemoryPropertyFlags properties)
  {
    ImageCreateInfo imageInfo = new()
    {
      SType = StructureType.ImageCreateInfo,
      ImageType = ImageType.Type2D,
      Extent = extent,
      MipLevels = mipLevels,
      ArrayLayers = 1,
      Format = format,
      Tiling = tiling,
      InitialLayout = ImageLayout.Undefined,
      Usage = usage,
      Samples = numSamples,
      SharingMode = SharingMode.Exclusive,
    };

    fixed (Image* imagePtr = &image)
    {
      if (vk!.CreateImage(device, imageInfo, null, imagePtr) != Result.Success)
      {
        throw new Exception("Failed to create image.");
      }
    }

    vk!.GetImageMemoryRequirements(device, image, out MemoryRequirements memRequirements);

    MemoryAllocateInfo allocInfo = new()
    {
      SType = StructureType.MemoryAllocateInfo,
      AllocationSize = memRequirements.Size,
      MemoryTypeIndex = physicalDevice.FindMemoryType(memRequirements.MemoryTypeBits, properties),
    };

    fixed (DeviceMemory* imageMemoryPtr = &memory)
    {
      if (vk!.AllocateMemory(device, allocInfo, null, imageMemoryPtr) != Result.Success)
      {
        throw new Exception("Failed to allocate image memory.");
      }
    }

    vk!.BindImageMemory(device, image, memory, 0);
  }
}