using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
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