using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Extensions;

public static class PhysicalDeviceExtensions
{
  public static uint FindMemoryType(this PhysicalDevice physicalDevice, uint typeFilter, MemoryPropertyFlags properties)
  {
    Vk vk = Vk.GetApi();
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
}