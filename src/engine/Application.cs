using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;

namespace Fidelity;

public unsafe class Application : IApplication, IDisposable
{
  private readonly IWindow _window;
  private Vk _vk;
  private Instance _instance;

  public Application()
  {
    var options = WindowOptions.Default;
    options.Size = new Vector2D<int>(800, 600);
    options.Title = "Vulkan using Silk.NET";

    _window = Window.Create(options);
    _window.Initialize();

    _window.Load += Load;
    _window.Update += Update;
    _window.Render += Render;
    _window.FramebufferResize += ResizeFramebuffer;

    if (_window.VkSurface == null)
    {
      //throw new Exception("Windowing platform doesn't support Vulkan.");
    }
  }

  public void KeyDown(IKeyboard arg1, Key arg2, int arg3)
  {
    if (arg2 == Key.Escape)
    {
      _window.Close();
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
    _window.Run();
  }

  public void Update(double dt)
  {

  }

  public void Dispose()
  {
    _vk!.DestroyInstance(_instance, null);
    _vk!.Dispose();

    _window?.Dispose();
  }

  private void InitVulkan()
  {
    CreateVkInstance();
  }

  private void CreateVkInstance()
  {
    _vk = Vk.GetApi();

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

    var glfwExtensions = _window!.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);

    createInfo.EnabledExtensionCount = glfwExtensionCount;
    createInfo.PpEnabledExtensionNames = glfwExtensions;
    createInfo.EnabledLayerCount = 0;

    if (_vk.CreateInstance(ref createInfo, null, out _instance) != Result.Success)
    {
      throw new Exception("failed to create instance!");
    }

    Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
    Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
  }
}