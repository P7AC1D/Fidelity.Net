using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class Fence(Device device) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Silk.NET.Vulkan.Fence fence;
  private bool isInitialized = false;

  public Silk.NET.Vulkan.Fence Get => fence;

  public Fence Create()
  {
    if (isInitialized)
    {
      throw new Exception("Fence is already initialised.");
    }

    FenceCreateInfo fenceCreateInfo = new FenceCreateInfo
    {
      SType = StructureType.FenceCreateInfo,
      Flags = FenceCreateFlags.SignaledBit,
    };

    if (vk!.CreateFence(device, &fenceCreateInfo, null, out fence) != Result.Success)
    {
      throw new Exception("Failed to create fence");
    }

    isInitialized = true;
    return this;
  }

  public void Wait(ulong maxDuration = ulong.MaxValue)
  {
    if (!isInitialized)
    {
      throw new Exception("Fence is not initialised.");
    }

    vk!.WaitForFences(device, 1, fence, true, maxDuration);
  }

  public void Reset()
  {
    if (!isInitialized)
    {
      throw new Exception("Fence is not initialised.");
    }

    vk!.ResetFences(device, 1, fence);
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
      vk!.DestroyFence(device, fence, null);
    }
  }
}