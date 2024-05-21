using System.Runtime.InteropServices;
using Fidelity.Rendering.Models;
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

  public static bool IsDeviceSuitable(this PhysicalDevice device, KhrSurface? khrSurface, SurfaceKHR surface, string[] deviceExtensions)
  {
    Vk vk = Vk.GetApi();
    var indices = FindQueueFamilies(device, khrSurface, surface);

    bool extensionsSupported = CheckDeviceExtensionsSupport(device, deviceExtensions);

    bool swapChainAdequate = false;
    if (extensionsSupported)
    {
      var swapChainSupport = QuerySwapChainSupport(device, khrSurface, surface);
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

  public static SwapChainSupportDetails QuerySwapChainSupport(this PhysicalDevice physicalDevice, KhrSurface? khrSurface, SurfaceKHR surface)
  {
    var details = new SwapChainSupportDetails();

    khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, out details.Capabilities);

    uint formatCount = 0;
    khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, null);

    if (formatCount != 0)
    {
      details.Formats = new SurfaceFormatKHR[formatCount];
      fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
      {
        khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, formatsPtr);
      }
    }
    else
    {
      details.Formats = [];
    }

    uint presentModeCount = 0;
    khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, null);

    if (presentModeCount != 0)
    {
      details.PresentModes = new PresentModeKHR[presentModeCount];
      fixed (PresentModeKHR* formatsPtr = details.PresentModes)
      {
        khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, formatsPtr);
      }
    }
    else
    {
      details.PresentModes = [];
    }

    return details;
  }
}