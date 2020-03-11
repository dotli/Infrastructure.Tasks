namespace Infrastructure.Tasks.Tests
{
  class Program
  {
    static void Main(string[] args)
    {
      ConcreteData.AddTasks(4);

      //ConcreteTaskThreadService.Run();
      //ConcreteBackgroundTaskService.Run();
      AutoStopableBackgroundTaskService.Run();
    }
  }
}
