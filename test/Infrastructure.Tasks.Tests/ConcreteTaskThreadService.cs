using System;
using System.Threading.Tasks;

namespace Infrastructure.Tasks.Tests
{
  internal class ConcreteTaskThreadService : BackgroundTaskThreadService<ConcreteTask, ConcreteTaskThread>
  {
    public ConcreteTaskThreadService()
        : base("ConreteTaskThreadService")
    {
      // 设置最大工作线程数。默认12倍当前计算机处理器数。
      ThreadCount = 2;

      // 设置任务空闲时调度任务的频率（默认5分钟）
      TaskIdleTime = TimeSpan.FromSeconds(5);

      // 设置任务非空闲时调度任务的频率（默认1秒）
      TaskBusyTime = TimeSpan.FromMilliseconds(10);

      Console.WriteLine("{0}\tConreteTaskThreadService Initialized:\r\n\t\tThreadCount={1},TaskIdleTime={2}ms,TaskBusyTime={3}ms",
          DateTime.Now.ToLongTimeString(),
          ThreadCount.ToString(),
          TaskIdleTime.TotalMilliseconds.ToString(),
          TaskBusyTime.Milliseconds.ToString());
    }

    public static void Run()
    {
      //初始化具体服务类
      var conreteService = new ConcreteTaskThreadService();
      conreteService.ThreadStarted += ConreteService_ThreadStarted;
      conreteService.ThreadExited += ConreteService_ThreadExited;
      conreteService.TaskExecuting += ConreteService_TaskExecuting;
      conreteService.TaskExecuted += ConreteService_TaskExecuted;
      conreteService.ServiceStoped += ConreteService_ServiceStoped;

      // 开始启动服务。
      // 如果在Windows服务中可以在OnStart事件里面调用
      conreteService.Start();

      Console.WriteLine("{0}\t{1} Started\r\n\t\tThreadCount={2},TaskIdleTime={3}ms,TaskBusyTime={4}ms",
          DateTime.Now.ToLongTimeString(),
          conreteService.Name,
          conreteService.ThreadCount.ToString(),
          conreteService.TaskIdleTime.TotalMilliseconds.ToString(),
          conreteService.TaskBusyTime.Milliseconds.ToString());

      while (true)
      {
        ConsoleKeyInfo keyInfo = Console.ReadKey(false);
        if (keyInfo.Key == ConsoleKey.Escape)
        {
          // 停止服务。如果在Windows服务中可以在OnStop事件里面调用
          conreteService.Stop();
          conreteService.Dispose();
          break;
        }

        if (keyInfo.Key == ConsoleKey.A)
        {
          Console.WriteLine("Input the task num:");
          string taskNumStr = Console.ReadLine();
          int.TryParse(taskNumStr, out int taskNum);
          if (taskNum > 0)
            taskNum = ConcreteData.AddTasks(taskNum);
          Console.WriteLine("{0}\tAdd new tasks:{1}",
              DateTime.Now.ToLongTimeString(), taskNum.ToString());
        }
      }


      Console.Read();
    }

    static void ConreteService_ThreadStarted(object sender, TaskThreadStartedEventArgs e)
    {
      var thread = (ConcreteTaskThread)sender;
      Console.WriteLine("{0}\t{1} started.",
          DateTime.Now.ToLongTimeString(), thread.Name);
    }

    static void ConreteService_TaskExecuting(object sender, TaskExecutingEventArgs<ConcreteTask> e)
    {
      var thread = (ConcreteTaskThread)sender;
      Console.WriteLine("{0}\t{1} executing task({2})...",
          DateTime.Now.ToLongTimeString(), thread.Name, e.Task.Id.ToString());
    }

    static void ConreteService_TaskExecuted(object sender, TaskExecutedEventArgs<ConcreteTask> e)
    {
      var thread = (ConcreteTaskThread)sender;
      Console.WriteLine("{0}\t{1} executed task({2}) with {3}ms.",
          DateTime.Now.ToLongTimeString(), thread.Name, e.Task.Id.ToString(), e.Milliseconds.ToString());
    }

    static void ConreteService_ThreadExited(object sender, TaskThreadExitedEventArgs e)
    {
      var thread = (ConcreteTaskThread)sender;
      Console.WriteLine("{0}\t{1} exited.",
          DateTime.Now.ToLongTimeString(), thread.Name);
    }

    static void ConreteService_ServiceStoped(object sender, ServiceStopEventArgs e)
    {
      var service = (ConcreteTaskThreadService)sender;
      Console.WriteLine("{0}\t{1} stoped with {2}.",
          DateTime.Now.ToLongTimeString(), service.Name, e.IsSafeExited ? "safely" : "unsafely");
    }
  }

  internal class ConcreteTaskThread : BackgroundTaskThread<ConcreteTask>
  {
    protected override async Task<ConcreteTask> GetTaskAsync()
    {
      // 这里从内存任务队列获取目标任务，也可以是从数据库等其它地方获取。
      ConcreteData.TryDequeue(out ConcreteTask task);

      if (task == null)
        Console.WriteLine("{0}\t{1} running with empty task.",
            DateTime.Now.ToLongTimeString(), Name);

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
      System.Threading.Thread.Sleep(1000);
      return Task.CompletedTask;
    }
  }

  /// <summary>
  /// 定义后台多线程服务执行的具体任务数据对象。
  /// </summary>
  [Serializable]
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
