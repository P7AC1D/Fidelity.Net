using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;

namespace Fidelity;

struct QueueFamilyIndices
{
  public uint? GraphicsFamily { get; set; }
  public bool IsComplete()
  {
    return GraphicsFamily.HasValue;
  }
}

public unsafe class Application : IApplication, IDisposable
{
  private readonly IWindow window;
  private Vk vk;
  private Instance instance;

  private ExtDebugUtils? debugUtils;
  private DebugUtilsMessengerEXT debugMessenger;

  private PhysicalDevice physicalDevice;

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
  }

  public void Update(double dt)
  {

  }

  public void Dispose()
  {
    if (EnableValidationLayers)
    {
      debugUtils?.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
    }

    vk?.DestroyInstance(instance, null);
    vk?.Dispose();

    window?.Dispose();
  }

  private void InitVulkan()
  {
    CreateInstance();
    SetupDebugMessenger();
    PickPhysicalDevice();
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