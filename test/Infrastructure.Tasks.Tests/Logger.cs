using System;

namespace Infrastructure.Tasks.Tests
{
  internal class Logger
  {
    public static void Trace(string message, params object[] args)
    {
      Console.WriteLine("{0}  {1}", DateTime.Now.ToLongTimeString(), string.Format(message, args));
    }
  }
}
