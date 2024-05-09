using Silk.NET.Vulkan;

namespace Fidelity;

public static class Utility
{
  public static CommandBuffer BeginSingleTimeCommands(CommandPool commandPool, Device device)
  {
    Vk vk = Vk.GetApi();

    CommandBufferAllocateInfo allocateInfo = new()
    {
      SType = StructureType.CommandBufferAllocateInfo,
      Level = CommandBufferLevel.Primary,
      CommandPool = commandPool,
      CommandBufferCount = 1,
    };

    vk!.AllocateCommandBuffers(device, allocateInfo, out CommandBuffer commandBuffer);

    CommandBufferBeginInfo beginInfo = new()
    {
      SType = StructureType.CommandBufferBeginInfo,
      Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
    };

    vk!.BeginCommandBuffer(commandBuffer, beginInfo);

    return commandBuffer;
  }

  public unsafe static void EndSingleTimeCommands(Queue graphicsQueue, CommandBuffer commandBuffer, Device device, CommandPool commandPool)
  {
    Vk vk = Vk.GetApi();

    vk!.EndCommandBuffer(commandBuffer);

    SubmitInfo submitInfo = new()
    {
      SType = StructureType.SubmitInfo,
      CommandBufferCount = 1,
      PCommandBuffers = &commandBuffer,
    };

    vk!.QueueSubmit(graphicsQueue, 1, submitInfo, default);
    vk!.QueueWaitIdle(graphicsQueue);

    vk!.FreeCommandBuffers(device, commandPool, 1, commandBuffer);
  }
}