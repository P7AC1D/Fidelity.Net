using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Fidelity;

struct QueueFamilyIndices
{
  public uint? GraphicsFamily { get; set; }
  public uint? PresentFamily { get; set; }

  public bool IsComplete()
  {
    return GraphicsFamily.HasValue && PresentFamily.HasValue;
  }
}

public unsafe class Application : IApplication, IDisposable
{
  private readonly IWindow window;
  private Vk vk;
  private Instance instance;

  private ExtDebugUtils? debugUtils;
  private DebugUtilsMessengerEXT debugMessenger;
  private KhrSurface? khrSurface;
  private SurfaceKHR surface;

  private PhysicalDevice physicalDevice;
  private Device device;

  private Queue graphicsQueue;
  private Queue presentQueue;

  private bool EnableValidationLayers = true;

  private readonly string[] validationLayers =
    [
        "VK_LAYER_KHRONOS_validation"
    ];

  public Application()
  {
    var options = WindowOptions.Default;
    options.Size = new Vector2D<int>(800, 600);
    options.Title = "Vulkan using Silk.NET";

    window = Window.Create(options);
    window.Initialize();

    window.Load += Load;
    window.Update += Update;
    window.Render += Render;
    window.FramebufferResize += ResizeFramebuffer;
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
    InitVulkan();
  }

  public void Render(double dt)
  {

  }

  public void ResizeFramebuffer(Vector2D<int> newSize)
  {

  }

  public void Run()
  {
    window.Run();
    CleanUp();
  }

  public void Update(double dt)
  {

  }

  public void Dispose()
  {
    CleanUp();
    window?.Dispose();
  }

  private void CleanUp()
  {
    vk?.DestroyDevice(device, null);

    if (EnableValidationLayers)
    {
      debugUtils?.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
    }

    khrSurface!.DestroySurface(instance, surface, null);
    vk?.DestroyInstance(instance, null);
    vk?.Dispose();

  }

  private void InitVulkan()
  {
    CreateInstance();
    SetupDebugMessenger();
    CreateSurface();
    PickPhysicalDevice();
    CreateLogicalDevice();
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

    return indices.IsComplete();
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

    PhysicalDeviceFeatures deviceFeatures = new();

    DeviceCreateInfo createInfo = new()
    {
      SType = StructureType.DeviceCreateInfo,
      QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
      PQueueCreateInfos = queueCreateInfos,

      PEnabledFeatures = &deviceFeatures,

      EnabledExtensionCount = 0
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
}