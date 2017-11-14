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
        private int maxServiceRunnum = 1;
        /// <summary>
        /// 获取或设置最大工作线程数。默认12倍当前计算机处理器数。
        /// </summary>
        public int MaxServiceRunnum
        {
            get
            {
                return maxServiceRunnum;
            }
            protected set
            {
                if (value > 0)
                {
                    maxServiceRunnum = value;
                }
            }
        }
        /// <summary>
        /// 获取当前工作线程数
        /// </summary>
        private int serviceRunnum = 0;
        /// <summary>
        /// 获取当前工作线程数。
        /// </summary>
        public int ServiceRunnum
        {
            get { return serviceRunnum; }
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
        #endregion

        #region events

        /// <summary>
        /// 服务运行主逻辑执行异常时触发该事件。
        /// </summary>
        public event Action<Exception> ServiceErrored;

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
                throw new ArgumentNullException("serviceName");
            }

            ServiceName = serviceName;
            MaxServiceRunnum = Environment.ProcessorCount * 12;
        }

        #endregion

        #region methods

        /// <summary>
        /// 使当前服务运行线程数加1
        /// </summary>
        /// <returns></returns>
        private void IncrementRunnum()
        {
            Interlocked.Increment(ref serviceRunnum);
        }

        /// <summary>
        /// 使当前服务运行线程数减1。如果当前服务运行线程数小于1则不做任何操作。
        /// </summary>
        /// <returns></returns>
        private void DecrementRunnum()
        {
            if (ServiceRunnum < 1)
                return;

            Interlocked.Decrement(ref serviceRunnum);

            if (ServiceRunnum == 0)
            {
                // 如果当前工作线程数为0时，通知可以安全退出
                if (ServiceSafeExitResetEvent != null)
                {
                    ServiceSafeExitResetEvent.Set();
                }
            }
        }

        /// <summary>
        /// 通知服务运行主逻辑发生异常
        /// </summary>
        /// <param name="exp"></param>
        protected virtual void OnServiceError(Exception exp)
        {
            ServiceErrored?.Invoke(exp);
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
        protected abstract TTask GetTask();

        /// <summary>
        /// 目标任务执行主逻辑。
        /// </summary>
        /// <param name="task"><see cref="TTask"/>类型的任务。</param>
        protected abstract void ExecuteTask(TTask task);

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

                if (ServiceRunnum >= MaxServiceRunnum)
                {
                    // 工作线程已满,等待其它线程退出
                    dispatchTimeout = TimeSpan.FromMilliseconds(500);
                    continue;
                }

                // 使当前工作线程数加1
                IncrementRunnum();

                Task.Factory.StartNew(new Action(() =>
                {
                    Stopwatch stopWatch = Stopwatch.StartNew();

                    // 获取目标任务
                    TTask task = GetTask();

                    if (null == task)
                    {
                        // 暂时没有需要处理的数据
                        IsTaskRapid = false;
                        return;
                    }

                    // 有需要处理的数据
                    IsTaskRapid = true;

                    try
                    {
                        ExecuteTask(task);
                    }
                    finally
                    {
                        // 性能计数
                    }

                }), TaskCreationOptions.PreferFairness)

                // 任务执行完毕后需要一些操作
                .ContinueWith((t) =>
                {
                    // 执行任务完成后，使当前工作线程数减1
                    DecrementRunnum();

                    if (t.IsFaulted)
                    {
                        // 通知任务执行异常
                        Exception exp = t.Exception.InnerException ?? t.Exception;
                        OnServiceError(exp);
                    }

                    if (ServiceRunnum == 0 &&
                        ServiceSafeExitResetEvent != null)
                    {
                        // 通知服务现在可以安全退出
                        ServiceSafeExitResetEvent.Set();
                    }
                });

            } while (TaskDispatchResetEvent.WaitOne(dispatchTimeout) == false);
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
                LogTrace(string.Format("由于服务{0}被禁用，调用Start方法启动失败!", ServiceName));
                return;
            }

            lock (this)
            {
                if (!Running)
                {
                    Running = true;

                    TaskDispatchResetEvent = new AutoResetEvent(false);
                    ServiceSafeExitResetEvent = new AutoResetEvent(false);

                    Task.Factory.StartNew(DispatchTasks, TaskCreationOptions.LongRunning)
                        .ContinueWith((t) =>
                        {
                            // 服务运行结束
                            // 释放资源
                            if (TaskDispatchResetEvent != null)
                            {
                                TaskDispatchResetEvent.Dispose();
                                TaskDispatchResetEvent = null;
                            }
                        });

                    LogTrace(string.Format("服务{0}启动成功!", ServiceName));
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
            if (Running == false)
            {
                return;
            }

            Running = false;
            LogTrace(string.Format("服务{0}正在停止...", ServiceName));

            if (TaskDispatchResetEvent != null)
            {
                // 通知服务调度不再继续新的工作线程
                TaskDispatchResetEvent.Set();
            }

            if (ServiceRunnum > 0
                && ServiceSafeExitResetEvent != null)
            {
                // 当有正在工作的线程存在，等待工作线程安全退出
                // 但在等待指定超时时间后，工作线程还未退出，服务会强制结束。
                TimeSpan timeout = WaitExitTimeout > 0 ?
                    TimeSpan.FromSeconds(WaitExitTimeout) :
                    TimeSpan.FromMilliseconds(-1);
                ServiceSafeExitResetEvent.WaitOne(timeout);
                ServiceSafeExitResetEvent.Dispose();
                ServiceSafeExitResetEvent = null;
            }

            //通知服务运行完毕
            OnServiceCompleted();

            LogTrace(string.Format("服务{0}已停止!", ServiceName));
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
        protected virtual void LogInfo(string message)
        {
            Debug.WriteLine(message);
        }

        /// <summary>
        /// 记录服务跟踪日志
        /// </summary>
        /// <param name="message"></param>
        protected virtual void LogTrace(string message)
        {
            Debug.WriteLine(message);
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
