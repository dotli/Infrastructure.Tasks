// Copyright (c) lyson@outlook.com All rights reserved.

namespace Infrastructure.Tasks
{
  using System;
  using System.Threading;

  /// <summary>
  /// 提供某项任务需要后台多线程执行的基础功能。
  /// </summary>
  /// <remarks>
  /// 根据配置的工作线程数，启动相应的工作线程。
  /// </remarks>
  /// <typeparam name="TThread">定义执行任务的具体工作线程类型。该类型必须继承自<see cref="BackgroundTaskThread{TTask}"/></typeparam>
  public class BackgroundTaskThreadService<TTask, TThread> : IDisposable
    where TTask : BackgroundTask
    where TThread : BackgroundTaskThread<TTask>, new()
  {
    #region constructors

    /// <summary>
    /// 构造函数，以指定的服务名称初始化<see cref="BackgroundTaskThreadService"/>
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <exception cref="ArgumentNullException">当serviceName为null或System.String.Empty时引发该异常。</exception>
    public BackgroundTaskThreadService(string serviceName)
    {
      if (string.IsNullOrWhiteSpace(serviceName))
      {
        throw new ArgumentNullException(nameof(serviceName));
      }

      Name = serviceName;
      ThreadCount = Environment.ProcessorCount * 12;
    }

    #endregion

    #region properties

    /// <summary>
    /// 资源释放标志。
    /// </summary>
    private bool disposed = false;
    /// <summary>
    /// 服务运行状态
    /// </summary>
    private volatile bool serviceRunning = false;
    /// <summary>
    /// 获取服务运行状态。
    /// </summary>
    public bool IsRunning { get { return serviceRunning; } }
    /// <summary>
    /// 获取或设置服务名称
    /// </summary>
    public string Name { get; protected set; }
    /// <summary>
    /// 表示是否启用服务。
    /// </summary>
    /// <remarks>
    /// 当多服务共存时可以根据配置来决定是否启用服务。
    /// 当ServiceEnabled为true时表示启用服务，为false表示禁用服务。
    /// </remarks>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// 工作线程数组。
    /// </summary>
    private TThread[] TaskThreads;
    /// <summary>
    /// 目标任务执行工作线程数。
    /// </summary>
    private int threadCount = 1;
    /// <summary>
    /// 获取或设置工作线程数。默认12倍当前计算机处理器数。
    /// </summary>
    public int ThreadCount
    {
      get
      {
        return threadCount;
      }
      set
      {
        if (value > 0)
        {
          threadCount = value;
        }
      }
    }
    /// <summary>
    /// 当前正在执行任务的工作线程数
    /// </summary>
    private int activeThreadCount = 0;
    /// <summary>
    /// 获取当前正在执行任务的工作线程数。
    /// </summary>
    public int ActiveThreadCount
    {
      get { return activeThreadCount; }
    }
    /// <summary>
    /// 获取或设置任务空闲时工作线程的休眠时间（默认5分钟）。
    /// </summary>
    public TimeSpan TaskIdleTime { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>
    /// 获取或设置任务非空闲时工作线程的休眠时间。（默认1秒）。
    /// </summary>
    public TimeSpan TaskBusyTime { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>
    /// 服务安全退出前等待的超时时间。单位：秒，默认无限期等待。
    /// </summary>
    private int safeExitTimeout = 0;
    /// <summary>
    /// 获取或设置服务安全退出前等待的超时时间。单位：秒，默认无限期等待。
    /// <remarks>取值为大于或等于0的整数。设置为0表示无限期等待。</remarks>
    /// </summary>
    public int ExitWaitTimeout
    {
      get
      {
        return safeExitTimeout;
      }
      set
      {
        if (value < 0)
        {
          safeExitTimeout = 0;
        }
        else
        {
          safeExitTimeout = value;
        }
      }
    }
    /// <summary>
    /// 定义服务安全退出信号量。
    /// </summary>
    private EventWaitHandle SafeExitWaitHandle;
    #endregion

    #region events

    /// <summary>
    /// 工作线程启动时触发该事件。
    /// </summary>
    public event TaskThreadStartedEventHandler ThreadStarted;

    /// <summary>
    /// 工作任务构建失败时触发该事件。
    /// </summary>
    public event TaskBuildFaultedEventHandler TaskBuildFaulted;

    /// <summary>
    /// 工作任务即将执行前触发该事件。
    /// </summary>
    public event TaskExecutingEventHandler<TTask> TaskExecuting;

    /// <summary>
    /// 工作任务完成时触发该事件。
    /// </summary>
    public event TaskExecutedEventHandler<TTask> TaskExecuted;

    /// <summary>
    /// 工作线程退出时触发该事件。
    /// </summary>
    public event TaskThreadExitedEventHandler ThreadExited;

    /// <summary>
    /// 服务停止后触发该事件。
    /// </summary>
    public event ServiceStopedEventHandler ServiceStoped;

    #endregion

    #region methods

    /// <summary>
    /// 使当前正在执行任务的工作线程数加1
    /// </summary>
    /// <returns></returns>
    private void IncrementActiveThread()
    {
      Interlocked.Increment(ref activeThreadCount);
    }

    /// <summary>
    /// 使当前正在执行任务的工作线程数减1。如果正在执行任务的工作线程数小于1则不做任何操作。
    /// </summary>
    /// <returns></returns>
    private void DecrementActiveThread()
    {
      if (ActiveThreadCount > 0)
      {
        Interlocked.Decrement(ref activeThreadCount);
      }
    }

    /// <summary>
    /// 通知工作线程已启动。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected void OnThreadStarted(object sender, TaskThreadStartedEventArgs e)
    {
      ThreadStarted?.Invoke(sender, e);
    }

    /// <summary>
    /// 通知工作任务构建失败。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected void OnTaskBuildFaulted(object sender, TaskBuildFaultedEventArgs e)
    {
      TaskBuildFaulted?.Invoke(sender, e);
    }

    /// <summary>
    /// 通知工作线程即将执行任务。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected void OnTaskExecuting(object sender, TaskExecutingEventArgs<TTask> e)
    {
      IncrementActiveThread();
      TaskExecuting?.Invoke(sender, e);
    }

    /// <summary>
    /// 通知工作线程执行任务完成。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="ObjectDisposedException"></exception>
    protected void OnTaskExecuted(object sender, TaskExecutedEventArgs<TTask> e)
    {
      if (disposed)
      {
        throw new ObjectDisposedException(nameof(BackgroundTaskThreadService<TTask, TThread>));
      }

      DecrementActiveThread();
      TaskExecuted?.Invoke(sender, e);
    }

    /// <summary>
    /// 通知工作线程已退出。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected void OnThreadExited(object sender, TaskThreadExitedEventArgs e)
    {
      ThreadExited?.Invoke(sender, e);

      if (ActiveThreadCount == 0 && SafeExitWaitHandle != null)
      {
        SafeExitWaitHandle.Set();
      }
    }

    /// <summary>
    /// 通知服务已停止。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected void OnServiceStoped(ServiceStopEventArgs e)
    {
      ServiceStoped?.Invoke(this, e);
    }

    /// <summary>
    /// 开始服务。
    /// </summary>
    /// <remarks>
    /// 当ServerEnabled为true时，才能启动服务。
    /// 如果服务已经为启动状态，该方法不做任何操作。
    /// </remarks>
    /// <exception cref="ObjectDisposedException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public void Start()
    {
      if (disposed)
      {
        throw new ObjectDisposedException(nameof(BackgroundTaskThreadService<TTask, TThread>));
      }

      if (!Enabled)
      {
        throw new InvalidOperationException("启动失败，当前服务被禁用。");
      }

      lock (this)
      {
        if (!IsRunning)
        {
          serviceRunning = true;
          SafeExitWaitHandle = new ManualResetEvent(false);
          InitializeTaskThreads();
          StartTaskThreads();
        }
      }
    }

    private void InitializeTaskThreads()
    {
      TaskThreads = new TThread[ThreadCount];

      for (int i = 0; i < ThreadCount; i++)
      {
        TThread taskThread = new TThread()
        {
          Index = i,
          Name = string.Format("Thread-{0}", i.ToString()),
          TaskIdleTime = TaskIdleTime,
          TaskBusyTime = TaskBusyTime
        };

        taskThread.ThreadStarted += new TaskThreadStartedEventHandler(OnThreadStarted);
        taskThread.TaskBuildFaulted += new TaskBuildFaultedEventHandler(OnTaskBuildFaulted);
        taskThread.TaskExecuting += new TaskExecutingEventHandler<TTask>(OnTaskExecuting);
        taskThread.TaskExecuted += new TaskExecutedEventHandler<TTask>(OnTaskExecuted);
        taskThread.ThreadExited += new TaskThreadExitedEventHandler(OnThreadExited);

        TaskThreads[i] = taskThread;
      }
    }

    private void StartTaskThreads()
    {
      if (TaskThreads != null && TaskThreads.Length > 0)
      {
        for (int i = 0; i < TaskThreads.Length; i++)
        {
          TaskThreads[i].Start();
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
      if (IsRunning == false)
      {
        return;
      }

      serviceRunning = false;

      if (TaskThreads != null && TaskThreads.Length > 0)
      {
        for (int i = 0; i < TaskThreads.Length; i++)
        {
          TaskThreads[i].Stop();
        }
      }

      bool IsSafeExited = true;

      if (ActiveThreadCount > 0 && SafeExitWaitHandle != null)
      {
        // 停止时还有任务未执行完成，等待任务执行完成
        // 但在等待指定超时时间后，会强制结束。
        TimeSpan timeout = ExitWaitTimeout > 0 ?
            TimeSpan.FromSeconds(ExitWaitTimeout) :
            TimeSpan.FromMilliseconds(-1);
        System.Diagnostics.Debug.WriteLine(string.Format("ready to wait {0}s.", timeout.TotalSeconds), Name);
        IsSafeExited = SafeExitWaitHandle.WaitOne(timeout);
        SafeExitWaitHandle.Dispose();
        SafeExitWaitHandle = null;
      }

      System.Diagnostics.Debug.WriteLine("trigger ServiceStoped event.", Name);

      // 通知服务已停止。
      OnServiceStoped(new ServiceStopEventArgs(IsSafeExited));
    }

    #endregion

    #region Dispose & Finalize

    /// <summary>
    /// 释放由BackgroundTaskThreadService类的当前实例占用的所有资源。
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放BackgroundTaskThreadService。同时释放所有非托管资源。
    /// </summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
      if (!disposed)
      {
        if (disposing)
        {
          if (IsRunning)
          {
            Stop();
          }

          if (TaskThreads != null && TaskThreads.Length > 0)
          {
            for (int i = 0; i < TaskThreads.Length; i++)
            {
              TaskThreads[i].Dispose();
              TaskThreads[i] = null;
            }
          }
        }

        disposed = true;
      }
    }

    /// <summary>
    /// 析构函数。
    /// </summary>
    ~BackgroundTaskThreadService()
    {
      Dispose(false);
    }

    #endregion
  }
}