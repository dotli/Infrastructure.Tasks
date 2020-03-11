using System;
using System.Collections.Concurrent;

namespace Infrastructure.Tasks.Tests
{
  /// <summary>
  /// ConcreteData
  /// </summary>
  internal static class ConcreteData
  {
    // 模拟任务来自内存队列，实际任务数据可以是来自数据库。
    private static readonly ConcurrentQueue<ConcreteTask> tasks = new ConcurrentQueue<ConcreteTask>();

    public static int AddTasks(int num)
    {
      for (var i = 0; i < num; i++)
      {
        tasks.Enqueue(new ConcreteTask()
        {
          Id = i.ToString(),
          Name = "ConcreteTask(" + i.ToString() + ")",
          CreateTime = DateTime.Now,
        });
      }

      Console.WriteLine($"{DateTime.Now.ToLongTimeString()}\tConcreteData Append {num} new tasks.");
      return tasks.Count;
    }

    public static bool TryDequeue(out ConcreteTask task)
    {
      return tasks.TryDequeue(out task);
    }
  }
}
