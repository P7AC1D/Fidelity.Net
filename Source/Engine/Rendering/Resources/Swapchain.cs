using Fidelity.Rendering.Extensions;
using Fidelity.Rendering.Models;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Fidelity.Rendering.Resources;

public unsafe class Swapchain(
  Instance instance, 
  Device device, 
  PhysicalDevice physicalDevice, 
  IWindow window, 
  Queue presentQueue, 
  SurfaceKHR surface, 
  KhrSurface khrSurface)
{
  private Vk vk = Vk.GetApi();
  private KhrSwapchain khrSwapChain;
  private SwapchainKHR swapChain;
  private Image[] swapChainImages = [];
  private Format swapChainImageFormat;
  private Extent2D swapChainExtent;
  private bool isInitialized = false;

  public uint Length => (uint)swapChainImages!.Length;
  public SwapchainKHR Get => swapChain;
  public Extent2D Extent => swapChainExtent;
  public Format Format => swapChainImageFormat;
  public uint Width => swapChainExtent.Width;
  public uint Height => swapChainExtent.Height;
  public Image[] Images => swapChainImages;

  public Swapchain Create()
  {
    if (isInitialized)
    {
      throw new Exception("SwapChain has already been initialized.");
    }

    var swapChainSupport = physicalDevice.QuerySwapChainSupport(khrSurface, surface);
    var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    var presentMode = ChoosePresentMode(swapChainSupport.PresentModes);
    var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

    var imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
    if (swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.Capabilities.MaxImageCount)
    {
      imageCount = swapChainSupport.Capabilities.MaxImageCount;
    }

    SwapchainCreateInfoKHR createInfo = new()
    {
      SType = StructureType.SwapchainCreateInfoKhr,
      Surface = surface,

      MinImageCount = imageCount,
      ImageFormat = surfaceFormat.Format,
      ImageColorSpace = surfaceFormat.ColorSpace,
      ImageExtent = extent,
      ImageArrayLayers = 1,
      ImageUsage = ImageUsageFlags.ColorAttachmentBit,
    };

    var indices = physicalDevice.FindQueueFamilies(khrSurface, surface);
    var queueFamilyIndices = stackalloc[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

    if (indices.GraphicsFamily != indices.PresentFamily)
    {
      createInfo = createInfo with
      {
        ImageSharingMode = SharingMode.Concurrent,
        QueueFamilyIndexCount = 2,
        PQueueFamilyIndices = queueFamilyIndices,
      };
    }
    else
    {
      createInfo.ImageSharingMode = SharingMode.Exclusive;
    }

    createInfo = createInfo with
    {
      PreTransform = swapChainSupport.Capabilities.CurrentTransform,
      CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
      PresentMode = presentMode,
      Clipped = true,

      OldSwapchain = default
    };

    if (!vk!.TryGetDeviceExtension(instance, device, out khrSwapChain))
    {
      throw new NotSupportedException("VK_KHR_swapchain extension not found.");
    }

    if (khrSwapChain!.CreateSwapchain(device, createInfo, null, out swapChain) != Result.Success)
    {
      throw new Exception("Failed to create swap chain.");
    }

    khrSwapChain.GetSwapchainImages(device, swapChain, ref imageCount, null);
    swapChainImages = new Image[imageCount];
    fixed (Image* swapChainImagesPtr = swapChainImages)
    {
      khrSwapChain.GetSwapchainImages(device, swapChain, ref imageCount, swapChainImagesPtr);
    }

    swapChainImageFormat = surfaceFormat.Format;
    swapChainExtent = extent;
    isInitialized = true;
    return this;
  }

  public bool GetNextImageIndex(Semaphore semaphore, out uint index)
  {
    uint imageIndex = 0;
    var result = khrSwapChain!.AcquireNextImage(device, swapChain, ulong.MaxValue, semaphore!.Get, default, ref imageIndex);
    index = imageIndex;

    if (result == Result.ErrorOutOfDateKhr)
    {
      return false;
    }
    else if (result != Result.Success && result != Result.SuboptimalKhr)
    {
      throw new Exception("Failed to acquire swap chain image.");
    }
    return true;
  }

  public bool Present(Semaphore semaphore, uint imageIndex)
  {
    var signalSemaphores = stackalloc[] { semaphore!.Get };
    var swapChains = stackalloc[] { swapChain };
    PresentInfoKHR presentInfo = new()
    {
      SType = StructureType.PresentInfoKhr,

      WaitSemaphoreCount = 1,
      PWaitSemaphores = signalSemaphores,

      SwapchainCount = 1,
      PSwapchains = swapChains,

      PImageIndices = &imageIndex
    };

    Result result = khrSwapChain.QueuePresent(presentQueue, presentInfo);
    if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
    {
      return false;
    }
    else if (result != Result.Success)
    {
      throw new Exception("failed to present swap chain image!");
    }
    return true;
  }

  public uint FindGraphicsQueueFamilyIndex()
  {
    var indices = physicalDevice.FindQueueFamilies(khrSurface, surface);
    return indices.GraphicsFamily!.Value;
  }

  public void DestroySwapchain()
  {
    if (!isInitialized)
    {
      throw new Exception("SwapChain has not been initialized.");
    }

    khrSwapChain!.DestroySwapchain(device, swapChain, null);
  }

  private SurfaceFormatKHR ChooseSwapSurfaceFormat(IReadOnlyList<SurfaceFormatKHR> availableFormats)
  {
    foreach (var availableFormat in availableFormats)
    {
      if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
      {
        return availableFormat;
      }
    }

    return availableFormats[0];
  }

  private PresentModeKHR ChoosePresentMode(IReadOnlyList<PresentModeKHR> availablePresentModes)
  {
    foreach (var availablePresentMode in availablePresentModes)
    {
      if (availablePresentMode == PresentModeKHR.MailboxKhr)
      {
        return availablePresentMode;
      }
    }

    return PresentModeKHR.FifoKhr;
  }

  private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
  {
    if (capabilities.CurrentExtent.Width != uint.MaxValue)
    {
      return capabilities.CurrentExtent;
    }
    else
    {
      var framebufferSize = window!.FramebufferSize;

      Extent2D actualExtent = new()
      {
        Width = (uint)framebufferSize.X,
        Height = (uint)framebufferSize.Y
      };

      actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
      actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

      return actualExtent;
    }
  }
}