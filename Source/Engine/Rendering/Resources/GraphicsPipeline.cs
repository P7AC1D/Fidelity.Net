using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class GraphicsPipeline(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private ShaderModule? vertexShader, fragmentShader;
  private IList<VertexInputBindingDescription> vertexInputBindingDescriptions;
  private IList<VertexInputAttributeDescription> vertexInputAttributeDescriptions;
  private PrimitiveTopology primitiveTopology = PrimitiveTopology.TriangleList;
  private Viewport? viewport;
  private Rect2D? scissor;
  private PipelineRasterizationStateCreateInfo? rasterizationState;
  private PipelineMultisampleStateCreateInfo? multisampleState;
  private PipelineDepthStencilStateCreateInfo? depthStencilState;
  private DescriptorSetLayout? descriptorSetLayout;
  private PipelineLayout pipelineLayout;
  private GraphicsRenderPass renderPass;
  private Pipeline graphicsPipeline;

  public Pipeline Pipeline => graphicsPipeline;
  public PipelineLayout PipelineLayout => pipelineLayout;

  public GraphicsPipeline SetVerteShader(byte[] shaderByteCode)
  {
    vertexShader = CreateShaderModule(shaderByteCode);
    return this;
  }

  public GraphicsPipeline SetFragmentShader(byte[] shaderByteCode)
  {
    fragmentShader = CreateShaderModule(shaderByteCode);
    return this;
  }

  public GraphicsPipeline SetInputAssemblyState(PrimitiveTopology primitiveTopology)
  {
    this.primitiveTopology = primitiveTopology;
    return this;
  }

  public GraphicsPipeline SetVertexInputState(
    VertexInputBindingDescription[] vertexInputBindingDescriptions,
    VertexInputAttributeDescription[] vertexInputAttributeDescriptions)
  {
    this.vertexInputBindingDescriptions = vertexInputBindingDescriptions.ToList();
    this.vertexInputAttributeDescriptions = vertexInputAttributeDescriptions.ToList();
    return this;
  }

  public GraphicsPipeline SetViewport(uint width, uint height, uint x = 0, uint y = 0, uint minDepth = 0, int maxDepth = 1)
  {
    viewport = new()
    {
      X = x,
      Y = y,
      Width = width,
      Height = height,
      MinDepth = minDepth,
      MaxDepth = maxDepth,
    };
    return this;
  }

  public GraphicsPipeline SetScissor(uint width, uint height, int x = 0, int y = 0)
  {
    scissor = new()
    {
      Offset = { X = (int)Math.Clamp(x, 0, width), Y = (int)Math.Clamp(y, 0, height) },
      Extent = new Extent2D { Height = height, Width = width },
    };
    return this;
  }

  public GraphicsPipeline SetRasterizationState(
    PolygonMode polygonMode = PolygonMode.Fill,
    bool depthClampEnabled = false,
    CullModeFlags cullMode = CullModeFlags.BackBit,
    FrontFace frontFace = FrontFace.CounterClockwise,
    bool depthBiasEnable = false)
  {
    rasterizationState = new()
    {
      SType = StructureType.PipelineRasterizationStateCreateInfo,
      DepthClampEnable = depthClampEnabled,
      RasterizerDiscardEnable = false,
      PolygonMode = polygonMode,
      LineWidth = 1,
      CullMode = cullMode,
      FrontFace = frontFace,
      DepthBiasEnable = depthBiasEnable,
    };
    return this;
  }

  public GraphicsPipeline SetMultisampleState(SampleCountFlags sampleCount = SampleCountFlags.Count1Bit)
  {
    multisampleState = new()
    {
      SType = StructureType.PipelineMultisampleStateCreateInfo,
      SampleShadingEnable = false,
      RasterizationSamples = sampleCount,
    };
    return this;
  }

  public GraphicsPipeline SetDepthStencilState(
    bool depthTestEnabled = true,
    bool depthWriteEnable = true,
    CompareOp depthCompareOp = CompareOp.Less,
    bool stencilTestEnabled = false)
  {
    depthStencilState = new()
    {
      SType = StructureType.PipelineDepthStencilStateCreateInfo,
      DepthTestEnable = depthTestEnabled,
      DepthWriteEnable = depthWriteEnable,
      DepthCompareOp = depthCompareOp,
      DepthBoundsTestEnable = false,
      StencilTestEnable = stencilTestEnabled,
    };
    return this;
  }

  public GraphicsPipeline SetDescriptorSetLayout(DescriptorSetLayout descriptorSetLayout)
  {
    this.descriptorSetLayout = descriptorSetLayout;
    return this;
  }

  public GraphicsPipeline SetRenderPass(GraphicsRenderPass renderPass)
  {
    this.renderPass = renderPass;
    return this;
  }

  public GraphicsPipeline Allocate()
  {
    ValidateInput();

    IList<PipelineShaderStageCreateInfo> shaderStages =
    [
      new PipelineShaderStageCreateInfo
      {
        SType = StructureType.PipelineShaderStageCreateInfo,
        Stage = ShaderStageFlags.VertexBit,
        Module = vertexShader!.Value,
        PName = (byte*)SilkMarshal.StringToPtr("main")
      },
      new PipelineShaderStageCreateInfo
      {
        SType = StructureType.PipelineShaderStageCreateInfo,
        Stage = ShaderStageFlags.FragmentBit,
        Module = fragmentShader!.Value,
        PName = (byte*)SilkMarshal.StringToPtr("main")
      }
    ];

    fixed (VertexInputAttributeDescription* attributeDescriptionsPtr = vertexInputAttributeDescriptions.ToArray())
    fixed (VertexInputBindingDescription* bindingDescriptionsPtr = vertexInputBindingDescriptions.ToArray())
    {
      PipelineVertexInputStateCreateInfo vertexInputInfo = new()
      {
        SType = StructureType.PipelineVertexInputStateCreateInfo,
        VertexBindingDescriptionCount = (uint)vertexInputBindingDescriptions.Count,
        VertexAttributeDescriptionCount = (uint)vertexInputAttributeDescriptions.Count,
        PVertexBindingDescriptions = bindingDescriptionsPtr,
        PVertexAttributeDescriptions = attributeDescriptionsPtr,
      };

      PipelineInputAssemblyStateCreateInfo inputAssembly = new()
      {
        SType = StructureType.PipelineInputAssemblyStateCreateInfo,
        Topology = primitiveTopology,
        PrimitiveRestartEnable = false,
      };

      var layout = descriptorSetLayout!.Value;
      PipelineLayoutCreateInfo pipelineLayoutInfo = new()
      {
        SType = StructureType.PipelineLayoutCreateInfo,
        PushConstantRangeCount = 0,
        SetLayoutCount = 1,
        PSetLayouts = &layout,
      };

      if (vk!.CreatePipelineLayout(device, pipelineLayoutInfo, null, out pipelineLayout) != Result.Success)
      {
        throw new Exception("Failed to create pipeline layout.");
      }

      var viewport = this.viewport!.Value;
      var scissor = this.scissor!.Value;
      PipelineViewportStateCreateInfo viewportState = new()
      {
        SType = StructureType.PipelineViewportStateCreateInfo,
        ViewportCount = 1,
        PViewports = &viewport,
        ScissorCount = 1,
        PScissors = &scissor,
      };

      var rasterizationState = this.rasterizationState!.Value;
      var multisampleState = this.multisampleState!.Value;
      var depthStencilState = this.depthStencilState!.Value;
  
      PipelineColorBlendAttachmentState colorBlendAttachment = new()
      {
        ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
        BlendEnable = false,
      };

      PipelineColorBlendStateCreateInfo colorBlending = new()
      {
        SType = StructureType.PipelineColorBlendStateCreateInfo,
        LogicOpEnable = false,
        LogicOp = LogicOp.Copy,
        AttachmentCount = 1,
        PAttachments = &colorBlendAttachment,
      };

      colorBlending.BlendConstants[0] = 0;
      colorBlending.BlendConstants[1] = 0;
      colorBlending.BlendConstants[2] = 0;
      colorBlending.BlendConstants[3] = 0;

      fixed (PipelineShaderStageCreateInfo* shaderStagesPtr = shaderStages.ToArray())
      {
        GraphicsPipelineCreateInfo pipelineInfo = new()
        {
          SType = StructureType.GraphicsPipelineCreateInfo,
          StageCount = (uint)shaderStages.Count,
          PStages = shaderStagesPtr,
          PVertexInputState = &vertexInputInfo,
          PInputAssemblyState = &inputAssembly,
          PViewportState = &viewportState,
          PRasterizationState = &rasterizationState,
          PMultisampleState = &multisampleState,
          PDepthStencilState = &depthStencilState,
          PColorBlendState = &colorBlending,
          Layout = pipelineLayout,
          RenderPass = renderPass!.Pass,
          Subpass = 0,
          BasePipelineHandle = default
        };

        if (vk!.CreateGraphicsPipelines(device, default, 1, pipelineInfo, null, out graphicsPipeline) != Result.Success)
        {
          throw new Exception("failed to create graphics pipeline!");
        }
      }
    }

    foreach (var shaderStage in shaderStages)
    {
      SilkMarshal.Free((nint)shaderStage.PName);
    }
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
      if (vertexShader.HasValue)
      {
        vk!.DestroyShaderModule(device, vertexShader.Value, null);
      }

      if (fragmentShader.HasValue)
      {
        vk!.DestroyShaderModule(device, fragmentShader.Value, null);
      }
    }
  }

  private void ValidateInput()
  {
    if (!vertexInputBindingDescriptions.Any() || !vertexInputAttributeDescriptions.Any())
    {
      throw new Exception("Vertex input state not set.");
    }

    if (!vertexShader.HasValue || !fragmentShader.HasValue)
    {
      throw new Exception("Vertex and fragment shaders must be set.");
    }

    if (viewport == null || scissor == null)
    {
      throw new Exception("Viewport and scissor must be set.");
    }

    if (rasterizationState == null)
    {
      throw new Exception("Rasterization state must be set.");
    }

    if (multisampleState == null)
    {
      throw new Exception("Multisample state must be set.");
    }

    if (depthStencilState == null)
    {
      throw new Exception("Depth stencil state must be set.");
    }

    if (!descriptorSetLayout.HasValue)
    {
      throw new Exception("Descriptor set layout bindings must be set.");
    }

    if (renderPass == null)
    {
      throw new Exception("Render pass must be set.");
    }
  }

  private ShaderModule CreateShaderModule(byte[] code)
  {
    ShaderModuleCreateInfo createInfo = new()
    {
      SType = StructureType.ShaderModuleCreateInfo,
      CodeSize = (nuint)code.Length,
    };

    ShaderModule shaderModule;

    fixed (byte* codePtr = code)
    {
      createInfo.PCode = (uint*)codePtr;

      if (vk!.CreateShaderModule(device, createInfo, null, out shaderModule) != Result.Success)
      {
        throw new Exception("Shader module creation failed.");
      }
    }
    return shaderModule;
  }
}