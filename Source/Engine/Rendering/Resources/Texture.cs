using Fidelity.Rendering.Extensions;
using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class Texture(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private DeviceMemory memory;
  private Image image;
  private bool initialized = false;

  public ImageView ImageView { get; private set; }
  public Image Image => image;

  public Texture Allocate(
    Extent3D extent,
    uint mipLevels,
    SampleCountFlags numSamples,
    Format format,
    ImageTiling tiling,
    ImageUsageFlags usage,
    MemoryPropertyFlags properties,
    ImageAspectFlags imageAspectFlags)
  {
    if (initialized)
    {
      throw new Exception("Texture has already been allocated.");
    }

    ImageCreateInfo imageInfo = new()
    {
      SType = StructureType.ImageCreateInfo,
      ImageType = ImageType.Type2D,
      Extent = extent,
      MipLevels = mipLevels,
      ArrayLayers = 1,
      Format = format,
      Tiling = tiling,
      InitialLayout = ImageLayout.Undefined,
      Usage = usage,
      Samples = numSamples,
      SharingMode = SharingMode.Exclusive,
    };

    fixed (Image* imagePtr = &image)
    {
      if (vk!.CreateImage(device, imageInfo, null, imagePtr) != Result.Success)
      {
        throw new Exception("Failed to create image.");
      }
    }

    vk!.GetImageMemoryRequirements(device, image, out MemoryRequirements memRequirements);

    MemoryAllocateInfo allocInfo = new()
    {
      SType = StructureType.MemoryAllocateInfo,
      AllocationSize = memRequirements.Size,
      MemoryTypeIndex = physicalDevice.FindMemoryType(memRequirements.MemoryTypeBits, properties),
    };

    fixed (DeviceMemory* imageMemoryPtr = &memory)
    {
      if (vk!.AllocateMemory(device, allocInfo, null, imageMemoryPtr) != Result.Success)
      {
        throw new Exception("Failed to allocate image memory.");
      }
    }

    vk!.BindImageMemory(device, image, memory, 0);

    ImageView = new ImageView(device)
      .SetImage(image, format)
      .SetRange(imageAspectFlags, 0, mipLevels, 0, 1)
      .Allocate();
    initialized = true;
    return this;
  }

  public Texture TransitionImageLayout(
    ImageLayout oldLayout,
    ImageLayout newLayout,
    uint mipLevels,
    CommandPool commandPool,
    Queue queue)
  {
    if (!initialized)
    {
      throw new Exception("Texture has not been allocated.");
    }

    CommandBuffer commandBuffer = BeginSingleTimeCommands(commandPool);

    ImageMemoryBarrier barrier = new()
    {
      SType = StructureType.ImageMemoryBarrier,
      OldLayout = oldLayout,
      NewLayout = newLayout,
      SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
      DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
      Image = image,
      SubresourceRange =
      {
          AspectMask = ImageAspectFlags.ColorBit,
          BaseMipLevel = 0,
          LevelCount = mipLevels,
          BaseArrayLayer = 0,
          LayerCount = 1,
      }
    };

    PipelineStageFlags sourceStage;
    PipelineStageFlags destinationStage;

    if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
    {
      barrier.SrcAccessMask = 0;
      barrier.DstAccessMask = AccessFlags.TransferWriteBit;

      sourceStage = PipelineStageFlags.TopOfPipeBit;
      destinationStage = PipelineStageFlags.TransferBit;
    }
    else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
    {
      barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
      barrier.DstAccessMask = AccessFlags.ShaderReadBit;

      sourceStage = PipelineStageFlags.TransferBit;
      destinationStage = PipelineStageFlags.FragmentShaderBit;
    }
    else
    {
      throw new Exception("Unsupported image layout transition.");
    }

    vk!.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, barrier);

    EndSingleTimeCommands(commandPool, commandBuffer, queue);
    return this;
  }

  public Texture CopyFromBuffer(GpuBuffer buffer, uint width, uint height, CommandPool commandPool, Queue queue)
  {
    if (!initialized)
    {
      throw new Exception("Texture has not been allocated.");
    }

    CommandBuffer commandBuffer = BeginSingleTimeCommands(commandPool);

    BufferImageCopy region = new()
    {
      BufferOffset = 0,
      BufferRowLength = 0,
      BufferImageHeight = 0,
      ImageSubresource =
      {
          AspectMask = ImageAspectFlags.ColorBit,
          MipLevel = 0,
          BaseArrayLayer = 0,
          LayerCount = 1,
      },
      ImageOffset = new Offset3D(0, 0, 0),
      ImageExtent = new Extent3D(width, height, 1),

    };

    vk!.CmdCopyBufferToImage(commandBuffer, buffer.Buffer, image, ImageLayout.TransferDstOptimal, 1, region);

    EndSingleTimeCommands(commandPool, commandBuffer, queue);
    return this;
  }

  public Texture GenerateMipMaps(Format imageFormat, uint width, uint height, uint mipLevels, CommandPool commandPool, Queue queue)
  {
    if (!initialized)
    {
      throw new Exception("Texture has not been allocated.");
    }

    vk!.GetPhysicalDeviceFormatProperties(physicalDevice, imageFormat, out var formatProperties);

    if ((formatProperties.OptimalTilingFeatures & FormatFeatureFlags.SampledImageFilterLinearBit) == 0)
    {
      throw new Exception("Texture image format does not support linear blitting.");
    }

    var commandBuffer = BeginSingleTimeCommands(commandPool);

    ImageMemoryBarrier barrier = new()
    {
      SType = StructureType.ImageMemoryBarrier,
      Image = image,
      SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
      DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
      SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseArrayLayer = 0,
                LayerCount = 1,
                LevelCount = 1,
            }
    };

    var mipWidth = width;
    var mipHeight = height;

    for (uint i = 1; i < mipLevels; i++)
    {
      barrier.SubresourceRange.BaseMipLevel = i - 1;
      barrier.OldLayout = ImageLayout.TransferDstOptimal;
      barrier.NewLayout = ImageLayout.TransferSrcOptimal;
      barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
      barrier.DstAccessMask = AccessFlags.TransferReadBit;

      vk!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit, 0,
          0, null,
          0, null,
          1, barrier);

      ImageBlit blit = new()
      {
        SrcOffsets =
                {
                    Element0 = new Offset3D(0,0,0),
                    Element1 = new Offset3D((int)mipWidth, (int)mipHeight, 1),
                },
        SrcSubresource =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = i - 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
        DstOffsets =
                {
                    Element0 = new Offset3D(0,0,0),
                    Element1 = new Offset3D((int)(mipWidth > 1 ? mipWidth / 2 : 1), (int)(mipHeight > 1 ? mipHeight / 2 : 1),1),
                },
        DstSubresource =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = i,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },

      };

      vk!.CmdBlitImage(commandBuffer,
          image, ImageLayout.TransferSrcOptimal,
          image, ImageLayout.TransferDstOptimal,
          1, blit,
          Filter.Linear);

      barrier.OldLayout = ImageLayout.TransferSrcOptimal;
      barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
      barrier.SrcAccessMask = AccessFlags.TransferReadBit;
      barrier.DstAccessMask = AccessFlags.ShaderReadBit;

      vk!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0,
          0, null,
          0, null,
          1, barrier);

      if (mipWidth > 1) mipWidth /= 2;
      if (mipHeight > 1) mipHeight /= 2;
    }

    barrier.SubresourceRange.BaseMipLevel = mipLevels - 1;
    barrier.OldLayout = ImageLayout.TransferDstOptimal;
    barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
    barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
    barrier.DstAccessMask = AccessFlags.ShaderReadBit;

    vk!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0,
        0, null,
        0, null,
        1, barrier);

    EndSingleTimeCommands(commandPool, commandBuffer, queue);
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
      vk!.DestroyImage(device, image, null);
      vk!.FreeMemory(device, memory, null);
    }
  }

  private CommandBuffer BeginSingleTimeCommands(CommandPool commandPool)
  {
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

  private void EndSingleTimeCommands(CommandPool commandPool, CommandBuffer commandBuffer, Queue graphicsQueue)
  {
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