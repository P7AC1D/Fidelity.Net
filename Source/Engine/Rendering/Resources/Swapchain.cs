using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Fidelity.Rendering.Resources;

public unsafe class Swapchain(
  Instance instance,
  Device device,
  PhysicalDevice physicalDevice,
  IWindow window,
  Surface surface)
{
  private Vk vk = Vk.GetApi();
  private KhrSwapchain khrSwapChain;
  private SwapchainKHR swapChain;
  private Queue presentQueue;
  private bool isInitialized = false;

  public uint Length => (uint)Images!.Length;
  public SwapchainKHR Get => swapChain;
  public Extent2D Extent { get; private set; }
  public Format Format { get; private set; }
  public uint Width => Extent.Width;
  public uint Height => Extent.Height;
  public Silk.NET.Vulkan.Image[] Images { get; private set; } = [];
  public ImageView[] ImageViews {get; private set; } = [];
  public uint ImageIndex { get; private set; }

  public Swapchain Create()
  {
    if (isInitialized)
    {
      throw new Exception("SwapChain has already been initialized.");
    }

    var swapChainSupport = surface.QuerySwapChainSupport(physicalDevice);
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
      Surface = surface.Get,

      MinImageCount = imageCount,
      ImageFormat = surfaceFormat.Format,
      ImageColorSpace = surfaceFormat.ColorSpace,
      ImageExtent = extent,
      ImageArrayLayers = 1,
      ImageUsage = ImageUsageFlags.ColorAttachmentBit
    };

    var indices = surface.FindQueueFamilies(physicalDevice);
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
        Images = new Silk.NET.Vulkan.Image[imageCount];
    fixed (Silk.NET.Vulkan.Image* swapChainImagesPtr = Images)
    {
            khrSwapChain.GetSwapchainImages(device, swapChain, ref imageCount, swapChainImagesPtr);
    }

    Format = surfaceFormat.Format;
    Extent = extent;

    ImageViews = new ImageView[Length];

    for (int i = 0; i < Length; i++)
    {
      ImageViews[i] = new ImageView(device)
        .SetImage(Images![i], Format)
        .SetRange(ImageAspectFlags.ColorBit)
        .Allocate();
    }

    vk!.GetDeviceQueue(device, indices.PresentFamily!.Value, 0, out presentQueue);

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
    var indices = surface.FindQueueFamilies(physicalDevice);
    return indices.GraphicsFamily!.Value;
  }

  public void DestroySwapchain()
  {
    if (!isInitialized)
    {
      throw new Exception("SwapChain has not been initialized.");
    }

    khrSwapChain!.DestroySwapchain(device, swapChain, null);

    isInitialized = false;
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