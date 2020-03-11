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
      TaskBusyTime = TimeSpan.FromMilliseconds(50);
      TaskIdleTime = TimeSpan.FromSeconds(1);
    }

    protected override async Task<ConcreteTask> GetTaskAsync()
    {
      LogTrace("{0}\t    WorkThread-{1} GetTaskAsync.",
          DateTime.Now.ToLongTimeString(), Thread.CurrentThread.ManagedThreadId);

      Thread.Sleep(500);

      // 这里从内存任务队列获取目标任务，也可以是从数据库等其它地方获取。
      ConcreteData.TryDequeue(out ConcreteTask task);

      if (task == null)
        LogTrace("{0}\t    WorkThread-{1} Returned empty task.",
            DateTime.Now.ToLongTimeString(), Thread.CurrentThread.ManagedThreadId);

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
      LogTrace("{0}\t    WorkThread-{1} ExecuteTaskAsync(TaskId={2}).",
          DateTime.Now.ToLongTimeString(), Thread.CurrentThread.ManagedThreadId, task.Id);

      Thread.Sleep(1000);
      return Task.CompletedTask;
    }

    public static void Run()
    {
      // 初始化具体服务类
      var conreteService = new ConcreteBackgroundTaskService();

      conreteService.ThreadChanged += (currentThreadCount) =>
      {
        conreteService.LogTrace("{0}\tThread-{1} Reported CurrentThreadCount = {2}.",
            DateTime.Now.ToLongTimeString(), Thread.CurrentThread.ManagedThreadId, currentThreadCount);
      };

      // 开始启动服务。
      // 如果在Windows服务中可以在OnStart事件里面调用
      conreteService.Start();

      conreteService.LogTrace("{0}\t{1} Started\r\n\t\tAllowedThreadMax={2}, TaskIdleTime={3}ms, TaskBusyTime={4}ms",
          DateTime.Now.ToLongTimeString(),
          conreteService.ServiceName,
          conreteService.AllowedThreadMax.ToString(),
          conreteService.TaskIdleTime.TotalMilliseconds.ToString(),
          conreteService.TaskBusyTime.TotalMilliseconds.ToString());

      Console.Read();
    }
  }
}
