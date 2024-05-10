using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class DescriptorSet(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private DescriptorPool descriptorPool;
  private DescriptorSetLayout descriptorSetLayout;
  private Silk.NET.Vulkan.DescriptorSet descriptorSet;

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