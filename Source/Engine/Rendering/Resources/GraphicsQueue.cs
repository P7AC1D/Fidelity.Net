using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class GraphicsQueue(Device device)
{
  private readonly Vk vk = Vk.GetApi();
  private Queue queue;
  private bool isInitialized = false;

  public Queue Queue => queue;

  public GraphicsQueue Retrieve(uint queueFamilyIndex, uint queueIndex = 0)
  {
    vk!.GetDeviceQueue(device, queueFamilyIndex, queueIndex, out queue);
    isInitialized = true;
    return this;
  }

  public void SubmitAndWait(CommandBuffer commandBuffer)
  {
    if (!isInitialized)
    {
      throw new Exception("GraphicsQueue has not been allocated.");
    }

    var cmdBuf = commandBuffer.Buffer;
    SubmitInfo submitInfo = new()
    {
      SType = StructureType.SubmitInfo,
      CommandBufferCount = 1,
      PCommandBuffers = &cmdBuf,
    };

    vk!.QueueSubmit(queue, 1, submitInfo, default);
    vk!.QueueWaitIdle(queue);
  }

  public void Submit(CommandBuffer commandBuffer, Semaphore waitSemaphore, Semaphore signalSemaphore, Fence fence, PipelineStageFlags pipelineStageFlags)
  {
    if (!isInitialized)
    {
      throw new Exception("GraphicsQueue has not been allocated.");
    }

    var waitSemaphores = stackalloc[] { waitSemaphore.Get };
    var signalSemaphores = stackalloc[] { signalSemaphore.Get };
    Silk.NET.Vulkan.CommandBuffer buffer = commandBuffer.Buffer;
    SubmitInfo submitInfo = new()
    {
      SType = StructureType.SubmitInfo,
      WaitSemaphoreCount = 1,
      PWaitSemaphores = waitSemaphores,
      PWaitDstStageMask = &pipelineStageFlags,
      CommandBufferCount = 1,
      PCommandBuffers = &buffer,
      SignalSemaphoreCount = 1,
      PSignalSemaphores = signalSemaphores,
    };

    if (vk!.QueueSubmit(queue, 1, submitInfo, fence!.Get) != Result.Success)
    {
      throw new Exception("Failed to submit command buffer to queue.");
    }
  }

  public void Wait()
  {
    if (!isInitialized)
    {
      throw new Exception("GraphicsQueue has not been allocated.");
    }

    vk!.QueueWaitIdle(queue);
  }
}