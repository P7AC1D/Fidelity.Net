using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class Framebuffer(Device device) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Silk.NET.Vulkan.Framebuffer framebuffer;
  private RenderPass renderPass;
  private IList<ImageView> attachments = [];
  private uint width, height;
  private bool isInitialized = false;

  public Silk.NET.Vulkan.Framebuffer Buffer => framebuffer;

  public Framebuffer AddAttachment(ImageView imageView)
  {
    if (isInitialized)
    {
      throw new Exception("Framebuffer has already been initialized.");
    }

    attachments.Add(imageView);
    return this;
  }

  public Framebuffer SetRenderPass(RenderPass renderPass)
  {
    if (isInitialized)
    {
      throw new Exception("Framebuffer has already been initialized.");
    }
    this.renderPass = renderPass;
    return this;
  }

  public Framebuffer SetBoundary(uint width, uint height)
  {
    if (isInitialized)
    {
      throw new Exception("Framebuffer has already been initialized.");
    }

    this.width = width;
    this.height = height;
    return this;
  }

  public Framebuffer Allocate()
  {
    if (isInitialized)
    {
      throw new Exception("Framebuffer has already been initialized.");
    }

    fixed (Silk.NET.Vulkan.ImageView* attachmentsPtr = attachments.Select(x => x.View).ToArray())
    fixed (Silk.NET.Vulkan.Framebuffer* framebufferPtr = &framebuffer)
    {
      FramebufferCreateInfo framebufferInfo = new()
      {
        SType = StructureType.FramebufferCreateInfo,
        RenderPass = renderPass.Pass,
        AttachmentCount = (uint)attachments.Count,
        PAttachments = attachmentsPtr,
        Width = width,
        Height = height,
        Layers = 1,
      };

      if (vk!.CreateFramebuffer(device, framebufferInfo, null, framebufferPtr) != Result.Success)
      {
        throw new Exception("Failed to create Framebuffer.");
      }
    }

    isInitialized = true;
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
      if (attachments.Count > 0)
      {
        foreach (var attachment in attachments)
        {
          attachment.Dispose();
        }
        attachments.Clear();
      }

      vk!.DestroyFramebuffer(device, framebuffer, null);
    }
  }
}