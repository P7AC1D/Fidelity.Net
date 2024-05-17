using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class ImageView(Device device) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Silk.NET.Vulkan.ImageView imageView;
  private Silk.NET.Vulkan.Image image;
  private Format format;
  private ImageSubresourceRange subresourceRange;
  private bool isInitialized = false;

  public Silk.NET.Vulkan.ImageView View => imageView;

  public ImageView SetImage(Image image, Format format)
  {
    if (isInitialized)
    {
      throw new Exception("ImageView has already been initialized.");
    }

    this.format = format;
    this.image = image;
    return this;
  }

  public ImageView SetRange(ImageAspectFlags imageAspectFlags, uint baseMip = 0, uint levelCount = 1, uint baseLayer = 0, uint layerCount = 1)
  {
    if (isInitialized)
    {
      throw new Exception("ImageView has already been initialized.");
    }

    subresourceRange = new()
    {
      AspectMask = imageAspectFlags,
      BaseMipLevel = baseMip,
      LevelCount = levelCount,
      BaseArrayLayer = baseLayer,
      LayerCount = layerCount,
    };
    return this;
  }

  public ImageView Allocate()
  {
    if (isInitialized)
    {
      throw new Exception("ImageView has already been initialized.");
    }

    ImageViewCreateInfo createInfo = new()
    {
      SType = StructureType.ImageViewCreateInfo,
      Image = image,
      ViewType = ImageViewType.Type2D,
      Format = format,
      SubresourceRange = subresourceRange
    };

    if (vk!.CreateImageView(device, createInfo, null, out imageView) != Result.Success)
    {
      throw new Exception("Failed to create image view.");
    }

    isInitialized = true;
    return this;
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
      vk!.DestroyImageView(device, imageView, null);
    }
  }
}