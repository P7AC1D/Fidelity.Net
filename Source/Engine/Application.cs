using Fidelity.Rendering.Enums;
using Fidelity.Rendering.Extensions;
using Fidelity.Rendering.Models;
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
using CommandBuffer = Fidelity.Rendering.Resources.CommandBuffer;
using CommandPool = Fidelity.Rendering.Resources.CommandPool;
using Framebuffer = Fidelity.Rendering.Resources.Framebuffer;
using ImageView = Fidelity.Rendering.Resources.ImageView;
using Image = Fidelity.Rendering.Resources.Image;
using Fidelity.Rendering;

namespace Fidelity;

public class Application
{
  private readonly IWindow window;
  private readonly Renderer renderer;

  public Application()
  {
    var options = WindowOptions.DefaultVulkan;
    options.Size = new Vector2D<int>(800, 600);
    options.Title = "Vulkan using Silk.NET";

    window = Window.Create(options);
    window.Initialize();

    window.Load += Load;
    window.Update += Update;
    window.Render += DrawFrame;
    window.Resize += FramebufferResizeCallback;

    if (window.VkSurface is null)
    {
      throw new Exception("Windowing platform doesn't support Vulkan.");
    }

    renderer = new Renderer(window);
    renderer.Initialize();
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

  public void DrawFrame(double dt)
  {
    renderer.DrawFrame();
  }

  public void Run()
  {
    window.Run();
    window.Dispose();
  }

  public void Update(double dt)
  {

  }

  private void FramebufferResizeCallback(Vector2D<int> obj)
  {
    renderer.RecreateSwapChain();
  }
}