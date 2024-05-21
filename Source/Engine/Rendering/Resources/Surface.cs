using Fidelity.Rendering.Extensions;
using Fidelity.Rendering.Models;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Fidelity.Rendering.Resources;

public unsafe class Surface(Instance instance, IWindow window) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private SurfaceKHR surface;
  private KhrSurface khrSurface;
  private bool isInitialized = false;

  public SurfaceKHR Get => surface;
  public KhrSurface KhrGet => khrSurface;

  public Surface Create()
  {
    if (isInitialized)
    {
      throw new Exception("Surface has already been initialized.");
    }

    if (!vk!.TryGetInstanceExtension(instance, out khrSurface))
    {
      throw new NotSupportedException("KHR_surface extension not found.");
    }

    surface = window!.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();

    isInitialized = true;
    return this;
  }

  public bool DoesSurfaceSupportPresent(PhysicalDevice physicalDevice, uint queueFamilyIndex)
  {
    if (!isInitialized)
    {
      throw new Exception("Surface has not be initialized.");
    }

    khrSurface!.GetPhysicalDeviceSurfaceSupport(physicalDevice, queueFamilyIndex, surface, out var presentSupport);
    return presentSupport;
  }

  public SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice physicalDevice)
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

  public QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
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

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (disposing)
    {
      khrSurface!.DestroySurface(instance, surface, null);
    }
  }
}
