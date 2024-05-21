using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class CommandPool(Device device) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Silk.NET.Vulkan.CommandPool commandPool;
  private bool isInitialized = false;

  public Silk.NET.Vulkan.CommandPool Pool => commandPool;

  public CommandBuffer AllocateCommandBuffer()
  {
    return AllocateCommandBuffers(1)!.First();
  }

  public CommandBuffer[] AllocateCommandBuffers(uint count)
  {
    if (!isInitialized)
    {
      throw new Exception("CommandPool has not been allocated.");
    }

    Silk.NET.Vulkan.CommandBuffer[] buffers = new Silk.NET.Vulkan.CommandBuffer[count];

    CommandBufferAllocateInfo allocInfo = new()
    {
      SType = StructureType.CommandBufferAllocateInfo,
      CommandPool = commandPool,
      Level = CommandBufferLevel.Primary,
      CommandBufferCount = (uint)buffers.Length,
    };

    fixed (Silk.NET.Vulkan.CommandBuffer* commandBuffersPtr = buffers)
    {
      if (vk!.AllocateCommandBuffers(device, allocInfo, commandBuffersPtr) != Result.Success)
      {
        throw new Exception("Failed to allocate CommandBuffers.");
      }
    }

    CommandBuffer[] commandBuffers = new CommandBuffer[count];
    for (int i = 0; i < count; i++)
    {
      commandBuffers[i] = new CommandBuffer(buffers[i]);
    }
    return commandBuffers;
  }

  public CommandPool Create(uint queueFamilyIndex)
  {
    if (isInitialized)
    {
      throw new Exception("CommandPool has already been allocated.");
    }

    CommandPoolCreateInfo poolInfo = new()
    {
      SType = StructureType.CommandPoolCreateInfo,
      QueueFamilyIndex = queueFamilyIndex,
      Flags = CommandPoolCreateFlags.ResetCommandBufferBit
    };

    if (vk!.CreateCommandPool(device, poolInfo, null, out commandPool) != Result.Success)
    {
      throw new Exception("Failed to create CommandPool.");
    }
    isInitialized = true;
    return this;
  }

  public void FreeCommandBuffer(CommandBuffer commandBuffer)
  {
    FreeCommandBuffers([commandBuffer]);
  }

  public void FreeCommandBuffers(CommandBuffer[] commandBuffers)
  {
    fixed (Silk.NET.Vulkan.CommandBuffer* commandBuffersPtr = commandBuffers.Select(x => x.Buffer).ToArray())
    {
      vk!.FreeCommandBuffers(device, commandPool, (uint)commandBuffers!.Length, commandBuffersPtr);
    }
    commandBuffers = [];
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
      vk!.DestroyCommandPool(device, commandPool, null);
    }
  }
}