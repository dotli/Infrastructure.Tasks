using System;
using System.Threading.Tasks;

namespace Infrastructure.Tasks.Tests
{
  internal class ConcreteTaskThreadService : BackgroundTaskThreadService<ConcreteTask, ConcreteTaskThread>
  {
    public ConcreteTaskThreadService()
        : base(nameof(ConcreteTaskThreadService))
    {
      // 设置最大工作线程数。默认12倍当前计算机处理器数。
      AllowedThreadMax = 4;

      // 设置任务空闲时调度任务的频率（默认5分钟）
      TaskIdleTime = TimeSpan.FromSeconds(5);

      // 设置任务非空闲时调度任务的频率（默认1秒）
      TaskBusyTime = TimeSpan.FromMilliseconds(10);
    }

    public static void Run()
    {
      // 初始化具体服务类
      var concreteService = new ConcreteTaskThreadService();
      concreteService.ThreadStarted += ConreteService_ThreadStarted;
      concreteService.ThreadExited += ConreteService_ThreadExited;
      concreteService.TaskExecuting += ConreteService_TaskExecuting;
      concreteService.TaskExecuted += ConreteService_TaskExecuted;
      concreteService.ServiceStoped += ConreteService_ServiceStoped;

      // 开始启动服务。
      // 如果在Windows服务中可以在OnStart事件里面调用
      concreteService.Start();

      Logger.Trace("{0} Started.  AllowedThreadMax={1}, TaskIdleTime={2}ms, TaskBusyTime={3}ms.",
          concreteService.Name,
          concreteService.AllowedThreadMax.ToString(),
          concreteService.TaskIdleTime.TotalMilliseconds.ToString(),
          concreteService.TaskBusyTime.TotalMilliseconds.ToString());

      while (true)
      {
        ConsoleKeyInfo keyInfo = Console.ReadKey(false);
        if (keyInfo.Key == ConsoleKey.Escape)
        {
          // 停止服务。如果在Windows服务中可以在OnStop事件里面调用
          concreteService.Stop();
          concreteService.Dispose();
          break;
        }

        if (keyInfo.Key == ConsoleKey.A)
        {
          Logger.Trace("Input the task num:");
          string taskNumStr = Console.ReadLine();
          int.TryParse(taskNumStr, out int taskNum);
          if (taskNum > 0)
            taskNum = ConcreteData.AddTasks(taskNum);
          Logger.Trace("Add new {1} tasks.", taskNum.ToString());
        }
      }

      Console.WriteLine("Press any key to exit.");
      Console.Read();
    }

    static void ConreteService_ThreadStarted(object sender, TaskThreadStartedEventArgs e)
    {
      var thread = (ConcreteTaskThread)sender;
      Logger.Trace("{0} Started.", thread.Name);
    }

    static void ConreteService_TaskExecuting(object sender, TaskExecutingEventArgs<ConcreteTask> e)
    {
      var thread = (ConcreteTaskThread)sender;
      Logger.Trace("{0} Executing task({1})...", thread.Name, e.Task.Id.ToString());
    }

    static void ConreteService_TaskExecuted(object sender, TaskExecutedEventArgs<ConcreteTask> e)
    {
      var thread = (ConcreteTaskThread)sender;
      Logger.Trace("{0} Executed task({1}) with {2}ms.",
          thread.Name, e.Task.Id.ToString(), e.Milliseconds.ToString());
    }

    static void ConreteService_ThreadExited(object sender, TaskThreadExitedEventArgs e)
    {
      var thread = (ConcreteTaskThread)sender;
      Logger.Trace("{0} Exited.", thread.Name);
    }

    static void ConreteService_ServiceStoped(object sender, ServiceStopEventArgs e)
    {
      var service = (ConcreteTaskThreadService)sender;
      Logger.Trace("{0} Stoped with {1}.", service.Name, e.IsSafeExited ? "safely" : "unsafely");
    }
  }

  internal class ConcreteTaskThread : BackgroundTaskThread<ConcreteTask>
  {
    protected override void LogTrace(string message, params object[] args)
    {
      Logger.Trace(message, args);
    }

    protected override async Task<ConcreteTask> GetTaskAsync()
    {
      LogTrace("{0} GetTaskAsync...", Name);

      await Task.Delay(500);

      // 这里从内存任务队列获取目标任务，也可以是从数据库等其它地方获取。
      ConcreteData.TryDequeue(out ConcreteTask task);

      if (task == null)
        LogTrace("{0} Returned empty task.", Name);

      // 注意：
      // 如果不需要任务数据时，也请返回一个默认的BackgroundTask实例。
      // 因为，服务调度是根据这里返回的Task决定是否要执行ExecuteTask里面的逻辑。
      // 如：
      // return new BackgroundTask();
      await Task.CompletedTask;
      return task;
    }

    protected override Task ExecuteTaskAsync(ConcreteTask task)
    {
      return Task.Delay(1000);
    }
  }

  /// <summary>
  /// 定义后台多线程服务执行的具体任务数据对象。
  /// </summary>
  public class ConcreteTask : BackgroundTask
  {
    /// <summary>
    /// 任务创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
    /// <summary>
    /// 任务开始处理时间
    /// </summary>
    public DateTime? HandlingTime { get; set; }
    /// <summary>
    /// 任务处理完成时间
    /// </summary>
    public DateTime? CompletedTime { get; set; }
    /// <summary>
    /// 处理状态 0-未处理 1-处理中 2-处理成功 3-处理失败
    /// </summary> 
    /// <remarks>0-未处理 1-处理中 2-处理成功 3-处理失败</remarks>
    public int HandleState { get; set; }
    /// <summary>
    /// 备注
    /// </summary>
    public string Remark { get; set; }
  }
}
