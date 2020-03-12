// Copyright (c) lyson@outlook.com All rights reserved.

namespace Infrastructure.Tasks
{
  using System;

  #region Thread Delegates

  /// <summary>
  /// 定义工作线程启动事件的委托。
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"><see cref="TaskThreadStartedEventArgs"/></param>
  public delegate void TaskThreadStartedEventHandler(object sender, TaskThreadStartedEventArgs e);
  /// <summary>
  /// 定义工作任务获取失败事件的委托。
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"><see cref="TaskBuildFaultedEventArgs"/></param>
  public delegate void TaskBuildFaultedEventHandler(object sender, TaskBuildFaultedEventArgs e);
  /// <summary>
  /// 定义工作任务执行前事件的委托。
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"><see cref="TaskExecutingEventArgs{TTask}"/></param>
  public delegate void TaskExecutingEventHandler<TTask>(object sender, TaskExecutingEventArgs<TTask> e)
    where TTask : BackgroundTask;
  /// <summary>
  /// 定义工作任务完成事件的委托。
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"><see cref="TaskExecutedEventArgs{TTask}"/></param>
  public delegate void TaskExecutedEventHandler<TTask>(object sender, TaskExecutedEventArgs<TTask> e)
    where TTask : BackgroundTask;
  /// <summary>
  /// 定义工作线程退出事件的委托。
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"><see cref="TaskThreadExitedEventArgs"/></param>
  public delegate void TaskThreadExitedEventHandler(object sender, TaskThreadExitedEventArgs e);


  /// <summary>
  /// 定义工作线程启动事件所需要的参数。
  /// </summary>
  public class TaskThreadStartedEventArgs : EventArgs
  {
    /// <summary>
    /// 初始化<see cref="TaskThreadStartedEventArgs{TTask}"/>类的新实例。
    /// </summary>
    public TaskThreadStartedEventArgs() { }
  }
  /// <summary>
  /// 定义工作线程退出事件所需要的参数。
  /// </summary>
  public class TaskThreadExitedEventArgs : EventArgs
  {
    /// <summary>
    /// 初始化<see cref="TaskThreadExitedEventArgs"/>类的新实例。
    /// </summary>
    public TaskThreadExitedEventArgs() { }
  }
  /// <summary>
  /// 定义工作任务构建相关事件所需要的参数。
  /// </summary>
  public class TaskBuildFaultedEventArgs : EventArgs
  {
    /// <summary>
    /// 初始化<see cref="TaskBuildFaultedEventArgs"/>类的新实例。
    /// </summary>
    public TaskBuildFaultedEventArgs() { }
    /// <summary>
    /// 使用指定的<see cref="BackgroundTaskException"/>初始化<see cref="TaskBuildFaultedEventArgs"/>类的新实例。
    /// </summary>
    /// <param name="exception"><see cref="BackgroundTaskException"/>类的实例，表示构建任务过程中产生的异常。</param>
    public TaskBuildFaultedEventArgs(BackgroundTaskException exception)
    {
      Exception = exception;
    }
    /// <summary>
    /// 构建任务过程中产生的异常。如果构建任务过程中未引发任何异常，将返回 null。
    /// </summary>
    public BackgroundTaskException Exception { get; internal set; }
  }
  /// <summary>
  /// 定义工作任务执行相关事件所需要的参数。
  /// </summary>
  public class TaskExecutingEventArgs<TTask> : EventArgs where TTask : BackgroundTask
  {
    /// <summary>
    /// 初始化<see cref="TaskExecutingEventArgs{TTask}"/>类的新实例。
    /// </summary>
    public TaskExecutingEventArgs() { }
    /// <summary>
    /// 使用指定的<see cref="TTask"/>初始化<see cref="TaskExecutingEventArgs{TTask}"/>类的新实例。
    /// </summary>
    /// <param name="task"><see cref="TTask"/>类的实例，表示当前工作线程的关联的任务数据对象。</param>
    public TaskExecutingEventArgs(TTask task)
    {
      Task = task;
    }
    /// <summary>
    /// 关联任务数据对象。
    /// </summary>
    public TTask Task { get; set; }
  }
  /// <summary>
  /// 定义工作任务执行完成事件所需要的参数。
  /// </summary>
  public class TaskExecutedEventArgs<TTask> : EventArgs where TTask : BackgroundTask
  {
    /// <summary>
    /// 初始化<see cref="TaskExecutedEventArgs{TTask}"/>类的新实例。
    /// </summary>
    public TaskExecutedEventArgs()
    {
      BeginTime = DateTime.Now;
    }
    /// <summary>
    /// 使用指定的<see cref="TTask"/>初始化<see cref="TaskExecutedEventArgs{TTask}"/>类的新实例。
    /// </summary>
    /// <param name="task"><see cref="TTask"/>类的实例，表示当前工作线程的关联的任务数据对象。</param>
    public TaskExecutedEventArgs(TTask task) : this()
    {
      Task = task;
    }
    /// <summary>
    /// 表示目标工作任务。
    /// </summary>
    public TTask Task { get; internal set; }
    /// <summary>
    /// 任务执行开始时间。
    /// </summary>
    public DateTime BeginTime { get; internal set; }
    /// <summary>
    /// 任务执行完成时间。
    /// </summary>
    public DateTime CompletedTime { get; internal set; }
    /// <summary>
    /// 任务执行耗时。单位：毫秒
    /// </summary>
    public long Milliseconds
    {
      get
      {
        if (CompletedTime == null) CompletedTime = DateTime.Now;
        return (long)(CompletedTime - BeginTime).TotalMilliseconds;
      }
    }
    /// <summary>
    /// 表示任务是否由于未经处理异常的原因而完成。
    /// 如果任务引发了未经处理的异常，则为 true；否则为 false。
    /// </summary>
    public bool IsFaulted { get; internal set; }
    /// <summary>
    /// 执行任务过程中产生的异常。如果任务过程中未引发任何异常，将返回 null。
    /// </summary>
    public BackgroundTaskException Exception { get; internal set; }
  }
  #endregion

  #region Service Delegates

  /// <summary>
  /// 定义服务停止事件委托。
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public delegate void ServiceStopedEventHandler(object sender, ServiceStopEventArgs e);

  /// <summary>
  /// 定义服务停止事件所需要的参数。
  /// </summary>
  public class ServiceStopEventArgs : EventArgs
  {
    /// <summary>
    /// 表示服务是否是安全退出。
    /// </summary>
    public bool IsSafeExited { get; set; }
    /// <summary>
    /// 初始化<see cref="ServiceStopEventArgs"/>类的新实例。
    /// </summary>
    public ServiceStopEventArgs() { }
    /// <summary>
    /// 初始化<see cref="ServiceStopEventArgs"/>类的新实例。
    /// </summary>
    /// <param name="safeExited">表示服务是否是安全退出。</param>
    public ServiceStopEventArgs(bool safeExited)
    {
      IsSafeExited = safeExited;
    }
  }

  /// <summary>
  /// 定义服务工作线程状态变更事件所需要的参数。
  /// </summary>
  public class ServiceThreadStatusChangedEventArgs<TTask> : EventArgs where TTask : BackgroundTask
  {
    /// <summary>
    /// 当前工作线程状态。
    /// </summary>
    public ServiceThreadStatus Status { get; set; }
    /// <summary>
    /// 关联任务数据对象。
    /// </summary>
    public TTask Task { get; set; }
    /// <summary>
    /// 任务执行异常。
    /// </summary>
    public AggregateException Exception { get; set; }
    /// <summary>
    /// 初始化类<see cref="WorkThreadReportEventArgs"/>的新实例。
    /// </summary>
    /// <param name="status">当前工作线程状态。</param>
    public ServiceThreadStatusChangedEventArgs(ServiceThreadStatus status)
    {
      Status = status;
    }
  }

  /// <summary>
  /// 工作线程状态。
  /// </summary>
  public enum ServiceThreadStatus : byte
  {
    /// <summary>
    /// 初始状态。
    /// </summary>
    Initialized = 0,
    /// <summary>
    /// 没有任务需要处理。
    /// </summary>
    NoTask = 1,
    /// <summary>
    /// 即将执行。
    /// </summary>
    Executing = 2,
    /// <summary>
    /// 处理完成。
    /// </summary>
    Completed = 3,
    /// <summary>
    /// 处理失败。
    /// </summary>
    Falted = 4
  }

  #endregion
}