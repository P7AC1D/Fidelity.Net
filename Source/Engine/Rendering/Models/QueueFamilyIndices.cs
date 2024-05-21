namespace Fidelity.Rendering.Models;

public struct QueueFamilyIndices
{
  public uint? GraphicsFamily { get; set; }
  public uint? PresentFamily { get; set; }

  public readonly bool IsComplete()
  {
    return GraphicsFamily.HasValue && PresentFamily.HasValue;
  }
}