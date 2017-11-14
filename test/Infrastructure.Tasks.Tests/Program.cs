using System;
using System.Threading;

namespace Infrastructure.Tasks.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            ConcreteData.AddTasks(10);
            ConcreteTaskThreadService.Run();
        }
    }
}
