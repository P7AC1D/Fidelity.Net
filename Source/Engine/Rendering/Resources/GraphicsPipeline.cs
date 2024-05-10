using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class GraphicsPipeline(Device device, PhysicalDevice physicalDevice) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private ShaderModule? vertexShader, fragmentShader;
  private VertexInputBindingDescription[]? vertexInputBindingDescriptions;
  private VertexInputAttributeDescription[]? vertexInputAttributeDescriptions;
  private PrimitiveTopology primitiveTopology = PrimitiveTopology.TriangleList;
  private Viewport? viewport;
  private Rect2D? scissor;
  private PipelineRasterizationStateCreateInfo? rasterizationState;
  private PipelineMultisampleStateCreateInfo? multisampleState;
  private PipelineDepthStencilStateCreateInfo? depthStencilState;

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
    this.vertexInputBindingDescriptions = vertexInputBindingDescriptions;
    this.vertexInputAttributeDescriptions = vertexInputAttributeDescriptions;
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