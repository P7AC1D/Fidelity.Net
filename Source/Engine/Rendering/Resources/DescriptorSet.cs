using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class DescriptorSet(Device device, PhysicalDevice physicalDevice, DescriptorPool descriptorPool) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private IList<WriteDescriptorSet> writeDescriptorSets = new List<WriteDescriptorSet>();
  private IList<DescriptorSetLayoutBinding> descriptorSetLayoutBindings = new List<DescriptorSetLayoutBinding>();
  private Silk.NET.Vulkan.DescriptorSet descriptorSet;
  private DescriptorSetLayout descriptorSetLayout;
  private bool isInitialized = false;

  public Silk.NET.Vulkan.DescriptorSet Set => descriptorSet;
  public DescriptorSetLayout Layout => descriptorSetLayout;

  public DescriptorSet AddUniformBuffer(GpuBuffer gpuBuffer, uint binding)
  {
    DescriptorBufferInfo bufferInfo = new()
    {
      Buffer = gpuBuffer!.Buffer,
      Offset = 0,
      Range = gpuBuffer.SizeBytes,
    };

    writeDescriptorSets.Add(new WriteDescriptorSet
    {
      SType = StructureType.WriteDescriptorSet,
      DstBinding = binding,
      DstArrayElement = 0,
      DescriptorType = DescriptorType.UniformBuffer,
      DescriptorCount = 1,
      PBufferInfo = &bufferInfo,
    });

    descriptorSetLayoutBindings.Add(new DescriptorSetLayoutBinding
    {
      Binding = binding,
      DescriptorType = DescriptorType.UniformBuffer,
      DescriptorCount = 1,
      StageFlags = ShaderStageFlags.AllGraphics,
    });
    return this;
  }

  public DescriptorSet AddTexureSampler(Texture texture, TextureSampler textureSampler, uint binding)
  {
    DescriptorImageInfo imageInfo = new()
    {
      ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
      ImageView = texture.ImageView,
      Sampler = textureSampler.Sampler,
    };

    writeDescriptorSets.Add(new WriteDescriptorSet
    {
      SType = StructureType.WriteDescriptorSet,
      DstBinding = binding,
      DstArrayElement = 0,
      DescriptorType = DescriptorType.CombinedImageSampler,
      DescriptorCount = 1,
      PImageInfo = &imageInfo,
    });

    descriptorSetLayoutBindings.Add(new DescriptorSetLayoutBinding
    {
      Binding = binding,
      DescriptorType = DescriptorType.CombinedImageSampler,
      DescriptorCount = 1,
      StageFlags = ShaderStageFlags.AllGraphics,
    });
    return this;
  }

  public DescriptorSet Allocate()
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

    fixed (DescriptorSetLayout* layoutsPtr = &descriptorSetLayout)
    {
      DescriptorSetAllocateInfo allocateInfo = new()
      {
        SType = StructureType.DescriptorSetAllocateInfo,
        DescriptorPool = descriptorPool,
        DescriptorSetCount = 1,
        PSetLayouts = layoutsPtr,
      };

      fixed (Silk.NET.Vulkan.DescriptorSet* descriptorSetsPtr = &descriptorSet)
      {
        if (vk!.AllocateDescriptorSets(device, allocateInfo, descriptorSetsPtr) != Result.Success)
        {
          throw new Exception("Failed to allocate descriptor set.");
        }
      }
    }

    for (int i = 0; i < writeDescriptorSets.Count; i++)
    {
      WriteDescriptorSet writeDescriptorSet = writeDescriptorSets[i];
      writeDescriptorSets[i] = writeDescriptorSet;
    }

    isInitialized = true;
    return this;
  }

  public DescriptorSet Update()
  {
    if (!isInitialized)
    {
      throw new Exception("DescriptorSet must be allocated before updating.");
    }

    fixed (WriteDescriptorSet* descriptorWritesPtr = writeDescriptorSets.ToArray())
    {
      vk!.UpdateDescriptorSets(device, (uint)writeDescriptorSets.Count, descriptorWritesPtr, 0, null);
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
      vk!.DestroyDescriptorSetLayout(device, descriptorSetLayout, null);
    }
  }
}