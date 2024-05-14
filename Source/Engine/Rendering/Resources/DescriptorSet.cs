

using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class DescriptorSet(Device device, PhysicalDevice physicalDevice)
{
  private readonly Vk vk = Vk.GetApi();
  private IList<WriteDescriptorSet> writeDescriptorSets;

  public DescriptorSet BindBuffer(GpuBuffer gpuBuffer, uint binding)
  {
    DescriptorBufferInfo bufferInfo = new()
    {
      Buffer = gpuBuffer!.Buffer,
      Offset = 0,
      Range = gpuBuffer.SizeBytes,

    };

    writeDescriptorSets.Add(new WriteDescriptorSet
    {

    });
    return this;
  }
}