using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Extensions;

public unsafe static class PhysicalDeviceExtensions
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

  public static Format FindSupportedFormat(this PhysicalDevice physicalDevice, IEnumerable<Format> candidates, ImageTiling tiling, FormatFeatureFlags features)
  {
    Vk vk = Vk.GetApi();
    foreach (var format in candidates)
    {
      vk!.GetPhysicalDeviceFormatProperties(physicalDevice, format, out var props);

      if (tiling == ImageTiling.Linear && (props.LinearTilingFeatures & features) == features)
      {
        return format;
      }
      else if (tiling == ImageTiling.Optimal && (props.OptimalTilingFeatures & features) == features)
      {
        return format;
      }
    }
    throw new Exception("Failed to find supported format!");
  }
}