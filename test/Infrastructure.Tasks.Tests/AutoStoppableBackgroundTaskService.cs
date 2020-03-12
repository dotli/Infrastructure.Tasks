using System;
using System.Threading;

namespace Infrastructure.Tasks.Tests
{
  internal class AutoStopableBackgroundTaskService : ConcreteBackgroundTaskService
  {
    public AutoStopableBackgroundTaskService()
      : base(nameof(AutoStopableBackgroundTaskService))
    {
      AllowedThreadMax = 10;
      TaskBusyTime = TimeSpan.FromMilliseconds(50);
      TaskIdleTime = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// The main thread.
    /// </summary>
    public static new void Run()
    {
      var serviceWaitHandle = new AutoResetEvent(false);

      // 初始化具体服务类
      var concreteService = new AutoStopableBackgroundTaskService();

      concreteService.ThreadStatusChanged += (e) =>
      {
        switch (e.Status)
        {
          case ServiceThreadStatus.Initialized:
            Logger.Trace("  Thread-{0} GetTaskAsync.", Thread.CurrentThread.ManagedThreadId);
            break;
          case ServiceThreadStatus.Completed when e.Task != null:
            Logger.Trace("  Thread-{0} ExecuteTaskAsync(TaskId={1}) completed.", Thread.CurrentThread.ManagedThreadId, e.Task.Id);
            break;
          default:
            Logger.Trace("  Thread-{0} {1}.", Thread.CurrentThread.ManagedThreadId, e.Status.ToString());
            break;
        }

        if (e.Status == ServiceThreadStatus.NoTask)
        {
          serviceWaitHandle.Set();
        }
      };

      concreteService.ServiceCompleted += new Action(() =>
      {
        Logger.Trace("{0} Completed.", concreteService.ServiceName);
      });

      // 开始启动服务。
      // 如果在Windows服务中可以在OnStart事件里面调用
      concreteService.Start();

      // wait tasks complete.
      // serviceWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
      serviceWaitHandle.WaitOne();

      concreteService.Stop();

      Logger.Trace("{0} Exited.", concreteService.ServiceName);

      Console.WriteLine("Press any key to exit.");
      Console.Read();
    }
  }
}
