using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class DescriptorSetLayout(Device device) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private readonly IList<DescriptorSetLayoutBinding> descriptorSetLayoutBindings = [];
  private Silk.NET.Vulkan.DescriptorSetLayout descriptorSetLayout;
  private bool isInitialized = false;

  public Silk.NET.Vulkan.DescriptorSetLayout Layout => descriptorSetLayout;

  public DescriptorSetLayout AddBinding(uint binding, DescriptorType descriptorType, uint count = 1, ShaderStageFlags stageFlags = ShaderStageFlags.AllGraphics)
  {
    if (isInitialized)
    {
      throw new Exception("DescriptorSetLayout has already been initialized.");
    }

    descriptorSetLayoutBindings.Add(new()
    {
      Binding = binding,
      DescriptorCount = count,
      DescriptorType = descriptorType,
      StageFlags = stageFlags,
    });
    return this;
  }

  public DescriptorSetLayout Allocate()
  {
    if (isInitialized)
    {
      throw new Exception("DescriptorSetLayout has already been initialized.");
    }

    fixed (DescriptorSetLayoutBinding* bindingsPtr = descriptorSetLayoutBindings.ToArray())
    fixed (Silk.NET.Vulkan.DescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
    {
      DescriptorSetLayoutCreateInfo layoutInfo = new()
      {
        SType = StructureType.DescriptorSetLayoutCreateInfo,
        BindingCount = (uint)descriptorSetLayoutBindings.Count,
        PBindings = bindingsPtr,
      };

      if (vk!.CreateDescriptorSetLayout(device, layoutInfo, null, descriptorSetLayoutPtr) != Result.Success)
      {
        throw new Exception("Failed to create DescriptorSetLayout.");
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
      vk!.DestroyDescriptorSetLayout(device, descriptorSetLayout, null);
    }
  }
}