using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

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

  public static QueueFamilyIndices FindQueueFamilies(this PhysicalDevice device, KhrSurface? khrSurface, SurfaceKHR surface)
  {
    Vk vk = Vk.GetApi();
    var indices = new QueueFamilyIndices();

    uint queueFamilityCount = 0;
    vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, null);

    var queueFamilies = new QueueFamilyProperties[queueFamilityCount];
    fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
    {
      vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, queueFamiliesPtr);
    }


    uint i = 0;
    foreach (var queueFamily in queueFamilies)
    {
      if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
      {
        indices.GraphicsFamily = i;
      }

      khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, surface, out var presentSupport);

      if (presentSupport)
      {
        indices.PresentFamily = i;
      }

      if (indices.IsComplete())
      {
        break;
      }

      i++;
    }

    return indices;
  }
}