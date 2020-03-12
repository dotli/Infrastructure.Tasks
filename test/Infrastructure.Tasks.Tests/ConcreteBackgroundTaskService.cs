using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tasks.Tests
{
  internal class ConcreteBackgroundTaskService : BackgroundTaskService<ConcreteTask>
  {
    public ConcreteBackgroundTaskService(string serviceName = nameof(ConcreteBackgroundTaskService)) : base(serviceName)
    {
      AllowedThreadMax = 2;
      TaskBusyTime = TimeSpan.FromMilliseconds(500);
      TaskIdleTime = TimeSpan.FromSeconds(1);
    }

    protected override void LogInfo(string message, params object[] args)
    {
      Logger.Trace(message, args);
    }

    protected override void LogTrace(string message, params object[] args)
    {
      Logger.Trace(message, args);
    }

    protected override async Task<ConcreteTask> GetTaskAsync()
    {
      await Task.Delay(1000);

      // 这里从内存任务队列获取目标任务，也可以是从数据库等其它地方获取。
      ConcreteData.TryDequeue(out ConcreteTask task);

      // 注意：
      // 如果不需要任务数据时，也请返回一个默认的 BackgroundTask 实例。
      // 因为，服务调度是根据这里返回的 Task 决定是否要执行 ExecuteTask 里面的逻辑。
      // 如：
      // return new BackgroundTask();
      return task;
    }

    protected override Task ExecuteTaskAsync(ConcreteTask task)
    {
      return Task.Delay(1000);
    }

    public static void Run()
    {
      // 初始化具体服务类
      var concreteService = new ConcreteBackgroundTaskService();

      concreteService.ThreadStatusChanged += (e) =>
      {
        switch (e.Status)
        {
          case ServiceThreadStatus.Initialized:
            Logger.Trace("  Thread-{0} GetTaskAsync.", Thread.CurrentThread.ManagedThreadId);
            break;
          case ServiceThreadStatus.Completed when e.Task != null:
            Logger.Trace("  Thread-{0} ExecuteTaskAsync(TaskId={1}) completed.",
              Thread.CurrentThread.ManagedThreadId, e.Task.Id);
            break;
          default:
            Logger.Trace("  Thread-{0} {1}.", Thread.CurrentThread.ManagedThreadId, e.Status.ToString());
            break;
        }
      };

      // 开始启动服务。
      // 如果在 Windows 服务中可以在 OnStart 事件里面调用
      concreteService.Start();

      while (true)
      {
        ConsoleKeyInfo keyInfo = Console.ReadKey(false);
        if (keyInfo.Key == ConsoleKey.Escape)
        {
          // 停止服务。如果在 Windows 服务中可以在 OnStop 事件里面调用
          concreteService.Stop();
          concreteService.Dispose();
          break;
        }

        if (keyInfo.Key == ConsoleKey.A)
        {
          Console.WriteLine("Input the task num:");
          string taskNumStr = Console.ReadLine();
          int.TryParse(taskNumStr, out int taskNum);
          if (taskNum > 0)
            taskNum = ConcreteData.AddTasks(taskNum);
          Console.WriteLine("{0}  Add new tasks:{1}",
              DateTime.Now.ToLongTimeString(), taskNum.ToString());
        }
      }

      Console.WriteLine("Press any key to exit.");
      Console.Read();
    }
  }
}
