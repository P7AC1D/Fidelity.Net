using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Models;

public struct SwapChainSupportDetails
{
  public SurfaceCapabilitiesKHR Capabilities;
  public SurfaceFormatKHR[] Formats;
  public PresentModeKHR[] PresentModes;
}