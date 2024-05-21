using System.Runtime.InteropServices;
using Fidelity.Rendering.Models;
using Fidelity.Rendering.Resources;
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

  public static bool IsDeviceSuitable(this PhysicalDevice device, Surface surface, string[] deviceExtensions)
  {
    Vk vk = Vk.GetApi();
    var indices = surface.FindQueueFamilies(device);

    bool extensionsSupported = CheckDeviceExtensionsSupport(device, deviceExtensions);

    bool swapChainAdequate = false;
    if (extensionsSupported)
    {
      var swapChainSupport = surface.QuerySwapChainSupport(device);
      swapChainAdequate = swapChainSupport.Formats.Any() && swapChainSupport.PresentModes.Any();
    }

    vk!.GetPhysicalDeviceFeatures(device, out PhysicalDeviceFeatures supportedFeatures);

    return indices.IsComplete() && extensionsSupported && swapChainAdequate && supportedFeatures.SamplerAnisotropy;
  }

  public static bool CheckDeviceExtensionsSupport(this PhysicalDevice device, string[] deviceExtensions)
  {
    Vk vk = Vk.GetApi();
    uint extentionsCount = 0;
    vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extentionsCount, null);

    var availableExtensions = new ExtensionProperties[extentionsCount];
    fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
    {
      vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extentionsCount, availableExtensionsPtr);
    }

    var availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)).ToHashSet();
    return deviceExtensions.All(availableExtensionNames.Contains);
  }
}