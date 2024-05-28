using Fidelity.Rendering.Enums;
using Fidelity.Rendering.Extensions;
using Fidelity.Rendering.Models;
using Fidelity.Rendering.Resources;
using Silk.NET.Assimp;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommandBuffer = Fidelity.Rendering.Resources.CommandBuffer;
using CommandPool = Fidelity.Rendering.Resources.CommandPool;
using Framebuffer = Fidelity.Rendering.Resources.Framebuffer;
using Image = Fidelity.Rendering.Resources.Image;

namespace Fidelity.Rendering;

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

public unsafe class Renderer(IWindow window)
{
  private Vk vk;
  private Instance instance;

  private ExtDebugUtils? debugUtils;
  private DebugUtilsMessengerEXT debugMessenger;
  private Swapchain swapChain;
  private Surface surface;

  private PhysicalDevice physicalDevice;
  private SampleCountFlags msaaSamples = SampleCountFlags.Count1Bit;
  private Device device;

  private GraphicsQueue graphicsQueue;

  private bool EnableValidationLayers = true;
  private uint mipLevels;

  private Framebuffer[] swapChainFramebuffers;

  private GraphicsPipeline graphicsPipeline;
  private Resources.RenderPass graphicsPipelineRenderPass;

  private GpuBuffer vertexBuffer, indexBuffer;
  private GpuBuffer[] uniformBuffers;

  private DescriptorPool descriptorPool;
  private Resources.DescriptorSet[]? descriptorSets;
  private Resources.DescriptorSetLayout descriptorSetLayout;

  private Image texture;
  private Resources.Sampler textureSampler;

  private CommandPool commandPool;
  private CommandBuffer[] commandBuffers;

  private Resources.Semaphore[]? imageAvailableSemaphores;
  private Resources.Semaphore[]? renderFinishedSemaphores;
  private Resources.Fence[]? inFlightFences;
  private Resources.Fence[]? imagesInFlight;
  private int currentFrame = 0;

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

  public void Initialize()
  {
    CreateInstance();
    SetupDebugMessenger();
    CreateSurface();
    PickPhysicalDevice();
    CreateLogicalDevice();
    CreateSwapChain();
    CreateRenderPass();
    CreateCommandPool();
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

  public void DrawFrame()
  {
    uint imageIndex = PrepareFrame();

    UpdateUniformBuffer(imageIndex);

    graphicsQueue.Submit(
      commandBuffers![imageIndex]!,
      imageAvailableSemaphores![currentFrame],
      renderFinishedSemaphores![currentFrame],
      inFlightFences![currentFrame],
      PipelineStageFlags.ColorAttachmentOutputBit);

    EndFrame(imageIndex);
  }

  public void RecreateSwapChain()
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
    CreateRenderPass();
    CreateFramebuffers();
    CreateUniformBuffers();
    CreateDescriptorPool();
    CreateDescriptorSets();
    CreateGraphicsPipeline();
    CreateCommandBuffers();

    imagesInFlight = new Rendering.Resources.Fence[swapChain!.Length];
  }

  private uint PrepareFrame()
  {
    inFlightFences![currentFrame].Wait();

    if (!swapChain.GetNextImageIndex(imageAvailableSemaphores![currentFrame], out uint imageIndex))
    {
      RecreateSwapChain();
      return PrepareFrame();
    }

    if (imagesInFlight![imageIndex]?.Get != null)
    {
      imagesInFlight[imageIndex].Wait();
    }
    imagesInFlight[imageIndex] = inFlightFences[currentFrame];

    inFlightFences[currentFrame]!.Reset();
    return imageIndex;
  }

  private void EndFrame(uint imageIndex)
  {
    if (!swapChain.Present(renderFinishedSemaphores![currentFrame], imageIndex))
    {
      RecreateSwapChain();
    }

    currentFrame = (currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
  }

  private void CleanUpSwapChain()
  {
    foreach (var framebuffer in swapChainFramebuffers!)
    {
      framebuffer.Dispose();
    }

    commandPool.FreeCommandBuffers(commandBuffers);

    graphicsPipeline.Dispose();
    graphicsPipelineRenderPass.Dispose();

    swapChain!.DestroySwapchain();

    for (int i = 0; i < swapChain!.Length; i++)
    {
      uniformBuffers[i]?.Dispose();
    }

    vk!.DestroyDescriptorPool(device, descriptorPool, null);
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
      renderFinishedSemaphores![i]!.Dispose();
      imageAvailableSemaphores![i]!.Dispose();
      inFlightFences![i].Dispose();
    }

    commandPool.Dispose();

    vk!.DestroyDevice(device, null);

    if (EnableValidationLayers)
    {
      debugUtils!.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
    }

    surface.Dispose();
    vk!.DestroyInstance(instance, null);
    vk!.Dispose();
  }

  private void CreateSyncObjects()
  {
    imageAvailableSemaphores = new Resources.Semaphore[MAX_FRAMES_IN_FLIGHT];
    renderFinishedSemaphores = new Resources.Semaphore[MAX_FRAMES_IN_FLIGHT];
    inFlightFences = new Resources.Fence[MAX_FRAMES_IN_FLIGHT];
    imagesInFlight = new Resources.Fence[swapChain!.Length];

    for (var i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
    {
      imageAvailableSemaphores[i] = new Resources.Semaphore(device).Create();
      renderFinishedSemaphores[i] = new Resources.Semaphore(device).Create();
      inFlightFences[i] = new Resources.Fence(device).Create();
    }
  }

  private void CreateCommandPool()
  {
    commandPool = new CommandPool(device).Create(swapChain.FindGraphicsQueueFamilyIndex());
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

    texture = new Image(device, physicalDevice)
      .SetExtent((uint)img.Width, (uint)img.Height)
      .SetMipLevels(mipLevels)
      .SetFormat(Format.R8G8B8A8Srgb)
      .SetUsage(ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit)
      .SetMemoryProperties(MemoryPropertyFlags.DeviceLocalBit)
      .SetImageAspectFlags(ImageAspectFlags.ColorBit)
      .Allocate()
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
    textureSampler = new Rendering.Resources.Sampler(device, physicalDevice)
      .SetMipmapping(maxLod: mipLevels)
      .Allocate();
  }

  private void CreateVertexBuffer()
  {
    ulong bufferSize = (ulong)(Unsafe.SizeOf<Vertex>() * vertices!.Length);

    using GpuBuffer staging = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Staging, bufferSize)
      .WriteDataArray(vertices);

    vertexBuffer = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Vertex, bufferSize);

    staging.CopyData(vertexBuffer, bufferSize, commandPool, graphicsQueue);
  }

  private void CreateIndexBuffer()
  {
    ulong bufferSize = (ulong)(Unsafe.SizeOf<uint>() * indices!.Length);

    using GpuBuffer staging = new(device, physicalDevice);
    staging.Allocate(BufferType.Staging, bufferSize)
      .WriteDataArray(indices);

    indexBuffer = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Index, bufferSize);
    staging.CopyData(indexBuffer, bufferSize, commandPool, graphicsQueue);
  }

  private void CreateUniformBuffers()
  {
    ulong bufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();

    uniformBuffers = new GpuBuffer[swapChain!.Length];

    for (int i = 0; i < swapChain.Length; i++)
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
          DescriptorCount = swapChain!.Length,
      },
      new DescriptorPoolSize()
      {
          Type = DescriptorType.CombinedImageSampler,
          DescriptorCount = swapChain!.Length,
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
        MaxSets = swapChain!.Length,
      };

      if (vk!.CreateDescriptorPool(device, poolInfo, null, descriptorPoolPtr) != Result.Success)
      {
        throw new Exception("Failed to create descriptor pool.");
      }

    }
  }

  private void CreateDescriptorSetLayout()
  {
    descriptorSetLayout = new Resources.DescriptorSetLayout(device)
      .AddBinding(0, DescriptorType.UniformBuffer, 1, ShaderStageFlags.VertexBit)
      .AddBinding(1, DescriptorType.CombinedImageSampler, 1, ShaderStageFlags.FragmentBit)
      .Allocate();
  }

  private void CreateDescriptorSets()
  {
    descriptorSets = new Resources.DescriptorSet[swapChain!.Length];
    for (int i = 0; i < swapChain!.Length; i++)
    {
      descriptorSets[i] = new Resources.DescriptorSet(device, descriptorPool)
        .BindUniformBuffer(uniformBuffers![i], 0)
        .BindTexureSampler(texture, textureSampler, 1)
        .SetLayout(descriptorSetLayout)
        .Allocate()
        .Update();
    }
  }

  private void UpdateUniformBuffer(uint currentImage)
  {
    var time = (float)window!.Time;

    UniformBufferObject ubo = new()
    {
      model = Matrix4X4<float>.Identity * Matrix4X4.CreateFromAxisAngle<float>(new Vector3D<float>(0, 0, 1), time * Scalar.DegreesToRadians(90.0f)),
      view = Matrix4X4.CreateLookAt(new Vector3D<float>(2, 2, 2), new Vector3D<float>(0, 0, 0), new Vector3D<float>(0, 0, 1)),
      proj = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), (float)swapChain.Width / swapChain.Height, 0.1f, 10.0f),
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

    throw new Exception("Failed to find suitable memory type.");
  }

  private void CreateCommandBuffers()
  {
    commandBuffers = commandPool.AllocateCommandBuffers((uint)swapChainFramebuffers!.Length);

    for (int i = 0; i < commandBuffers.Length; i++)
    {
      commandBuffers[i]!
        .Begin()
        .BeginRenderPass(graphicsPipelineRenderPass, swapChainFramebuffers[i], swapChain!.Extent)
        .BindGraphicsPipeline(graphicsPipeline)
        .BindVertexBuffer(vertexBuffer)
        .BindIndexBuffer(indexBuffer)
        .BindDescriptorSet(graphicsPipeline, descriptorSets![i])
        .DrawIndexed((uint)indices!.Length)
        .EndRenderPass()
        .End();
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
      .SetViewport(swapChain.Width, swapChain.Height)
      .SetScissor(swapChain.Width, swapChain.Height)
      .SetRasterizationState()
      .SetMultisampleState(msaaSamples)
      .SetDepthStencilState()
      .SetDescriptorSetLayout(descriptorSetLayout)
      .SetRenderPass(graphicsPipelineRenderPass)
      .Allocate();
  }

  private void CreateRenderPass()
  {
    graphicsPipelineRenderPass = new Resources.RenderPass(device, physicalDevice)
      .AddColorAttachment(swapChain.Format, msaaSamples)
      .AddDepthAttachment(msaaSamples)
      .AddResolverAttachment(swapChain.Format)
      .Allocate();
  }

  private void CreateSurface()
  {
    surface = new Surface(instance, window).Create();
  }

  private void PickPhysicalDevice()
  {
    var devices = vk!.GetPhysicalDevices(instance);

    foreach (var device in devices)
    {
      if (device.IsDeviceSuitable(surface, deviceExtensions))
      {
        physicalDevice = device;
        msaaSamples = GetMaxUsableSampleCount();
        break;
      }
    }

    if (physicalDevice.Handle == 0)
    {
      throw new Exception("Failed to find a supported GPU.");
    }
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
      throw new Exception("Failed to create logical device.");
    }

    graphicsQueue = new GraphicsQueue(device).Retrieve(indices.GraphicsFamily!.Value);

    if (EnableValidationLayers)
    {
      SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
    }
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

      if (surface.DoesSurfaceSupportPresent(physicalDevice, i))
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
      throw new Exception("No supported validation layers found.");
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
      throw new Exception("Failed to create instance.");
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
    swapChainFramebuffers = new Framebuffer[swapChain!.Length];
    for (int i = 0; i < swapChain.Length; i++)
    {
      swapChainFramebuffers[i] = new Framebuffer(device)
        .AddAttachment(new Image(device, physicalDevice)
          .SetExtent(swapChain.Width, swapChain.Height)
          .SetMsaaSamples(msaaSamples)
          .SetFormat(swapChain.Format)
          .SetUsage(ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit)
          .SetMemoryProperties(MemoryPropertyFlags.DeviceLocalBit)
          .SetImageAspectFlags(ImageAspectFlags.ColorBit)
          .Allocate()
          .ImageView)
        .AddAttachment(new Image(device, physicalDevice)
          .SetExtent(swapChain.Width, swapChain.Height)
          .SetMsaaSamples(msaaSamples)
          .SetFormat(physicalDevice.FindSupportedFormat([Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint], ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit))
          .SetUsage(ImageUsageFlags.DepthStencilAttachmentBit)
          .SetMemoryProperties(MemoryPropertyFlags.DeviceLocalBit)
          .SetImageAspectFlags(ImageAspectFlags.DepthBit)
          .Allocate()
          .ImageView)
        .AddAttachment(swapChain.ImageViews![i])
        .SetRenderPass(graphicsPipelineRenderPass)
        .SetBoundary(swapChain.Width, swapChain.Height)
        .Allocate();
    }
  }

  private string[] GetRequiredExtensions()
  {
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

  private void CreateSwapChain()
  {
    swapChain = new Swapchain(instance, device, physicalDevice, window, surface).Create();
  }
}