using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class DescriptorSet(Device device, DescriptorPool descriptorPool)
{
  private readonly Vk vk = Vk.GetApi();
  private readonly IList<(GpuBuffer Buffer, uint Binding)> uniformBuffers = [];
  private readonly IList<(Texture Texture, TextureSampler TextureSampler, uint Binding)> textureSamplers = [];
  private Silk.NET.Vulkan.DescriptorSet descriptorSet;
  private DescriptorSetLayout descriptorSetLayout;
  private bool isInitialized = false;
  private bool layoutAssigned = false;

  public Silk.NET.Vulkan.DescriptorSet Set => descriptorSet;

  public DescriptorSet BindUniformBuffer(GpuBuffer gpuBuffer, uint binding)
  {
    if (isInitialized)
    {
      throw new Exception("DescriptorSet has already been allocated.");
    }

    uniformBuffers.Add((gpuBuffer, binding));
    return this;
  }

  public DescriptorSet BindTexureSampler(Texture texture, TextureSampler textureSampler, uint binding)
  {
    if (isInitialized)
    {
      throw new Exception("DescriptorSet has already been allocated.");
    }

    textureSamplers.Add((texture, textureSampler, binding));
    return this;
  }

  public DescriptorSet SetLayout(DescriptorSetLayout layout)
  {
    if (isInitialized)
    {
      throw new Exception("DescriptorSet has already been allocated.");
    }

    descriptorSetLayout = layout;
    layoutAssigned = true;
    return this;
  }

  public DescriptorSet Allocate()
  {
    if (isInitialized)
    {
      throw new Exception("DescriptorSet has already been allocated.");
    }

    if (!layoutAssigned)
    {
      throw new Exception("DescriptorSetLayout must be assigned before allocating DescriptorSet.");
    }

    var layout = descriptorSetLayout!.Layout;
    DescriptorSetAllocateInfo allocateInfo = new()
    {
      SType = StructureType.DescriptorSetAllocateInfo,
      DescriptorPool = descriptorPool,
      DescriptorSetCount = 1,
      PSetLayouts = &layout,
    };

    fixed (Silk.NET.Vulkan.DescriptorSet* descriptorSetsPtr = &descriptorSet)
    {
      if (vk!.AllocateDescriptorSets(device, allocateInfo, descriptorSetsPtr) != Result.Success)
      {
        throw new Exception("Failed to allocate DescriptorSet.");
      }
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

    WriteDescriptorSet[] writeDescriptorSets = new WriteDescriptorSet[uniformBuffers.Count + textureSamplers.Count];

    for (int i = 0; i < uniformBuffers.Count; i++)
    {
      DescriptorBufferInfo bufferInfo = new()
      {
        Buffer = uniformBuffers[i].Buffer!.Buffer,
        Offset = 0,
        Range = uniformBuffers[i]!.Buffer!.SizeBytes,
      };

      writeDescriptorSets[i] = new WriteDescriptorSet
      {
        SType = StructureType.WriteDescriptorSet,
        DstBinding = uniformBuffers[i]!.Binding,
        DstArrayElement = 0,
        DstSet = descriptorSet,
        DescriptorType = DescriptorType.UniformBuffer,
        DescriptorCount = 1,
        PBufferInfo = &bufferInfo,
      };
    }

    for (int i = 0; i < textureSamplers.Count; i++)
    {
      DescriptorImageInfo imageInfo = new()
      {
        Sampler = textureSamplers[i].TextureSampler!.Sampler,
        ImageView = textureSamplers[i].Texture!.ImageView!.View,
        ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
      };

      writeDescriptorSets[i + uniformBuffers.Count] = new WriteDescriptorSet
      {
        SType = StructureType.WriteDescriptorSet,
        DstBinding = textureSamplers[i].Binding,
        DstArrayElement = 0,
        DstSet = descriptorSet,
        DescriptorType = DescriptorType.CombinedImageSampler,
        DescriptorCount = 1,
        PImageInfo = &imageInfo,
      };
    }

    fixed (WriteDescriptorSet* descriptorWritesPtr = writeDescriptorSets.ToArray())
    {
      vk!.UpdateDescriptorSets(device, (uint)writeDescriptorSets.Length, descriptorWritesPtr, 0, null);
    }

    return this;
  }
}