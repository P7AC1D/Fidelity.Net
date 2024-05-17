using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class GraphicsQueue(Device device, Queue graphicsQueue)
{
  private readonly Vk vk = Vk.GetApi();

}