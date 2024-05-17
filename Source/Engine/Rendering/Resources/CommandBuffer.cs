using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class CommandBuffer(Silk.NET.Vulkan.CommandBuffer commandBuffer)
{
  private readonly Vk vk = Vk.GetApi();
  private bool commandBufferRecording = false;
  private bool renderPassRecording = false;

  public Silk.NET.Vulkan.CommandBuffer Buffer => commandBuffer;

  public CommandBuffer Begin()
  {
    if (commandBufferRecording)
    {
      throw new Exception("CommandBuffer is already recording.");
    }

    CommandBufferBeginInfo beginInfo = new()
    {
      SType = StructureType.CommandBufferBeginInfo
    };

    if (vk!.BeginCommandBuffer(commandBuffer, &beginInfo) != Result.Success)
    {
      throw new Exception("Failed to begin recording CommandBuffer.");
    }
    commandBufferRecording = true;
    return this;
  }

  public CommandBuffer BeginRenderPass(RenderPass renderPass, Framebuffer framebuffer, Extent2D extent2D)
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is already recording.");
    }

    if (renderPassRecording)
    {
      throw new Exception("RenderPass is already recording.");
    }

    RenderPassBeginInfo renderPassInfo = new()
    {
      SType = StructureType.RenderPassBeginInfo,
      RenderPass = renderPass.Pass,
      Framebuffer = framebuffer.Buffer,
      RenderArea =
      {
          Offset = { X = 0, Y = 0 },
          Extent = extent2D,
      }
    };

    var clearValues = new ClearValue[]
    {
      new()
      {
        Color = new (){ Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 },
      },
      new()
      {
        DepthStencil = new () { Depth = 1, Stencil = 0 }
      }
    };

    fixed (ClearValue* clearValuesPtr = clearValues)
    {
      renderPassInfo.ClearValueCount = (uint)clearValues.Length;
      renderPassInfo.PClearValues = clearValuesPtr;

      vk!.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);
    }

    renderPassRecording = true;
    return this;
  }

  public CommandBuffer BindGraphicsPipeline(GraphicsPipeline pipeline)
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is not recording.");
    }

    if (!renderPassRecording)
    {
      throw new Exception("RenderPass is not recording.");
    }

    vk!.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline.Pipeline);
    return this;
  }

  public CommandBuffer BindVertexBuffer(GpuBuffer vertexBuffer)
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is not recording.");
    }

    if (!renderPassRecording)
    {
      throw new Exception("RenderPass is not recording.");
    }

    var vertexBuffers = new Silk.NET.Vulkan.Buffer[] { vertexBuffer.Buffer };
    var offsets = new ulong[] { 0 };

    fixed (ulong* offsetsPtr = offsets)
    fixed (Silk.NET.Vulkan.Buffer* vertexBuffersPtr = vertexBuffers)
    {
      vk!.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffersPtr, offsetsPtr);
    }
    return this;
  }

  public CommandBuffer BindIndexBuffer(GpuBuffer indexBuffer)
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is not recording.");
    }

    if (!renderPassRecording)
    {
      throw new Exception("RenderPass is not recording.");
    }

    vk!.CmdBindIndexBuffer(commandBuffer, indexBuffer.Buffer, 0, IndexType.Uint32);
    return this;
  }

  public CommandBuffer BindDescriptorSet(GraphicsPipeline graphicsPipeline, DescriptorSet descriptorSet)
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is not recording.");
    }

    if (!renderPassRecording)
    {
      throw new Exception("RenderPass is not recording.");
    }

    vk!.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, graphicsPipeline.Layout, 0, 1, descriptorSet!.Set, 0, null);
    return this;
  }

  public CommandBuffer DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is not recording.");
    }

    if (!renderPassRecording)
    {
      throw new Exception("RenderPass is not recording.");
    }

    vk!.CmdDrawIndexed(commandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    return this;
  }

  public CommandBuffer EndRenderPass()
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is not recording.");
    }

    if (!renderPassRecording)
    {
      throw new Exception("RenderPass is not recording.");
    }

    vk!.CmdEndRenderPass(commandBuffer);
    renderPassRecording = false;
    return this;
  }

  public CommandBuffer CopyBuffer(GpuBuffer srcBuffer, GpuBuffer dstBuffer, ulong size)
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is not recording.");
    }

    BufferCopy copyRegion = new()
    {
      Size = size
    };

    vk!.CmdCopyBuffer(commandBuffer, srcBuffer.Buffer, dstBuffer.Buffer, 1, &copyRegion);
    return this;
  }

  public CommandBuffer End()
  {
    if (!commandBufferRecording)
    {
      throw new Exception("CommandBuffer is not recording.");
    }

    if (renderPassRecording)
    {
      throw new Exception("RenderPass is still recording.");
    }

    if (vk!.EndCommandBuffer(commandBuffer) != Result.Success)
    {
      throw new Exception("Failed to record CommandBuffer.");
    }
    commandBufferRecording = false;
    return this;
  }
}