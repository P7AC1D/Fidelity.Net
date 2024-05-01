﻿using Fidelity;

try
{
  using var application = new Application();
  application.Run();
}
catch (Exception ex)
{
  Console.WriteLine($"Application runtime error has occurred: {ex.Message}");
}