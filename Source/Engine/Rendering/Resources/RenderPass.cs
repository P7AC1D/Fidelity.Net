using Silk.NET.Vulkan;
using Fidelity.Rendering.Extensions;

namespace Fidelity.Rendering.Resources;

public unsafe class RenderPass(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private IList<AttachmentDescription> colorAttachments = [];
  private AttachmentDescription? depthAttachment;
  private AttachmentDescription? colorAttachmentResolve;
  private Silk.NET.Vulkan.RenderPass renderPass;
  private bool initialized = false;

  public Silk.NET.Vulkan.RenderPass Pass => renderPass;

  public RenderPass AddColorAttachment(
    Format format,
    SampleCountFlags samples = SampleCountFlags.Count1Bit,
    AttachmentLoadOp loadOp = AttachmentLoadOp.Clear,
    AttachmentStoreOp storeOp = AttachmentStoreOp.Store,
    AttachmentLoadOp stencilLoadOp = AttachmentLoadOp.DontCare,
    ImageLayout initialLayout = ImageLayout.Undefined,
    ImageLayout finalLayout = ImageLayout.ColorAttachmentOptimal)
  {
    if (initialized)
    {
      throw new Exception("Render pass has already been initialized.");
    }

    colorAttachments.Add(new AttachmentDescription
    {
      Format = format,
      Samples = samples,
      LoadOp = loadOp,
      StoreOp = storeOp,
      StencilLoadOp = stencilLoadOp,
      InitialLayout = initialLayout,
      FinalLayout = finalLayout,
    });
    return this;
  }

  public RenderPass AddDepthAttachment(
    SampleCountFlags samples = SampleCountFlags.Count1Bit,
    AttachmentLoadOp loadOp = AttachmentLoadOp.Clear,
    AttachmentStoreOp storeOp = AttachmentStoreOp.DontCare,
    AttachmentLoadOp stencilLoadOp = AttachmentLoadOp.DontCare,
    AttachmentStoreOp stencilStoreOp = AttachmentStoreOp.DontCare,
    ImageLayout initialLayout = ImageLayout.Undefined,
    ImageLayout finalLayout = ImageLayout.DepthStencilAttachmentOptimal
  )
  {
    if (initialized)
    {
      throw new Exception("Render pass has already been initialized.");
    }

    depthAttachment = new AttachmentDescription
    {
      Format = FindDepthFormat(),
      Samples = samples,
      LoadOp = loadOp,
      StoreOp = storeOp,
      StencilLoadOp = stencilLoadOp,
      StencilStoreOp = stencilStoreOp,
      InitialLayout = initialLayout,
      FinalLayout = finalLayout,
    };
    return this;
  }

  public RenderPass AddResolverAttachment(
    Format format,
    SampleCountFlags samples = SampleCountFlags.Count1Bit,
    AttachmentLoadOp loadOp = AttachmentLoadOp.DontCare,
    AttachmentStoreOp storeOp = AttachmentStoreOp.Store,
    AttachmentLoadOp stencilLoadOp = AttachmentLoadOp.DontCare,
    AttachmentStoreOp stencilStoreOp = AttachmentStoreOp.DontCare,
    ImageLayout initialLayout = ImageLayout.Undefined
  )
  {
    if (initialized)
    {
      throw new Exception("Render pass has already been initialized.");
    }

    colorAttachmentResolve = new AttachmentDescription
    {
      Format = format,
      Samples = samples,
      LoadOp = loadOp,
      StoreOp = storeOp,
      StencilLoadOp = stencilLoadOp,
      StencilStoreOp = stencilStoreOp,
      InitialLayout = initialLayout,
      FinalLayout = ImageLayout.PresentSrcKhr,
    };
    return this;
  }

  public RenderPass Allocate()
  {
    if (initialized)
    {
      throw new Exception("Render pass has already been initialized.");
    }

    if (!colorAttachments.Any() && depthAttachment == null)
    {
      throw new Exception("Render pass must have at least one color attachment or depth attachment.");
    }

    var colorAttachmentReference = colorAttachments.Select(x => new AttachmentReference
    {
      Attachment = (uint)colorAttachments.IndexOf(x),
      Layout = ImageLayout.ColorAttachmentOptimal
    }).ToArray();

    uint attachmentCount = (uint)colorAttachmentReference.Length;

    AttachmentReference depthAttachmentRef = default;
    if (depthAttachment.HasValue)
    {
      depthAttachmentRef = new()
      {
        Attachment = attachmentCount++,
        Layout = ImageLayout.DepthStencilAttachmentOptimal,
      };
    }

    AttachmentReference colorAttachmentResolveRef = default;
    if (colorAttachmentResolve.HasValue)
    {
      colorAttachmentResolveRef = new()
      {
        Attachment = attachmentCount++,
        Layout = ImageLayout.ColorAttachmentOptimal,
      };
    }

    fixed (AttachmentReference* colorAttachmentsRefPtr = colorAttachmentReference)
    {
      SubpassDescription subpass = new()
      {
        PipelineBindPoint = PipelineBindPoint.Graphics,
        ColorAttachmentCount = (uint)colorAttachmentReference.Length,
        PColorAttachments = colorAttachmentsRefPtr,
        PDepthStencilAttachment = depthAttachment.HasValue ? &depthAttachmentRef : null,
        PResolveAttachments = colorAttachmentResolve.HasValue ? &colorAttachmentResolveRef : null,
      };

      SubpassDependency dependency = new()
      {
        SrcSubpass = Vk.SubpassExternal,
        DstSubpass = 0,
        SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
        SrcAccessMask = 0,
        DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
        DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
      };

      var attachments = colorAttachments;
      if (depthAttachment.HasValue) attachments.Add(depthAttachment.Value);
      if (colorAttachmentResolve.HasValue) attachments.Add(colorAttachmentResolve.Value);

      fixed (AttachmentDescription* attachmentsPtr = attachments.ToArray())
      {
        RenderPassCreateInfo renderPassInfo = new()
        {
          SType = StructureType.RenderPassCreateInfo,
          AttachmentCount = (uint)attachments.Count,
          PAttachments = attachmentsPtr,
          SubpassCount = 1,
          PSubpasses = &subpass,
          DependencyCount = 1,
          PDependencies = &dependency,
        };

        if (vk!.CreateRenderPass(device, renderPassInfo, null, out renderPass) != Result.Success)
        {
          throw new Exception("Failed to create render pass.");
        }
      }
    }
    initialized = true;
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
      vk!.DestroyRenderPass(device, renderPass, null);
    }
  }

  private Format FindDepthFormat()
  {
    return physicalDevice.FindSupportedFormat(new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint }, ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit);
  }
}