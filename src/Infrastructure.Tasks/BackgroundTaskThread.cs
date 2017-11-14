// Copyright (c) lyson@outlook.com All rights reserved.

namespace Infrastructure.Tasks
{
    using System;
    using System.Threading;

    /// <summary>
    /// 定义工作任务执行线程单元。
    /// </summary>
    public abstract class BackgroundTaskThread : IDisposable
    {
        #region constructors

        /// <summary>
        /// 构造函数。以指定的线程名称初始化<see cref="BackgroundTaskThread"/>类的新实例。
        /// </summary>
        /// <param name="threadName">线程名称。</param>
        public BackgroundTaskThread(string threadName = "BackgroundTaskThread")
        {
            Name = threadName;
        }

        #endregion

        #region properties

        /// <summary>
        /// 资源释放标志。
        /// </summary>
        private bool disposed = false;
        /// <summary>
        /// 工作线程运行标志。
        /// </summary>
        private volatile bool Running = false;
        /// <summary>
        /// 工作线程。
        /// </summary>
        private Thread TaskThread;
        /// <summary>
        /// 获取工作线程ID。
        /// </summary>
        public Guid Id { get; private set; } = Guid.NewGuid();
        /// <summary>
        /// 获取或设置工作线程的名称。
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 获取或设置工作线程的序号。
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// 工作线程休眠信号量。
        /// </summary>
        public EventWaitHandle TaskWaitHandle { get; private set; }
        /// <summary>
        /// 获取或设置任务空闲时工作线程的休眠时间（默认5分钟）。
        /// </summary>
        public TimeSpan TaskIdleTime { get; set; } = TimeSpan.FromMinutes(5);
        /// <summary>
        /// 获取或设置任务非空闲时工作线程的休眠时间。（默认1秒）。
        /// </summary>
        public TimeSpan TaskBusyTime { get; set; } = TimeSpan.FromSeconds(1);
        #endregion

        #region events

        /// <summary>
        /// 工作线程启动时触发该事件。
        /// </summary>
        public event TaskThreadStartedEventHandler ThreadStarted;

        /// <summary>
        /// 工作线程退出时触发该事件。
        /// </summary>
        public event TaskThreadExitedEventHandler ThreadExited;

        /// <summary>
        /// 工作任务构建失败时触发该事件。
        /// </summary>
        public event TaskBuildFaultedEventHandler TaskBuildFaulted;

        /// <summary>
        /// 工作任务即将执行前触发该事件。
        /// </summary>
        public event TaskExecutingEventHandler TaskExecuting;

        /// <summary>
        /// 工作任务完成时触发该事件。
        /// </summary>
        public event TaskExecuteEventHandler TaskExecuted;

        #endregion

        #region methods

        /// <summary>
        /// 通知工作线程已启动。
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnTaskThreadStarted(TaskThreadStartEventArgs e)
        {
            ThreadStarted?.Invoke(this, e);
        }

        /// <summary>
        /// 通知工作线程已退出。
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnTaskThreadStoped(TaskThreadExitEventArgs e)
        {
            ThreadExited?.Invoke(this, e);
        }

        /// <summary>
        /// 通知工作任务构建失败。
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnTaskBuildFaulted(TaskBuildEventArgs e)
        {
            TaskBuildFaulted?.Invoke(this, e);
        }

        /// <summary>
        /// 通知工作线程即将执行任务。
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnTaskExecuting(TaskExecutingEventArgs e)
        {
            TaskExecuting?.Invoke(this, e);
        }

        /// <summary>
        /// 通知工作线程执行任务完成。
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnTaskExecuted(TaskExecuteEventArgs e)
        {
            TaskExecuted?.Invoke(this, e);
        }

        /// <summary>
        /// 该方法负责获取工作线程要执行的目标任务。
        /// 实现子类需自行保证多线程环境下的线程同步。
        /// </summary>
        /// <remarks>如果工作线程不需要具体任务数据对象，可以返回<see cref="BackgroundTask"/>对象的一个实例。
        /// <example>return new BackgroundTask();</example>
        /// </remarks>
        /// <returns>返回<see cref="BackgroundTask"/>类型的任务。</returns>
        protected abstract BackgroundTask GetTask();

        /// <summary>
        /// 目标任务执行主逻辑。
        /// </summary>
        /// <param name="task"><see cref="BackgroundTask"/>类型的任务。</param>
        protected abstract void ExecuteTask(BackgroundTask task);

        /// <summary>
        /// 工作线程周期执行任务。
        /// </summary>
        private void ThreadWork()
        {
            while (Running)
            {
                // 获取目标任务
                BackgroundTask task = null;
                try
                {
                    task = GetTask();
                }
                catch (Exception ex)
                {
                    OnTaskBuildFaulted(new TaskBuildEventArgs(this)
                    {
                        Exception = new BackgroundTaskException(ex.Message, ex)
                    });
                    // 使用空闲休眠时间休眠
                    TaskWaitHandle.WaitOne(TaskIdleTime);
                    continue;
                }

                if (null == task)
                {
                    // 使用空闲休眠时间休眠
                    TaskWaitHandle.WaitOne(TaskIdleTime);
                    continue;
                }

                // 定义任务执行完成事件参数
                TaskExecuteEventArgs taskExecuteArgs = new TaskExecuteEventArgs(this)
                {
                    Task = task
                };

                try
                {
                    OnTaskExecuting(new TaskExecutingEventArgs(this) { Task = task });
                    ExecuteTask(task);
                }
                catch (Exception ex)
                {
                    taskExecuteArgs.IsFaulted = true;
                    taskExecuteArgs.Exception = new BackgroundTaskException(ex.Message, ex);
                }
                finally
                {
                    // 通知工作线程已执行完一轮任务。
                    taskExecuteArgs.CompletedTime = DateTime.Now;
                    OnTaskExecuted(taskExecuteArgs);
                }

                // 工作线程周期信号量
                TaskWaitHandle.WaitOne(TaskBusyTime);
            }
            TaskWaitHandle.Dispose();
            TaskWaitHandle = null;

            //通知工作线程已退出。
            OnTaskThreadStoped(new TaskThreadExitEventArgs(this));
        }

        /// <summary>
        /// 开始服务。
        /// </summary>
        /// <remarks>
        /// 如果已经为启动状态，该方法不做任何操作。
        /// </remarks>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Start()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(BackgroundTaskThread));
            }
            lock (this)
            {
                if (!Running)
                {
                    Running = true;

                    TaskWaitHandle = new AutoResetEvent(false);

                    TaskThread = new Thread(new ThreadStart(ThreadWork))
                    {
                        Name = Name,
                        IsBackground = true
                    };
                    TaskThread.Start();

                    OnTaskThreadStarted(new TaskThreadStartEventArgs(this));
                }
            }
        }

        /// <summary>
        /// 停止工作线程。
        /// </summary>
        /// <remarks>
        /// 如果工作线程已经为停止状态，该方法不做任何操作。
        /// 如果在调用Stop方法停止工作线程时还有运行中的任务，工作线程会等待WaitExitMinute指定的超时分钟数。
        /// </remarks>
        public void Stop()
        {
            if (Running == false)
            {
                return;
            }

            Running = false;


            if (TaskWaitHandle != null)
            {
                // 通知工作线程休眠信号量。
                TaskWaitHandle.Set();
            }
        }

        #endregion

        #region Dispose & Finalize

        /// <summary>
        /// 释放由BackgroundTaskThread类的当前实例占用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放BackgroundTaskThread。同时释放所有非托管资源。
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
        ~BackgroundTaskThread()
        {
            Dispose(false);
        }

        #endregion
    }
}