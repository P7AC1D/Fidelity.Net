using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class TextureSampler(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Sampler sampler;
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

  public Sampler Sampler => sampler;

  public TextureSampler SetFilters(
    Filter magFilter = Filter.Linear,
    Filter minFilter = Filter.Linear)
  {
    this.magFilter = magFilter;
    this.minFilter = minFilter;
    return this;
  }

  public TextureSampler SetAddressMode(
    SamplerAddressMode addressModeU = SamplerAddressMode.Repeat,
    SamplerAddressMode addressModeV = SamplerAddressMode.Repeat,
    SamplerAddressMode addressModeW = SamplerAddressMode.Repeat)
  {
    this.addressModeU = addressModeU;
    this.addressModeV = addressModeV;
    this.addressModeW = addressModeW;
    return this;
  }

  public TextureSampler SetAnisotropy(
    bool enableAnisotropy = false)
  {
    this.enableAnisotropy = enableAnisotropy;
    return this;
  }

  public TextureSampler SetMipmapping(
    SamplerMipmapMode mipmapMode = SamplerMipmapMode.Linear,
    float mipLodBias = 0.0f,
    float minLod = 0.0f,
    float maxLod = 0.0f)
  {
    this.mipmapMode = mipmapMode;
    this.mipLodBias = mipLodBias;
    this.minLod = minLod;
    this.maxLod = maxLod;
    return this;
  }

  public TextureSampler Allocate()
  {
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

    fixed (Sampler* textureSamplerPtr = &sampler)
    {
      if (vk!.CreateSampler(device, samplerInfo, null, textureSamplerPtr) != Result.Success)
      {
        throw new Exception("Failed to create texture sampler.");
      }
    }
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