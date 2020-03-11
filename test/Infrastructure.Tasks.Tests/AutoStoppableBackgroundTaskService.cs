using System;
using System.Threading;

namespace Infrastructure.Tasks.Tests
{
  internal class AutoStopableBackgroundTaskService : ConcreteBackgroundTaskService
  {
    public AutoStopableBackgroundTaskService()
      : base(nameof(AutoStopableBackgroundTaskService))
    {
      WaitExitTimeout = 10;
    }

    public static new void Run()
    {
      var serviceWaitHandle = new AutoResetEvent(false);

      // 初始化具体服务类
      var conreteService = new AutoStopableBackgroundTaskService();

      conreteService.ThreadChanged += (currentThreadCount) =>
      {
        conreteService.LogTrace("{0}\t    WorkThread-{1} Report CurrentThreadCount = {2}.",
            DateTime.Now.ToLongTimeString(), Thread.CurrentThread.ManagedThreadId, currentThreadCount);

        if (currentThreadCount == 0)
        {
          conreteService.Stop();
          serviceWaitHandle.Set();
        }
      };

      conreteService.ServiceCompleted += new Action(() =>
      {
        conreteService.LogTrace("{0}\t{1} Complete and stoped.",
            DateTime.Now.ToLongTimeString(),
            conreteService.ServiceName);
      });

      // 开始启动服务。
      // 如果在Windows服务中可以在OnStart事件里面调用
      conreteService.Start();

      conreteService.LogTrace("{0}\t{1} Started\r\n\tAllowedThreadMax={2}, TaskIdleTime={3}ms, TaskBusyTime={4}ms",
          DateTime.Now.ToLongTimeString(),
          conreteService.ServiceName,
          conreteService.AllowedThreadMax.ToString(),
          conreteService.TaskIdleTime.TotalMilliseconds.ToString(),
          conreteService.TaskBusyTime.TotalMilliseconds.ToString());

      serviceWaitHandle.WaitOne();

      conreteService.LogTrace("{0}\t{1} Exited.",
          DateTime.Now.ToLongTimeString(),
          conreteService.ServiceName);

      Console.Read();
    }
  }
}
