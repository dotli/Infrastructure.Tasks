// Copyright (c) lyson@outlook.com All rights reserved.

namespace Infrastructure.Tasks
{
  using System;
  using System.Diagnostics;
  using System.Threading;
  using System.Threading.Tasks;

  /// <summary>
  /// 提供某项任务需要后台多线程执行的基础功能。
  /// </summary>
  /// <remarks>
  /// 根据配置的最大工作线程数，动态创建工作线程（使用线程池）。
  /// </remarks>
  /// <typeparam name="TTask">定义后台多线程服务执行的具体任务数据对象类型。该对象必须继承自<see cref="BackgroundTask"/></typeparam>
  public abstract class BackgroundTaskService<TTask> : IDisposable
      where TTask : BackgroundTask
  {
    #region properties
    /// <summary>
    /// 资源释放标志。
    /// </summary>
    private bool disposed = false;
    /// <summary>
    /// 服务运行状态
    /// </summary>
    private volatile bool Running = false;
    /// <summary>
    /// 目标任务调度主线程控制信号量。
    /// </summary>
    private AutoResetEvent TaskDispatchResetEvent;
    /// <summary>
    /// 服务安全退出等待信号量。
    /// </summary>
    private AutoResetEvent ServiceSafeExitResetEvent;
    /// <summary>
    /// 获取或设置服务名称
    /// </summary>
    public string ServiceName { get; protected set; }
    /// <summary>
    /// 表示是否启用服务。
    /// </summary>
    /// <remarks>
    /// 当多服务共存时可以根据配置来决定是否启用服务。
    /// 当ServiceEnabled为true时表示启用服务，为false表示禁用服务。
    /// </remarks>
    public bool ServiceEnabled { get; set; } = true;
    /// <summary>
    /// 获取或设置任务空闲时调度任务的频率（默认5分钟）。
    /// </summary>
    public TimeSpan TaskIdleTime { get; protected set; } = TimeSpan.FromMinutes(5);
    /// <summary>
    /// 获取或设置任务非空闲时调度任务的频率。（默认1秒）。
    /// </summary>
    public TimeSpan TaskBusyTime { get; protected set; } = TimeSpan.FromSeconds(1);
    /// <summary>
    /// 目标任务执行最大工作线程数。
    /// </summary>
    private int allowedThreadMax = 1;
    /// <summary>
    /// 获取或设置最大工作线程数。默认12倍当前计算机处理器数。
    /// </summary>
    public int AllowedThreadMax
    {
      get
      {
        return allowedThreadMax;
      }
      protected set
      {
        if (value > 0)
        {
          allowedThreadMax = value;
        }
      }
    }
    /// <summary>
    /// 获取当前工作线程数
    /// </summary>
    private volatile int currentThreadCount = 0;
    /// <summary>
    /// 获取当前工作线程数。
    /// </summary>
    public int CurrentThreadCount
    {
      get { return currentThreadCount; }
    }
    /// <summary>
    /// 该值将告诉任务调度主线程如何设置调度任务的频率。
    /// </summary>
    public bool IsTaskRapid { get; private set; }
    /// <summary>
    /// 服务主线程安全退出前等待的超时时间。单位：秒，默认值为180。
    /// </summary>
    private int waitExitTimeout = 180;
    /// <summary>
    /// 获取或设置服务主线程安全退出前等待的超时时间。单位：秒，默认值为180。
    /// 取值为大于或等于0的整数。设置为0表示无限期等待，直到所有工作线程退出。
    /// </summary>
    public int WaitExitTimeout
    {
      get
      {
        return waitExitTimeout;
      }
      protected set
      {
        if (value < 0)
        {
          waitExitTimeout = 0;
        }
        else
        {
          waitExitTimeout = value;
        }
      }
    }
    /// <summary>
    /// Gets a unique identifier for the current managed thread.
    /// </summary>
    protected int CurrentThreadId
    {
      get { return Thread.CurrentThread.ManagedThreadId; }
    }
    #endregion

    #region events

    /// <summary>
    /// 服务线程增减时触发该事件。
    /// </summary>
    public event Action<int> ThreadChanged;

    /// <summary>
    /// 服务逻辑执行线程异常时触发该事件。
    /// </summary>
    public event Action<Exception> ThreadErrored;

    /// <summary>
    /// 服务运行完毕时触发该事件。
    /// </summary>
    public event Action ServiceCompleted;

    #endregion

    #region constructors

    /// <summary>
    /// 构造函数，以指定的服务名称初始化 BackgroundTaskService 类的新实例。
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <exception cref="ArgumentNullException">当serviceName为null或System.String.Empty时引发该异常。</exception>
    protected BackgroundTaskService(string serviceName)
    {
      if (string.IsNullOrWhiteSpace(serviceName))
      {
        throw new ArgumentNullException(nameof(serviceName));
      }

      ServiceName = serviceName;
      AllowedThreadMax = Environment.ProcessorCount * 12;
    }

    #endregion

    #region methods

    /// <summary>
    /// 使当前服务运行线程数加1
    /// </summary>
    /// <returns></returns>
    private void IncrementWorkThread()
    {
      LogTrace("Thread-{0} Increment WorkThread...", CurrentThreadId);
      Interlocked.Increment(ref currentThreadCount);
      ThreadChanged?.Invoke(CurrentThreadCount);
    }

    /// <summary>
    /// 使当前服务运行线程数减1。如果当前服务运行线程数小于1则不做任何操作。
    /// </summary>
    /// <returns></returns>
    private void DecrementWorkThread()
    {
      if (CurrentThreadCount < 1) { return; }

      LogTrace("Thread-{0} Decrement WorkThread...", CurrentThreadId);
      Interlocked.Decrement(ref currentThreadCount);
      ThreadChanged?.Invoke(CurrentThreadCount);
    }

    /// <summary>
    /// 通知服务逻辑执行发生异常
    /// </summary>
    /// <param name="exp"></param>
    protected virtual void OnThreadError(Exception exp)
    {
      ThreadErrored?.Invoke(exp);
    }

    /// <summary>
    /// 通知服务运行完毕
    /// </summary>
    protected virtual void OnServiceCompleted()
    {
      ServiceCompleted?.Invoke();
    }

    /// <summary>
    /// 该方法负责获取后台服务执行的目标任务。
    /// 实现子类需自行保证多线程环境下的线程同步。
    /// </summary>
    /// <remarks>如果服务不需要具体任务数据对象，可以返回<see cref="BackgroundTask"/>对象的一个实例。
    /// <example>return new BackgroundTask();</example>
    /// </remarks>
    /// <returns>返回<see cref="TTask"/>类型的任务。</returns>
    protected abstract Task<TTask> GetTaskAsync();

    /// <summary>
    /// 目标任务执行主逻辑。
    /// </summary>
    /// <param name="task"><see cref="TTask"/>类型的任务。</param>
    protected abstract Task ExecuteTaskAsync(TTask task);

    /// <summary>
    /// 服务主线程任务调度
    /// </summary>
    private void DispatchTasks()
    {
      TimeSpan dispatchTimeout;

      do
      {
        // 如果有目标任务需要处理
        // 加快调度频率
        dispatchTimeout = IsTaskRapid ? TaskBusyTime : TaskIdleTime;

        if (CurrentThreadCount >= AllowedThreadMax)
        {
          // 工作线程已满,等待其它线程退出
          dispatchTimeout = TimeSpan.FromMilliseconds(500);
          continue;
        }

        LogTrace("Thread-{0} Dispatching...", CurrentThreadId);

        // 使当前工作线程数加1
        IncrementWorkThread();

        Task.Run(async () =>
        {
          LogTrace("Thread-{0} Created and ready to work...", CurrentThreadId);

          // 获取目标任务
          TTask task = await GetTaskAsync();

          if (null == task)
          {
            // 暂时没有需要处理的数据
            IsTaskRapid = false;
            return;
          }

          // 有需要处理的数据
          IsTaskRapid = true;
          await ExecuteTaskAsync(task);

        })
        // 任务执行完毕后需要一些操作
        .ContinueWith((t) =>
        {
          try
          {
            if (t.IsFaulted)
            {
              // 通知任务执行异常
              Exception exp = t.Exception.InnerException ?? t.Exception;
              OnThreadError(exp);
            }
          }
          finally
          {
            // 执行任务完成后，使当前工作线程数减1
            DecrementWorkThread();
            ProcessServiceExitWaitHandle();

            LogTrace("Thread-{0} Exited with {1} left thread.", CurrentThreadId, CurrentThreadCount);
          }

        });

      } while (TaskDispatchResetEvent != null && TaskDispatchResetEvent.WaitOne(dispatchTimeout) == false);

      ProcessServiceExitWaitHandle();
      LogTrace("Thread-{0} Dispatch exited.", CurrentThreadId);
    }

    /// <summary>
    /// 如果当前工作线程数为0时，通知可以安全退出
    /// </summary>
    private void ProcessServiceExitWaitHandle()
    {
      if (CurrentThreadCount == 0 && ServiceSafeExitResetEvent != null)
      {
        LogTrace("Thread-{0} Set ServiceSafeExitResetEvent.", CurrentThreadId);
        ServiceSafeExitResetEvent.Set();
      }
    }

    /// <summary>
    /// 开始服务。
    /// </summary>
    /// <remarks>
    /// 当ServerEnabled为true时，才能启动服务。
    /// 如果服务已经为启动状态，该方法不做任何操作。
    /// </remarks>
    /// <exception cref="ObjectDisposedException"></exception>
    public void Start()
    {
      if (disposed)
      {
        throw new ObjectDisposedException(GetType().Name);
      }

      if (!ServiceEnabled)
      {
        LogTrace("由于服务 {0} 被禁用，调用Start方法启动失败!", ServiceName);
        return;
      }

      if (Running)
      {
        return;
      }

      lock (this)
      {
        if (!Running)
        {
          Running = true;

          TaskDispatchResetEvent = new AutoResetEvent(false);
          ServiceSafeExitResetEvent = new AutoResetEvent(false);

          LogTrace("Thread-{0} Start Dispatching.", CurrentThreadId);

          Task.Factory.StartNew(DispatchTasks, TaskCreationOptions.LongRunning)
              .ContinueWith((t) =>
              {
                // 服务运行结束
                // 释放资源
                if (TaskDispatchResetEvent != null)
                {
                  TaskDispatchResetEvent.Dispose();
                  TaskDispatchResetEvent = null;

                  LogTrace("Thread-{0} Disposed TaskDispatchResetEvent.", CurrentThreadId);
                }

                LogTrace("Thread-{0} Exited.", CurrentThreadId);

              });

          LogTrace("Thread-{0} Dispatched.", CurrentThreadId);

          LogInfo("{0} Started.  AllowedThreadMax={1}, TaskIdleTime={2}ms, TaskBusyTime={3}ms",
              ServiceName,
              AllowedThreadMax.ToString(),
              TaskIdleTime.TotalMilliseconds.ToString(),
              TaskBusyTime.TotalMilliseconds.ToString());
        }
      }
    }

    /// <summary>
    /// 停止服务。
    /// </summary>
    /// <remarks>
    /// 如果服务已经为停止状态，该方法不做任何操作。
    /// 如果在调用Stop方法停止服务时还有运行中的任务，服务主线程会等待WaitExitMinute指定的超时分钟数。
    /// </remarks>
    public void Stop()
    {
      LogTrace("Thread-{0} Requests to stop {1}...", CurrentThreadId, ServiceName);

      if (disposed)
      {
        throw new ObjectDisposedException(GetType().Name);
      }

      if (!Running)
      {
        LogTrace("Thread-{0} {1} Already stoped.", CurrentThreadId, ServiceName);
        return;
      }

      lock (this)
      {
        LogTrace("Thread-{0} Get stop lock.", CurrentThreadId);

        if (!Running)
        {
          LogTrace("Thread-{0} {1} Already stoped.", CurrentThreadId, ServiceName);
          return;
        }

        Running = false;
      }

      LogTrace("Thread-{0} {1} Stopping...", CurrentThreadId, ServiceName);

      if (TaskDispatchResetEvent != null)
      {
        // 通知服务调度不再继续新的工作线程
        TaskDispatchResetEvent.Set();
        LogTrace("Thread-{0} Set TaskDispatchResetEvent.", CurrentThreadId);
      }

      LogTrace("Thread-{0} CurrentThreadCount = {1}.", CurrentThreadId, CurrentThreadCount);

      if (CurrentThreadCount > 0
          && ServiceSafeExitResetEvent != null)
      {
        // 当有正在工作的线程存在，等待工作线程安全退出
        // 但在等待指定超时时间后，工作线程还未退出，服务会强制结束。
        TimeSpan timeout = WaitExitTimeout > 0 ?
            TimeSpan.FromSeconds(WaitExitTimeout) :
            TimeSpan.FromMilliseconds(-1);

        LogTrace("Thread-{0} ServiceSafeExitResetEvent.WaitOne({1}s).", CurrentThreadId, timeout.TotalSeconds);

        ServiceSafeExitResetEvent.WaitOne(timeout);
        ServiceSafeExitResetEvent.Dispose();
        ServiceSafeExitResetEvent = null;

        LogTrace("Thread-{0} Disposed ServiceSafeExitResetEvent.", CurrentThreadId);
      }

      // 通知服务运行完毕
      OnServiceCompleted();
      LogInfo("{0} Stoped.", ServiceName);
    }

    #endregion

    #region Loging

    /// <summary>
    /// 记录异常文本日志
    /// </summary>
    /// <param name="exp"></param>
    protected virtual void LogException(Exception exp)
    {
      Debug.Fail(exp.Message, exp.StackTrace);
    }

    /// <summary>
    /// 记录消息文本日志
    /// </summary>
    /// <param name="message"></param>
    protected virtual void LogInfo(string message, params object[] args)
    {
      Debug.WriteLine(string.Format(message, args));
    }

    /// <summary>
    /// 记录服务跟踪日志
    /// </summary>
    /// <param name="message"></param>
    protected virtual void LogTrace(string message, params object[] args)
    {
      Debug.WriteLine(string.Format(message, args));
    }

    #endregion

    #region Dispose & Finalize
    /// <summary>
    /// 释放由BackgroundTaskService类的当前实例占用的所有资源。
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
    /// <summary>
    /// 释放BackgroundTaskService。同时释放所有非托管资源。
    /// </summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
      if (!disposed)
      {
        if (disposing)
        {
          if (Running)
          {
            Stop();
          }
        }

        disposed = true;
      }
    }
    /// <summary>
    /// 析构函数。
    /// </summary>
    ~BackgroundTaskService()
    {
      Dispose(false);
    }

    #endregion
  }
}
