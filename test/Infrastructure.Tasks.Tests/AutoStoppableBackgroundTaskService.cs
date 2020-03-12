using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tasks.Tests
{
  internal class AutoStopableBackgroundTaskService : ConcreteBackgroundTaskService
  {
    public AutoStopableBackgroundTaskService()
      : base(nameof(AutoStopableBackgroundTaskService))
    {
    }

    /// <summary>
    /// The main thread.
    /// </summary>
    public static new void Run()
    {
      var serviceWaitHandle = new AutoResetEvent(false);

      // 初始化具体服务类
      var concreteService = new AutoStopableBackgroundTaskService();

      concreteService.ThreadChanged += (currentThreadCount) =>
      {
        Logger.Trace("Thread-{0} Reported CurrentThreadCount = {1}.",
            Thread.CurrentThread.ManagedThreadId, currentThreadCount);

        if (currentThreadCount == 0)
        {
          // Review: 
          // 增加线程由 DispatchThread 触发
          // 减少线程由 WorkThread 触发, 最后一个线程会因为 conreteService.Stop() 阻塞
          //    导致最后一个线程比  conreteService 晚结束。
          Task.Run(() =>
          {
            concreteService.Stop();
            serviceWaitHandle.Set();
          });
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

      Logger.Trace("{0} Exited.", concreteService.ServiceName);

      Console.WriteLine("Press any key to exit.");
      Console.Read();
    }
  }
}
