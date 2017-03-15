// Copyright (c) lyson@outlook.com All rights reserved.

namespace Infrastructure.Tasks {
	using System;

	/// <summary>
	/// 指定后台服务执行任务的数据对象类型。
	/// </summary>
	public abstract class BackgroundTask {
		/// <summary>
		/// 初始化 <see cref="BackgroundTask"/> 类的新实例。
		/// </summary>
		public BackgroundTask() {
			Name = "BackgroundTask";
		}
		/// <summary>
		/// 定义任务ID。
		/// </summary>
		public string Id { get; set; }
		/// <summary>
		/// 定义任务的名称。
		/// </summary>
		public string Name { get; set; }
	}

	/// <summary>
	/// 默认后台服务执行任务的数据对象类型。
	/// </summary>
	public class DefaultBackgroundTask : BackgroundTask {
		/// <summary>
		/// 初始化 <see cref="DefaultBackgroundTask"/> 类的新实例。
		/// </summary>
		public DefaultBackgroundTask()
			: base() {
			this.Id = Guid.NewGuid().ToString();
			this.Name = "DefaultBackgroundTask";
		}
	}

	/// <summary>
	/// 指定后台服务执行任务的数据对象类型。
	/// </summary>
	/// <typeparam name="T">执行具体任务的数据对象类型。</typeparam>
	public class BackgroundTask<T> : BackgroundTask {
		public BackgroundTask() : base() { }
		public T TaskData { get; set; }
	}

}