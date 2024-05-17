using Fidelity.Rendering.Enums;
using Fidelity.Rendering.Extensions;
using Fidelity.Rendering.Resources;
using Silk.NET.Assimp;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;
using CommandBuffer = Fidelity.Rendering.Resources.CommandBuffer;
using CommandPool = Fidelity.Rendering.Resources.CommandPool;
using Framebuffer = Fidelity.Rendering.Resources.Framebuffer;
using ImageView = Fidelity.Rendering.Resources.ImageView;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using Texture = Fidelity.Rendering.Resources.Texture;

namespace Fidelity;

public struct QueueFamilyIndices
{
  public uint? GraphicsFamily { get; set; }
  public uint? PresentFamily { get; set; }

  public bool IsComplete()
  {
    return GraphicsFamily.HasValue && PresentFamily.HasValue;
  }
}

struct Vertex
{
  public Vector3D<float> pos;
  public Vector3D<float> color;

  public Vector2D<float> textCoord;

  public static VertexInputBindingDescription GetBindingDescription()
  {
    VertexInputBindingDescription bindingDescription = new()
    {
      Binding = 0,
      Stride = (uint)Unsafe.SizeOf<Vertex>(),
      InputRate = VertexInputRate.Vertex,
    };

    return bindingDescription;
  }

  public static VertexInputAttributeDescription[] GetAttributeDescriptions()
  {
    var attributeDescriptions = new[]
    {
      new VertexInputAttributeDescription()
      {
          Binding = 0,
          Location = 0,
          Format = Format.R32G32B32Sfloat,
          Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(pos)),
      },
      new VertexInputAttributeDescription()
      {
          Binding = 0,
          Location = 1,
          Format = Format.R32G32B32Sfloat,
          Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(color)),
      },
      new VertexInputAttributeDescription()
      {
          Binding = 0,
          Location = 2,
          Format = Format.R32G32Sfloat,
          Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(textCoord)),
      }
    };

    return attributeDescriptions;
  }
}

struct UniformBufferObject
{
  public Matrix4X4<float> model;
  public Matrix4X4<float> view;
  public Matrix4X4<float> proj;
}

struct SwapChainSupportDetails
{
  public SurfaceCapabilitiesKHR Capabilities;
  public SurfaceFormatKHR[] Formats;
  public PresentModeKHR[] PresentModes;
}

public unsafe class Application
{
  private readonly IWindow window;
  private Vk vk;
  private Instance instance;

  private ExtDebugUtils? debugUtils;
  private DebugUtilsMessengerEXT debugMessenger;
  private KhrSurface? khrSurface;
  private SurfaceKHR surface;

  private PhysicalDevice physicalDevice;
  private SampleCountFlags msaaSamples = SampleCountFlags.Count1Bit;
  private Device device;

  private Queue graphicsQueue;
  private Queue presentQueue;

  private bool EnableValidationLayers = true;
  private uint mipLevels;
  private KhrSwapchain? khrSwapChain;
  private SwapchainKHR swapChain;
  private Image[]? swapChainImages;
  private Format swapChainImageFormat;
  private Extent2D swapChainExtent;
  private ImageView[] swapChainImageViews;
  private Framebuffer[] swapChainFramebuffers;

  private GraphicsPipeline graphicsPipeline;
  private Rendering.Resources.RenderPass graphicsPipelineRenderPass;

  private GpuBuffer vertexBuffer, indexBuffer;
  private GpuBuffer[] uniformBuffers;

  private DescriptorPool descriptorPool;
  private Rendering.Resources.DescriptorSet[]? descriptorSets;
  private Rendering.Resources.DescriptorSetLayout descriptorSetLayout;

  private Texture texture;
  private TextureSampler textureSampler;

  private Texture colorImage;
  private Texture depthImage;

  private CommandPool commandPool;
  private CommandBuffer[] commandBuffers;

  private Semaphore[]? imageAvailableSemaphores;
  private Semaphore[]? renderFinishedSemaphores;
  private Fence[]? inFlightFences;
  private Fence[]? imagesInFlight;
  private int currentFrame = 0;
  private bool frameBufferResized = false;

  const int MAX_FRAMES_IN_FLIGHT = 2;
  const string MODEL_PATH = @"Assets/viking_room.obj";
  const string TEXTURE_PATH = @"Assets/viking_room.png";

  private Vertex[] vertices;
  private uint[] indices;

  private readonly string[] deviceExtensions =
    [
        KhrSwapchain.ExtensionName
    ];

  private readonly string[] validationLayers =
    [
        "VK_LAYER_KHRONOS_validation"
    ];

  public Application()
  {
    var options = WindowOptions.DefaultVulkan;
    options.Size = new Vector2D<int>(800, 600);
    options.Title = "Vulkan using Silk.NET";

    window = Window.Create(options);
    window.Initialize();

    window.Load += Load;
    window.Update += Update;
    window.Render += Render;
    window.Resize += FramebufferResizeCallback;

    if (window.VkSurface is null)
    {
      throw new Exception("Windowing platform doesn't support Vulkan.");
    }

    InitVulkan();
  }

  public void KeyDown(IKeyboard arg1, Key arg2, int arg3)
  {
    if (arg2 == Key.Escape)
    {
      window.Close();
    }
  }

  public void Load()
  {
  }

  private void RecreateSwapChain()
  {
    Vector2D<int> framebufferSize = window!.FramebufferSize;

    while (framebufferSize.X == 0 || framebufferSize.Y == 0)
    {
      framebufferSize = window.FramebufferSize;
      window.DoEvents();
    }

    vk!.DeviceWaitIdle(device);

    CleanUpSwapChain();

    CreateSwapChain();
    CreateImageViews();
    CreateRenderPass();
    CreateColorResources();
    CreateDepthResources();
    CreateFramebuffers();
    CreateUniformBuffers();
    CreateDescriptorPool();
    CreateDescriptorSets();
    CreateGraphicsPipeline();
    CreateCommandBuffers();

    imagesInFlight = new Fence[swapChainImages!.Length];
  }

  public void Render(double dt)
  {
    vk!.WaitForFences(device, 1, inFlightFences![currentFrame], true, ulong.MaxValue);

    uint imageIndex = 0;
    var result = khrSwapChain!.AcquireNextImage(device, swapChain, ulong.MaxValue, imageAvailableSemaphores![currentFrame], default, ref imageIndex);

    if (result == Result.ErrorOutOfDateKhr)
    {
      RecreateSwapChain();
      return;
    }
    else if (result != Result.Success && result != Result.SuboptimalKhr)
    {
      throw new Exception("failed to acquire swap chain image!");
    }

    UpdateUniformBuffer(imageIndex);

    if (imagesInFlight![imageIndex].Handle != default)
    {
      vk!.WaitForFences(device, 1, imagesInFlight[imageIndex], true, ulong.MaxValue);
    }
    imagesInFlight[imageIndex] = inFlightFences[currentFrame];

    SubmitInfo submitInfo = new()
    {
      SType = StructureType.SubmitInfo,
    };

    var waitSemaphores = stackalloc[] { imageAvailableSemaphores[currentFrame] };
    var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };

    var buffer = commandBuffers![imageIndex]!.Buffer;

    submitInfo = submitInfo with
    {
      WaitSemaphoreCount = 1,
      PWaitSemaphores = waitSemaphores,
      PWaitDstStageMask = waitStages,

      CommandBufferCount = 1,
      PCommandBuffers = &buffer
    };

    var signalSemaphores = stackalloc[] { renderFinishedSemaphores![currentFrame] };
    submitInfo = submitInfo with
    {
      SignalSemaphoreCount = 1,
      PSignalSemaphores = signalSemaphores,
    };

    vk!.ResetFences(device, 1, inFlightFences[currentFrame]);

    if (vk!.QueueSubmit(graphicsQueue, 1, submitInfo, inFlightFences[currentFrame]) != Result.Success)
    {
      throw new Exception("failed to submit draw command buffer!");
    }

    var swapChains = stackalloc[] { swapChain };
    PresentInfoKHR presentInfo = new()
    {
      SType = StructureType.PresentInfoKhr,

      WaitSemaphoreCount = 1,
      PWaitSemaphores = signalSemaphores,

      SwapchainCount = 1,
      PSwapchains = swapChains,

      PImageIndices = &imageIndex
    };

    result = khrSwapChain.QueuePresent(presentQueue, presentInfo);

    if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || frameBufferResized)
    {
      frameBufferResized = false;
      RecreateSwapChain();
    }
    else if (result != Result.Success)
    {
      throw new Exception("failed to present swap chain image!");
    }

    currentFrame = (currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
  }

  public void Run()
  {
    window.Run();
    CleanUp();
  }

  public void Update(double dt)
  {

  }

  private void CleanUpSwapChain()
  {
    depthImage.Dispose();
    colorImage.Dispose();

    foreach (var framebuffer in swapChainFramebuffers!)
    {
      framebuffer.Dispose();
    }

    commandPool.FreeCommandBuffers();

    graphicsPipeline.Dispose();
    graphicsPipelineRenderPass.Dispose();

    foreach (var imageView in swapChainImageViews!)
    {
      imageView.Dispose();
    }

    khrSwapChain!.DestroySwapchain(device, swapChain, null);

    for (int i = 0; i < swapChainImages!.Length; i++)
    {
      uniformBuffers[i]?.Dispose();
    }

    vk!.DestroyDescriptorPool(device, descriptorPool, null);
  }

  private void FramebufferResizeCallback(Vector2D<int> obj)
  {
    frameBufferResized = true;
  }

  private void CleanUp()
  {
    CleanUpSwapChain();

    texture!.Dispose();
    descriptorSetLayout!.Dispose();

    vertexBuffer!.Dispose();
    indexBuffer!.Dispose();

    for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
    {
      vk!.DestroySemaphore(device, renderFinishedSemaphores![i], null);
      vk!.DestroySemaphore(device, imageAvailableSemaphores![i], null);
      vk!.DestroyFence(device, inFlightFences![i], null);
    }

    commandPool.Dispose();

    vk!.DestroyDevice(device, null);

    if (EnableValidationLayers)
    {
      //DestroyDebugUtilsMessenger equivilant to method DestroyDebugUtilsMessengerEXT from original tutorial.
      debugUtils!.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
    }

    khrSurface!.DestroySurface(instance, surface, null);
    vk!.DestroyInstance(instance, null);
    vk!.Dispose();

    window?.Dispose();
  }

  private void InitVulkan()
  {
    CreateInstance();
    SetupDebugMessenger();
    CreateSurface();
    PickPhysicalDevice();
    CreateLogicalDevice();
    CreateSwapChain();
    CreateImageViews();
    CreateRenderPass();
    CreateCommandPool();
    CreateColorResources();
    CreateDepthResources();
    CreateFramebuffers();
    CreateTextureImage();
    CreateTextureSampler();
    LoadModel();
    CreateVertexBuffer();
    CreateIndexBuffer();
    CreateUniformBuffers();
    CreateDescriptorPool();
    CreateDescriptorSetLayout();
    CreateDescriptorSets();
    CreateGraphicsPipeline();
    CreateCommandBuffers();
    CreateSyncObjects();
  }

  private void CreateSyncObjects()
  {
    imageAvailableSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
    renderFinishedSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
    inFlightFences = new Fence[MAX_FRAMES_IN_FLIGHT];
    imagesInFlight = new Fence[swapChainImages!.Length];

    SemaphoreCreateInfo semaphoreInfo = new()
    {
      SType = StructureType.SemaphoreCreateInfo,
    };

    FenceCreateInfo fenceInfo = new()
    {
      SType = StructureType.FenceCreateInfo,
      Flags = FenceCreateFlags.SignaledBit,
    };

    for (var i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
    {
      if (vk!.CreateSemaphore(device, semaphoreInfo, null, out imageAvailableSemaphores[i]) != Result.Success ||
          vk!.CreateSemaphore(device, semaphoreInfo, null, out renderFinishedSemaphores[i]) != Result.Success ||
          vk!.CreateFence(device, fenceInfo, null, out inFlightFences[i]) != Result.Success)
      {
        throw new Exception("failed to create synchronization objects for a frame!");
      }
    }
  }

  private void CreateCommandPool()
  {
    commandPool = new CommandPool(device)
      .Create(physicalDevice.FindQueueFamilies(khrSurface, surface));
  }

  private void LoadModel()
  {
    using var assimp = Assimp.GetApi();
    var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    var scene = assimp.ImportFile($"{assemblyPath}/{MODEL_PATH}", (uint)PostProcessPreset.TargetRealTimeMaximumQuality);

    var vertexMap = new Dictionary<Vertex, uint>();
    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    VisitSceneNode(scene->MRootNode);

    assimp.ReleaseImport(scene);

    this.vertices = vertices.ToArray();
    this.indices = indices.ToArray();

    void VisitSceneNode(Node* node)
    {
      for (int m = 0; m < node->MNumMeshes; m++)
      {
        var mesh = scene->MMeshes[node->MMeshes[m]];

        for (int f = 0; f < mesh->MNumFaces; f++)
        {
          var face = mesh->MFaces[f];

          for (int i = 0; i < face.MNumIndices; i++)
          {
            uint index = face.MIndices[i];

            var position = mesh->MVertices[index];
            var texture = mesh->MTextureCoords[0][(int)index];

            Vertex vertex = new()
            {
              pos = new Vector3D<float>(position.X, position.Y, position.Z),
              color = new Vector3D<float>(1, 1, 1),
              //Flip Y for OBJ in Vulkan
              textCoord = new Vector2D<float>(texture.X, 1.0f - texture.Y)
            };

            if (vertexMap.TryGetValue(vertex, out var meshIndex))
            {
              indices.Add(meshIndex);
            }
            else
            {
              indices.Add((uint)vertices.Count);
              vertexMap[vertex] = (uint)vertices.Count;
              vertices.Add(vertex);
            }
          }
        }
      }

      for (int c = 0; c < node->MNumChildren; c++)
      {
        VisitSceneNode(node->MChildren[c]);
      }
    }
  }

  private void CreateTextureImage()
  {
    var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>($"{assemblyPath}/{TEXTURE_PATH}");

    ulong imageSize = (ulong)(img.Width * img.Height * img.PixelType.BitsPerPixel / 8);
    mipLevels = (uint)(Math.Floor(Math.Log2(Math.Max(img.Width, img.Height))) + 1);

    using GpuBuffer stagingBuffer = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Staging, imageSize);

    void* mappedData = stagingBuffer.MapRange(0, imageSize);
    img.CopyPixelDataTo(new Span<byte>(mappedData, (int)imageSize));
    stagingBuffer.Unmap();

    texture = new Texture(device, physicalDevice)
      .Allocate(new Extent3D
      {
        Width = (uint)img.Width,
        Height = (uint)img.Height,
        Depth = 1
      },
        mipLevels,
        SampleCountFlags.Count1Bit,
        Format.R8G8B8A8Srgb,
        ImageTiling.Optimal,
        ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
        MemoryPropertyFlags.DeviceLocalBit,
        ImageAspectFlags.ColorBit)
      .TransitionImageLayout(ImageLayout.Undefined, ImageLayout.TransferDstOptimal, mipLevels, commandPool, graphicsQueue)
      .CopyFromBuffer(stagingBuffer, (uint)img.Width, (uint)img.Height, commandPool, graphicsQueue)
      .GenerateMipMaps(Format.R8G8B8A8Srgb, (uint)img.Width, (uint)img.Height, mipLevels, commandPool, graphicsQueue);
  }

  private SampleCountFlags GetMaxUsableSampleCount()
  {
    vk!.GetPhysicalDeviceProperties(physicalDevice, out var physicalDeviceProperties);

    var counts = physicalDeviceProperties.Limits.FramebufferColorSampleCounts & physicalDeviceProperties.Limits.FramebufferDepthSampleCounts;

    return counts switch
    {
      var c when (c & SampleCountFlags.Count64Bit) != 0 => SampleCountFlags.Count64Bit,
      var c when (c & SampleCountFlags.Count32Bit) != 0 => SampleCountFlags.Count32Bit,
      var c when (c & SampleCountFlags.Count16Bit) != 0 => SampleCountFlags.Count16Bit,
      var c when (c & SampleCountFlags.Count8Bit) != 0 => SampleCountFlags.Count8Bit,
      var c when (c & SampleCountFlags.Count4Bit) != 0 => SampleCountFlags.Count4Bit,
      var c when (c & SampleCountFlags.Count2Bit) != 0 => SampleCountFlags.Count2Bit,
      _ => SampleCountFlags.Count1Bit
    };
  }

  private void CreateTextureSampler()
  {
    textureSampler = new TextureSampler(device, physicalDevice)
      .SetMipmapping(maxLod: mipLevels)
      .Allocate();
  }

  private void CreateVertexBuffer()
  {
    ulong bufferSize = (ulong)(Unsafe.SizeOf<Vertex>() * vertices!.Length);

    using GpuBuffer staging = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Staging, bufferSize)
      .WriteDataArray<Vertex>(vertices);

    vertexBuffer = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Vertex, bufferSize);

    staging.CopyData(vertexBuffer, bufferSize, commandPool, graphicsQueue);
  }

  private void CreateIndexBuffer()
  {
    ulong bufferSize = (ulong)(Unsafe.SizeOf<uint>() * indices!.Length);

    using GpuBuffer staging = new GpuBuffer(device, physicalDevice);
    staging.Allocate(BufferType.Staging, bufferSize)
      .WriteDataArray<uint>(indices);

    indexBuffer = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Index, bufferSize);
    staging.CopyData(indexBuffer, bufferSize, commandPool, graphicsQueue);
  }

  private void CreateUniformBuffers()
  {
    ulong bufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();

    uniformBuffers = new GpuBuffer[swapChainImages!.Length];

    for (int i = 0; i < swapChainImages.Length; i++)
    {
      uniformBuffers[i] = new GpuBuffer(device, physicalDevice)
        .Allocate(BufferType.Uniform, bufferSize);
    }
  }

  private void CreateDescriptorPool()
  {
    var poolSizes = new DescriptorPoolSize[]
    {
      new DescriptorPoolSize()
      {
          Type = DescriptorType.UniformBuffer,
          DescriptorCount = (uint)swapChainImages!.Length,
      },
      new DescriptorPoolSize()
      {
          Type = DescriptorType.CombinedImageSampler,
          DescriptorCount = (uint)swapChainImages!.Length,
      }
    };

    fixed (DescriptorPoolSize* poolSizesPtr = poolSizes)
    fixed (DescriptorPool* descriptorPoolPtr = &descriptorPool)
    {

      DescriptorPoolCreateInfo poolInfo = new()
      {
        SType = StructureType.DescriptorPoolCreateInfo,
        PoolSizeCount = (uint)poolSizes.Length,
        PPoolSizes = poolSizesPtr,
        MaxSets = (uint)swapChainImages!.Length,
      };

      if (vk!.CreateDescriptorPool(device, poolInfo, null, descriptorPoolPtr) != Result.Success)
      {
        throw new Exception("failed to create descriptor pool!");
      }

    }
  }

  private void CreateDescriptorSetLayout()
  {
    descriptorSetLayout = new Rendering.Resources.DescriptorSetLayout(device)
      .AddBinding(0, DescriptorType.UniformBuffer, 1, ShaderStageFlags.VertexBit)
      .AddBinding(1, DescriptorType.CombinedImageSampler, 1, ShaderStageFlags.FragmentBit)
      .Allocate();
  }

  private void CreateDescriptorSets()
  {
    descriptorSets = new Rendering.Resources.DescriptorSet[swapChainImages!.Length];
    for (int i = 0; i < swapChainImages!.Length; i++)
    {
      descriptorSets[i] = new Rendering.Resources.DescriptorSet(device, descriptorPool)
        .BindUniformBuffer(uniformBuffers![i], 0)
        .BindTexureSampler(texture, textureSampler, 1)
        .SetLayout(descriptorSetLayout)
        .Allocate()
        .Update();
    }
  }

  private void UpdateUniformBuffer(uint currentImage)
  {
    //Silk Window has timing information so we are skipping the time code.
    var time = (float)window!.Time;

    UniformBufferObject ubo = new()
    {
      model = Matrix4X4<float>.Identity * Matrix4X4.CreateFromAxisAngle<float>(new Vector3D<float>(0, 0, 1), time * Scalar.DegreesToRadians(90.0f)),
      view = Matrix4X4.CreateLookAt(new Vector3D<float>(2, 2, 2), new Vector3D<float>(0, 0, 0), new Vector3D<float>(0, 0, 1)),
      proj = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), (float)swapChainExtent.Width / swapChainExtent.Height, 0.1f, 10.0f),
    };
    ubo.proj.M22 *= -1;

    uniformBuffers![currentImage].WriteData(ubo);

  }

  private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
  {
    vk!.GetPhysicalDeviceMemoryProperties(physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

    for (int i = 0; i < memProperties.MemoryTypeCount; i++)
    {
      if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
      {
        return (uint)i;
      }
    }

    throw new Exception("failed to find suitable memory type!");
  }

  private void CreateCommandBuffers()
  {
    commandBuffers = commandPool.AllocateCommandBuffers((uint)swapChainFramebuffers!.Length);

    for (int i = 0; i < commandBuffers.Length; i++)
    {
      commandBuffers[i].Begin()
        .BeginRenderPass(graphicsPipelineRenderPass, swapChainFramebuffers[i], swapChainExtent)
        .BindGraphicsPipeline(graphicsPipeline)
        .BindVertexBuffer(vertexBuffer)
        .BindIndexBuffer(indexBuffer)
        .BindDescriptorSet(graphicsPipeline, descriptorSets![i])
        .DrawIndexed((uint)indices!.Length)
        .EndRenderPass()
        .End();
    }
  }

  private void CreateImageViews()
  {
    swapChainImageViews = new ImageView[swapChainImages!.Length];

    for (int i = 0; i < swapChainImages.Length; i++)
    {
      swapChainImageViews[i] = new ImageView(device)
        .SetImage(swapChainImages[i], swapChainImageFormat)
        .SetRange(ImageAspectFlags.ColorBit)
        .Allocate();
    }
  }

  private void CreateGraphicsPipeline()
  {
    var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    var vertShaderCode = System.IO.File.ReadAllBytes($"{assemblyPath}/shaders/Depth-vert.spv");
    var fragShaderCode = System.IO.File.ReadAllBytes($"{assemblyPath}/shaders/Depth-frag.spv");

    graphicsPipeline = new GraphicsPipeline(device)
      .SetVerteShader(vertShaderCode)
      .SetFragmentShader(fragShaderCode)
      .SetVertexInputState([.. (new List<VertexInputBindingDescription> { Vertex.GetBindingDescription() })], Vertex.GetAttributeDescriptions())
      .SetViewport(swapChainExtent.Width, swapChainExtent.Height)
      .SetScissor(swapChainExtent.Width, swapChainExtent.Height)
      .SetRasterizationState()
      .SetMultisampleState(msaaSamples)
      .SetDepthStencilState()
      .SetDescriptorSetLayout(descriptorSetLayout)
      .SetRenderPass(graphicsPipelineRenderPass)
      .Allocate();
  }

  private void CreateRenderPass()
  {
        graphicsPipelineRenderPass = new Rendering.Resources.RenderPass(device, physicalDevice)
      .AddColorAttachment(swapChainImageFormat, msaaSamples)
      .AddDepthAttachment(msaaSamples)
      .AddResolverAttachment(swapChainImageFormat)
      .Allocate();
  }

  private void CreateSurface()
  {
    if (!vk!.TryGetInstanceExtension<KhrSurface>(instance, out khrSurface))
    {
      throw new NotSupportedException("KHR_surface extension not found.");
    }

    surface = window!.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
  }

  private void PickPhysicalDevice()
  {
    var devices = vk!.GetPhysicalDevices(instance);

    foreach (var device in devices)
    {
      if (IsDeviceSuitable(device))
      {
        physicalDevice = device;
        msaaSamples = GetMaxUsableSampleCount();
        break;
      }
    }

    if (physicalDevice.Handle == 0)
    {
      throw new Exception("failed to find a suitable GPU!");
    }
  }

  private bool IsDeviceSuitable(PhysicalDevice device)
  {
    var indices = FindQueueFamilies(device);

    bool extensionsSupported = CheckDeviceExtensionsSupport(device);

    bool swapChainAdequate = false;
    if (extensionsSupported)
    {
      var swapChainSupport = QuerySwapChainSupport(device);
      swapChainAdequate = swapChainSupport.Formats.Any() && swapChainSupport.PresentModes.Any();
    }

    vk!.GetPhysicalDeviceFeatures(device, out PhysicalDeviceFeatures supportedFeatures);

    return indices.IsComplete() && extensionsSupported && swapChainAdequate && supportedFeatures.SamplerAnisotropy;
  }

  private void CreateLogicalDevice()
  {
    var indices = FindQueueFamilies(physicalDevice);

    var uniqueQueueFamilies = new[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
    uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

    using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
    var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

    float queuePriority = 1.0f;
    for (int i = 0; i < uniqueQueueFamilies.Length; i++)
    {
      queueCreateInfos[i] = new()
      {
        SType = StructureType.DeviceQueueCreateInfo,
        QueueFamilyIndex = uniqueQueueFamilies[i],
        QueueCount = 1,
        PQueuePriorities = &queuePriority
      };
    }

    PhysicalDeviceFeatures deviceFeatures = new()
    {
      SamplerAnisotropy = true,
    };

    DeviceCreateInfo createInfo = new()
    {
      SType = StructureType.DeviceCreateInfo,
      QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
      PQueueCreateInfos = queueCreateInfos,

      PEnabledFeatures = &deviceFeatures,

      EnabledExtensionCount = (uint)deviceExtensions.Length,
      PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions)
    };

    if (EnableValidationLayers)
    {
      createInfo.EnabledLayerCount = (uint)validationLayers.Length;
      createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
    }
    else
    {
      createInfo.EnabledLayerCount = 0;
    }

    if (vk!.CreateDevice(physicalDevice, in createInfo, null, out device) != Result.Success)
    {
      throw new Exception("failed to create logical device!");
    }

    vk!.GetDeviceQueue(device, indices.GraphicsFamily!.Value, 0, out graphicsQueue);
    vk!.GetDeviceQueue(device, indices.PresentFamily!.Value, 0, out presentQueue);

    if (EnableValidationLayers)
    {
      SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
    }
  }

  private bool CheckDeviceExtensionsSupport(PhysicalDevice device)
  {
    uint extentionsCount = 0;
    vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extentionsCount, null);

    var availableExtensions = new ExtensionProperties[extentionsCount];
    fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
    {
      vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extentionsCount, availableExtensionsPtr);
    }

    var availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)).ToHashSet();

    return deviceExtensions.All(availableExtensionNames.Contains);

  }

  private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
  {
    var indices = new QueueFamilyIndices();

    uint queueFamilityCount = 0;
    vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, null);

    var queueFamilies = new QueueFamilyProperties[queueFamilityCount];
    fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
    {
      vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, queueFamiliesPtr);
    }


    uint i = 0;
    foreach (var queueFamily in queueFamilies)
    {
      if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
      {
        indices.GraphicsFamily = i;
      }

      khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, surface, out var presentSupport);

      if (presentSupport)
      {
        indices.PresentFamily = i;
      }

      if (indices.IsComplete())
      {
        break;
      }

      i++;
    }

    return indices;
  }

  private void CreateInstance()
  {
    vk = Vk.GetApi();

    if (EnableValidationLayers && !CheckValidationLayerSupport())
    {
      throw new Exception("validation layers requested, but not available!");
    }

    ApplicationInfo appInfo = new()
    {
      SType = StructureType.ApplicationInfo,
      PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Hello Triangle"),
      ApplicationVersion = new Version32(1, 0, 0),
      PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
      EngineVersion = new Version32(1, 0, 0),
      ApiVersion = Vk.Version11
    };

    InstanceCreateInfo createInfo = new()
    {
      SType = StructureType.InstanceCreateInfo,
      PApplicationInfo = &appInfo
    };

    var extensions = GetRequiredExtensions();
    createInfo.EnabledExtensionCount = (uint)extensions.Length;
    createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions); ;
    createInfo.Flags = InstanceCreateFlags.EnumeratePortabilityBitKhr;

    if (EnableValidationLayers)
    {
      createInfo.EnabledLayerCount = (uint)validationLayers.Length;
      createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);

      DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
      PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
      createInfo.PNext = &debugCreateInfo;
    }
    else
    {
      createInfo.EnabledLayerCount = 0;
      createInfo.PNext = null;
    }

    if (vk.CreateInstance(createInfo, null, out instance) != Result.Success)
    {
      throw new Exception("failed to create instance!");
    }

    Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
    Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
    SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

    if (EnableValidationLayers)
    {
      SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
    }
  }

  private void CreateFramebuffers()
  {
    swapChainFramebuffers = new Framebuffer[swapChainImageViews!.Length];
    for (int i = 0; i < swapChainImageViews.Length; i++)
    {
      swapChainFramebuffers[i] = new Framebuffer(device)
        .AddAttachment(colorImage.ImageView)
        .AddAttachment(depthImage.ImageView)
        .AddAttachment(swapChainImageViews[i])
        .SetRenderPass(graphicsPipelineRenderPass)
        .SetBoundary(swapChainExtent.Width, swapChainExtent.Height)
        .Allocate();
    }
  }

  private void CreateColorResources()
  {
    colorImage = new Texture(device, physicalDevice);
    colorImage.Allocate(
      new Extent3D
      {
        Width = swapChainExtent.Width,
        Height = swapChainExtent.Height,
        Depth = 1
      },
      1,
      msaaSamples,
      swapChainImageFormat,
      ImageTiling.Optimal,
      ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit,
      MemoryPropertyFlags.DeviceLocalBit,
      ImageAspectFlags.ColorBit);
  }

  private void CreateDepthResources()
  {
    Format depthFormat = physicalDevice.FindSupportedFormat(new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint }, ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit);

    depthImage = new Texture(device, physicalDevice);
    depthImage.Allocate(
      new Extent3D
      {
        Width = swapChainExtent.Width,
        Height = swapChainExtent.Height,
        Depth = 1
      },
      1,
      msaaSamples,
      depthFormat,
      ImageTiling.Optimal,
      ImageUsageFlags.DepthStencilAttachmentBit,
      MemoryPropertyFlags.DeviceLocalBit,
      ImageAspectFlags.DepthBit);
  }

  private string[] GetRequiredExtensions()
  {
    // TODO: DEBUG
    var glfwExtensions = window!.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
    var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);
    extensions = extensions.Append("VK_KHR_portability_enumeration").ToArray();
    if (EnableValidationLayers)
    {
      return extensions.Append(ExtDebugUtils.ExtensionName).ToArray();
    }

    return extensions;
  }

  private void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
  {
    createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
    createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                 DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                 DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
    createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                             DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                             DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
    createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
  }

  private void SetupDebugMessenger()
  {
    if (!EnableValidationLayers) return;

    //TryGetInstanceExtension equivilant to method CreateDebugUtilsMessengerEXT from original tutorial.
    if (!vk!.TryGetInstanceExtension(instance, out debugUtils)) return;

    DebugUtilsMessengerCreateInfoEXT createInfo = new();
    PopulateDebugMessengerCreateInfo(ref createInfo);

    if (debugUtils!.CreateDebugUtilsMessenger(instance, in createInfo, null, out debugMessenger) != Result.Success)
    {
      throw new Exception("failed to set up debug messenger!");
    }
  }

  private bool CheckValidationLayerSupport()
  {
    uint layerCount = 0;
    vk!.EnumerateInstanceLayerProperties(ref layerCount, null);
    var availableLayers = new LayerProperties[layerCount];
    fixed (LayerProperties* availableLayersPtr = availableLayers)
    {
      vk!.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
    }

    var availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)).ToHashSet();

    return validationLayers.All(availableLayerNames.Contains);
  }

  private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
  {
    Console.WriteLine($"validation layer:" + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));

    return Vk.False;
  }

  private SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice physicalDevice)
  {
    var details = new SwapChainSupportDetails();

    khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, out details.Capabilities);

    uint formatCount = 0;
    khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, null);

    if (formatCount != 0)
    {
      details.Formats = new SurfaceFormatKHR[formatCount];
      fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
      {
        khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, formatsPtr);
      }
    }
    else
    {
      details.Formats = Array.Empty<SurfaceFormatKHR>();
    }

    uint presentModeCount = 0;
    khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, null);

    if (presentModeCount != 0)
    {
      details.PresentModes = new PresentModeKHR[presentModeCount];
      fixed (PresentModeKHR* formatsPtr = details.PresentModes)
      {
        khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, formatsPtr);
      }

    }
    else
    {
      details.PresentModes = Array.Empty<PresentModeKHR>();
    }

    return details;
  }

  private SurfaceFormatKHR ChooseSwapSurfaceFormat(IReadOnlyList<SurfaceFormatKHR> availableFormats)
  {
    foreach (var availableFormat in availableFormats)
    {
      if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
      {
        return availableFormat;
      }
    }

    return availableFormats[0];
  }

  private PresentModeKHR ChoosePresentMode(IReadOnlyList<PresentModeKHR> availablePresentModes)
  {
    foreach (var availablePresentMode in availablePresentModes)
    {
      if (availablePresentMode == PresentModeKHR.MailboxKhr)
      {
        return availablePresentMode;
      }
    }

    return PresentModeKHR.FifoKhr;
  }

  private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
  {
    if (capabilities.CurrentExtent.Width != uint.MaxValue)
    {
      return capabilities.CurrentExtent;
    }
    else
    {
      var framebufferSize = window!.FramebufferSize;

      Extent2D actualExtent = new()
      {
        Width = (uint)framebufferSize.X,
        Height = (uint)framebufferSize.Y
      };

      actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
      actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

      return actualExtent;
    }
  }

  private void CreateSwapChain()
  {
    var swapChainSupport = QuerySwapChainSupport(physicalDevice);

    var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    var presentMode = ChoosePresentMode(swapChainSupport.PresentModes);
    var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

    var imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
    if (swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.Capabilities.MaxImageCount)
    {
      imageCount = swapChainSupport.Capabilities.MaxImageCount;
    }

    SwapchainCreateInfoKHR creatInfo = new()
    {
      SType = StructureType.SwapchainCreateInfoKhr,
      Surface = surface,

      MinImageCount = imageCount,
      ImageFormat = surfaceFormat.Format,
      ImageColorSpace = surfaceFormat.ColorSpace,
      ImageExtent = extent,
      ImageArrayLayers = 1,
      ImageUsage = ImageUsageFlags.ColorAttachmentBit,
    };

    var indices = FindQueueFamilies(physicalDevice);
    var queueFamilyIndices = stackalloc[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

    if (indices.GraphicsFamily != indices.PresentFamily)
    {
      creatInfo = creatInfo with
      {
        ImageSharingMode = SharingMode.Concurrent,
        QueueFamilyIndexCount = 2,
        PQueueFamilyIndices = queueFamilyIndices,
      };
    }
    else
    {
      creatInfo.ImageSharingMode = SharingMode.Exclusive;
    }

    creatInfo = creatInfo with
    {
      PreTransform = swapChainSupport.Capabilities.CurrentTransform,
      CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
      PresentMode = presentMode,
      Clipped = true,

      OldSwapchain = default
    };

    if (!vk!.TryGetDeviceExtension(instance, device, out khrSwapChain))
    {
      throw new NotSupportedException("VK_KHR_swapchain extension not found.");
    }

    if (khrSwapChain!.CreateSwapchain(device, creatInfo, null, out swapChain) != Result.Success)
    {
      throw new Exception("failed to create swap chain!");
    }

    khrSwapChain.GetSwapchainImages(device, swapChain, ref imageCount, null);
    swapChainImages = new Image[imageCount];
    fixed (Image* swapChainImagesPtr = swapChainImages)
    {
      khrSwapChain.GetSwapchainImages(device, swapChain, ref imageCount, swapChainImagesPtr);
    }

    swapChainImageFormat = surfaceFormat.Format;
    swapChainExtent = extent;
  }
}