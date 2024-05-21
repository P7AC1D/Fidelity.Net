using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class Sampler(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Silk.NET.Vulkan.Sampler sampler;
  private Filter magFilter = Filter.Linear;
  private Filter minFilter = Filter.Linear;
  private SamplerAddressMode addressModeU = SamplerAddressMode.Repeat;
  private SamplerAddressMode addressModeV = SamplerAddressMode.Repeat;
  private SamplerAddressMode addressModeW = SamplerAddressMode.Repeat;
  private bool enableAnisotropy = true;
  private SamplerMipmapMode mipmapMode = SamplerMipmapMode.Linear;
  private float mipLodBias = 0;
  private float minLod = 0;
  private float maxLod = 1;
  private bool isInitialized = false;

  public Silk.NET.Vulkan.Sampler Get => sampler;

  public Sampler SetFilters(
    Filter magFilter = Filter.Linear,
    Filter minFilter = Filter.Linear)
  {
    if (isInitialized)
    {
      throw new Exception("Sampler has already been initialized.");
    }

    this.magFilter = magFilter;
    this.minFilter = minFilter;
    return this;
  }

  public Sampler SetAddressMode(
    SamplerAddressMode addressModeU = SamplerAddressMode.Repeat,
    SamplerAddressMode addressModeV = SamplerAddressMode.Repeat,
    SamplerAddressMode addressModeW = SamplerAddressMode.Repeat)
  {
    if (isInitialized)
    {
      throw new Exception("Sampler has already been initialized.");
    }

    this.addressModeU = addressModeU;
    this.addressModeV = addressModeV;
    this.addressModeW = addressModeW;
    return this;
  }

  public Sampler SetAnisotropy(
    bool enableAnisotropy = false)
  {
    if (isInitialized)
    {
      throw new Exception("Sampler has already been initialized.");
    }

    this.enableAnisotropy = enableAnisotropy;
    return this;
  }

  public Sampler SetMipmapping(
    SamplerMipmapMode mipmapMode = SamplerMipmapMode.Linear,
    float mipLodBias = 0.0f,
    float minLod = 0.0f,
    float maxLod = 0.0f)
  {
    if (isInitialized)
    {
      throw new Exception("Sampler has already been initialized.");
    }

    this.mipmapMode = mipmapMode;
    this.mipLodBias = mipLodBias;
    this.minLod = minLod;
    this.maxLod = maxLod;
    return this;
  }

  public Sampler Allocate()
  {
    if (isInitialized)
    {
      throw new Exception("Sampler has already been initialized.");
    }

    vk!.GetPhysicalDeviceProperties(physicalDevice, out PhysicalDeviceProperties properties);

    SamplerCreateInfo samplerInfo = new()
    {
      SType = StructureType.SamplerCreateInfo,
      MagFilter = magFilter,
      MinFilter = minFilter,
      AddressModeU = addressModeU,
      AddressModeV = addressModeV,
      AddressModeW = addressModeW,
      AnisotropyEnable = enableAnisotropy,
      MaxAnisotropy = properties.Limits.MaxSamplerAnisotropy,
      BorderColor = BorderColor.IntOpaqueBlack,
      UnnormalizedCoordinates = false,
      CompareEnable = false,
      CompareOp = CompareOp.Always,
      MipmapMode = mipmapMode,
      MinLod = minLod,
      MaxLod = maxLod,
      MipLodBias = mipLodBias,
    };

    fixed (Silk.NET.Vulkan.Sampler* textureSamplerPtr = &sampler)
    {
      if (vk!.CreateSampler(device, samplerInfo, null, textureSamplerPtr) != Result.Success)
      {
        throw new Exception("Failed to create texture sampler.");
      }
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
      vk!.DestroySampler(device, sampler, null);
    }
  }
}