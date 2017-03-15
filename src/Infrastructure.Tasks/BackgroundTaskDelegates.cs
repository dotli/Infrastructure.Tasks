// Copyright (c) lyson@outlook.com All rights reserved.

namespace Infrastructure.Tasks
{
    using System;

    #region Thread Delegates

    /// <summary>
    /// 定义工作线程启动事件的委托。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"><see cref="TaskThreadStartEventArgs"/></param>
    public delegate void TaskThreadStartedEventHandler(object sender, TaskThreadStartEventArgs e);
    /// <summary>
    /// 定义工作线程退出事件的委托。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"><see cref="TaskThreadExitEventArgs"/></param>
    public delegate void TaskThreadExitedEventHandler(object sender, TaskThreadExitEventArgs e);
    /// <summary>
    /// 定义工作任务获取失败事件的委托。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"><see cref="TaskBuildEventArgs"/></param>
    public delegate void TaskBuildFaultedEventHandler(object sender, TaskBuildEventArgs e);
    /// <summary>
    /// 定义工作任务执行前事件的委托。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"><see cref="TaskExecutingEventArgs"/></param>
    public delegate void TaskExecutingEventHandler(object sender, TaskExecutingEventArgs e);
    /// <summary>
    /// 定义工作任务完成事件的委托。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"><see cref="TaskExecuteEventArgs"/></param>
    public delegate void TaskExecuteEventHandler(object sender, TaskExecuteEventArgs e);
    /// <summary>
    /// 定义工作线程启动事件所需要的参数。
    /// </summary>
    public class TaskThreadStartEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化<see cref="TaskThreadStartEventArgs"/>类的新实例。
        /// </summary>
        public TaskThreadStartEventArgs() { }
        /// <summary>
        /// 使用指定的<see cref="BackgroundTaskThread"/>初始化<see cref="TaskThreadStartEventArgs"/>类的新实例。
        /// </summary>
        /// <param name="thread">表示当前执行任务的<see cref="BackgroundTaskThread"/>类。</param>
        public TaskThreadStartEventArgs(BackgroundTaskThread thread)
        {
            this.Thread = thread;
        }
        /// <summary>
        /// 关联工作线程。
        /// </summary>
        public BackgroundTaskThread Thread { get; set; }
    }
    /// <summary>
    /// 定义工作线程退出事件所需要的参数。
    /// </summary>
    public class TaskThreadExitEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化<see cref="TaskThreadExitEventArgs"/>类的新实例。
        /// </summary>
        public TaskThreadExitEventArgs() { }
        /// <summary>
        /// 使用指定的<see cref="BackgroundTaskThread"/>初始化<see cref="TaskThreadExitEventArgs"/>类的新实例。
        /// </summary>
        /// <param name="thread">表示当前执行任务的<see cref="BackgroundTaskThread"/>类。</param>
        public TaskThreadExitEventArgs(BackgroundTaskThread thread)
        {
            this.Thread = thread;
        }
        /// <summary>
        /// 关联工作线程。
        /// </summary>
        public BackgroundTaskThread Thread { get; set; }
    }
    /// <summary>
    /// 定义工作任务构建相关事件所需要的参数。
    /// </summary>
    public class TaskBuildEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化<see cref="TaskBuildEventArgs"/>类的新实例。
        /// </summary>
        public TaskBuildEventArgs() { }
        /// <summary>
        /// 使用指定的<see cref="BackgroundTaskThread"/>初始化<see cref="TaskBuildEventArgs"/>类的新实例。
        /// </summary>
        /// <param name="thread">表示当前执行任务的<see cref="BackgroundTaskThread"/>类。</param>
        public TaskBuildEventArgs(BackgroundTaskThread thread)
        {
            this.Thread = thread;
        }
        /// <summary>
        /// 关联工作线程。
        /// </summary>
        public BackgroundTaskThread Thread { get; set; }
        /// <summary>
        /// 构建任务过程中产生的异常。如果构建任务过程中未引发任何异常，将返回 null。
        /// </summary>
        public BackgroundTaskException Exception { get; internal set; }
    }
    /// <summary>
    /// 定义工作任务执行相关事件所需要的参数。
    /// </summary>
    public class TaskExecutingEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化<see cref="TaskExecutingEventArgs"/>类的新实例。
        /// </summary>
        public TaskExecutingEventArgs() { }
        /// <summary>
        /// 使用指定的<see cref="BackgroundTaskThread"/>初始化<see cref="TaskExecutingEventArgs"/>类的新实例。
        /// </summary>
        /// <param name="thread">表示当前执行任务的<see cref="BackgroundTaskThread"/>类。</param>
        public TaskExecutingEventArgs(BackgroundTaskThread thread)
        {
            this.Thread = thread;
        }
        /// <summary>
        /// 关联工作线程。
        /// </summary>
        public BackgroundTaskThread Thread { get; set; }
        /// <summary>
        /// 表示目标工作任务。
        /// </summary>
        public BackgroundTask Task { get; set; }
    }
    /// <summary>
    /// 定义工作任务执行完成事件所需要的参数。
    /// </summary>
    public class TaskExecuteEventArgs : EventArgs
    {
        /// <summary>
        /// 使用指定的<see cref="BackgroundTaskThread"/>初始化<see cref="TaskExecuteEventArgs"/>类的新实例。
        /// </summary>
        /// <param name="thread">表示当前执行任务的<see cref="BackgroundTaskThread"/>类。</param>
        public TaskExecuteEventArgs(BackgroundTaskThread thread)
        {
            this.Thread = thread;
            this.BeginTime = DateTime.Now;
        }
        /// <summary>
        /// 初始化<see cref="TaskExecuteEventArgs"/>类的新实例。
        /// </summary>
        public TaskExecuteEventArgs()
        {
            this.BeginTime = DateTime.Now;
        }
        /// <summary>
        /// 关联工作线程。
        /// </summary>
        public BackgroundTaskThread Thread { get; internal set; }
        /// <summary>
        /// 表示目标工作任务。
        /// </summary>
        public BackgroundTask Task { get; internal set; }
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
    }
    #endregion
}