namespace Infrastructure.Tasks.Tests
{
  class Program
  {
    static void Main(string[] args)
    {
      ConcreteData.AddTasks(10);

      // Scenarios for using BackgroundTaskThreadService & BackgroundTaskThread.
      //ConcreteTaskThreadService.Run();

      // Do some long-running task in background using BackgroundTaskService.
      //ConcreteBackgroundTaskService.Run();

      // Asynchronous execution of batch tasks using BackgroundTaskService in the main thread.
      AutoStopableBackgroundTaskService.Run();
    }
  }
}
