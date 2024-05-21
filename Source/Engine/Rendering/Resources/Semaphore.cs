using Silk.NET.Vulkan;

namespace Fidelity.Rendering.Resources;

public unsafe class Semaphore(Device device) : IDisposable
{
  private readonly Vk vk = Vk.GetApi();
  private Silk.NET.Vulkan.Semaphore semaphore;
  private bool isInitialized = false;

  public Silk.NET.Vulkan.Semaphore Get => semaphore;

  public Semaphore Create()
  {
    if (isInitialized)
    {
      throw new Exception("Semaphore is already initialised.");
    }

    SemaphoreCreateInfo semaphoreCreateInfo = new SemaphoreCreateInfo
    {
      SType = StructureType.SemaphoreCreateInfo
    };

    if (vk!.CreateSemaphore(device, &semaphoreCreateInfo, null, out semaphore) != Result.Success)
    {
      throw new Exception("Failed to create semaphore");
    }

    isInitialized = true;
    return this;
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
      vk!.DestroySemaphore(device, semaphore, null);
    }
  }
}