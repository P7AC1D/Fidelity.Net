using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class DescriptorSets(Device device, PhysicalDevice physicalDevice, DescriptorPool descriptorPool) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private DescriptorSetLayout descriptorSetLayout;
  private DescriptorSet[] descriptorSets;
  private IList<DescriptorSetLayoutBinding> descriptorSetLayoutBindings;

  public DescriptorSetLayout Layout => descriptorSetLayout;

  public DescriptorSets AddDescriptorSetLayout(DescriptorType type, uint binding, ShaderStageFlags stageFlags)
  {
    descriptorSetLayoutBindings.Add(new DescriptorSetLayoutBinding
    {
      Binding = binding,
      DescriptorType = type,
      DescriptorCount = 1,
      StageFlags = stageFlags,
    });
    return this;
  }

  public DescriptorSets Allocate(uint descriptorSetCount)
  {
    fixed (DescriptorSetLayoutBinding* bindingsPtr = descriptorSetLayoutBindings.ToArray())
    fixed (DescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
    {
      DescriptorSetLayoutCreateInfo layoutInfo = new()
      {
        SType = StructureType.DescriptorSetLayoutCreateInfo,
        BindingCount = (uint)descriptorSetLayoutBindings.Count,
        PBindings = bindingsPtr,
      };

      if (vk!.CreateDescriptorSetLayout(device, layoutInfo, null, descriptorSetLayoutPtr) != Result.Success)
      {
        throw new Exception("Failed to create descriptor set layout.");
      }
    }

    var layouts = new DescriptorSetLayout[descriptorSetCount];
    Array.Fill(layouts, descriptorSetLayout);

    fixed (DescriptorSetLayout* layoutsPtr = layouts)
    {
      DescriptorSetAllocateInfo allocateInfo = new()
      {
        SType = StructureType.DescriptorSetAllocateInfo,
        DescriptorPool = descriptorPool,
        DescriptorSetCount = descriptorSetCount,
        PSetLayouts = layoutsPtr,
      };

      descriptorSets = new DescriptorSet[descriptorSetCount];
      fixed (DescriptorSet* descriptorSetsPtr = descriptorSets)
      {
        if (vk!.AllocateDescriptorSets(device, allocateInfo, descriptorSetsPtr) != Result.Success)
        {
          throw new Exception("Failed to allocate descriptor sets.");
        }
      }
    }

    return this;
  }

  public DescriptorSets Update()
  {
    foreach (var descriptorSet in descriptorSets)
    {
      
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
    }
  }
}