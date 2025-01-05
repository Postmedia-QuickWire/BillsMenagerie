using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Classes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bills.Common.Classes
{

	/// <summary>
	/// This class handles sub folders under a specified root as searate queue tasks
	/// This needs to be used as a base class --> override Init
	/// 
	/// I'm thinking I should implement Rabitmq in the same model?
	/// 
	/// the idea is to override MultiFolderEventQueue and  MultiFolderEventQueue &lt; TQueueObject> 
	/// where TQueueObject is your override of MultiFolderEventQueue
	/// and your MultiFolderEventQueue override will handle the queue task callbacks themselves
	/// </summary>
	public class MultiFolderQueueTaskBase
	{
		private readonly SemaphoreSlim _Signal = new SemaphoreSlim(1, 1);
		protected readonly ILogger _logger;
		public Task RunningTask { get; set; }
		public string QueueId { get; set; }
		// full path to queue (sub) folder - set by base class
		public string QueueFolder { get; set; }
		public TimeSpan retryInterval { get; set; }
		public string LastError { get; set; } // can use this for logging to WireMonitor
		public int ErrorCnt { get; set; }
		public string CurrentFile { get; set; }
		public void NotifyQueue()
		{
			try
			{
				if (_Signal.CurrentCount == 0)
					_Signal.Release();
			}
			catch (Exception) { }
		}
		public MultiFolderQueueTaskBase(string id, ILogger logger)
		{
			QueueId = id.Trim().ToLower(); // MUST be foldername compliant
			_logger = logger;
			LastError = "";
			CurrentFile = "";
			ErrorCnt = 0;
		}

		public Func<MultiFolderQueueTaskBase, string, CancellationToken, Task<bool>> OnQueueAction { get; set; }
		public virtual async Task Start(CancellationToken stoppingToken)
		{
			var dirOptions = new EnumerationOptions()
			{
				IgnoreInaccessible = true,
				AttributesToSkip = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System,
				RecurseSubdirectories = false,
				ReturnSpecialDirectories = false,
				MatchCasing = MatchCasing.CaseInsensitive
			};
			bool skipNextWait = true;
			int waitTime_ms = 600000;
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					// we need to allow the trigger queue host to notify us
					//if (!skipNextWait) //why did I disable this?
					await _Signal.WaitAsync(waitTime_ms, stoppingToken).ConfigureAwait(false); // should we timeout?
					skipNextWait = false;
					waitTime_ms = 600000;
					_logger.LogDebug("Checking queue, {q}", QueueFolder);

					//todo the mask needs to be a property
					var files = Directory.GetFiles(QueueFolder, "*.qwj", dirOptions).OrderBy(f => new FileInfo(f).LastWriteTimeUtc);
					int cnt = 0;
					foreach (var file in files)
					{
						if (stoppingToken.IsCancellationRequested)
							break;
						cnt++;
						CurrentFile = file;
						_logger.LogDebug("Found queue file, {f}", CurrentFile);
						if (await OnQueueAction?.Invoke(this, CurrentFile, stoppingToken))
						{
							DeleteQueueFile(CurrentFile);
						}
						else { 
							_logger.LogDebug("waiting {t} after error", retryInterval.ToString());
							waitTime_ms = (int)retryInterval.TotalMilliseconds;
						}

					}

					skipNextWait = cnt > 0; // means we found at least one file, poll again, no wait
				}
				catch (OperationCanceledException)
				{
					_logger.LogWarning("Queue task canceled, file: {f}", CurrentFile);
				}
				catch (Exception e)
				{
					_logger.LogCritical(e, "Exception, {m}, file: {f}", e.Message, CurrentFile);
				}
			}

		}

		protected virtual void DeleteQueueFile(string filename)
		{
			try
			{
				File.Delete(filename);
			}
			catch (Exception e)
			{
				_logger.LogError("Error {m}, deleting queue file {f}", e.Message, filename);
			}
		}
	}



	/// <summary>
	/// Inherit this and implement CreateQueues and override OnQueueAction
	/// for TQueueObject, use your inherited MultiFolderQueueTaskBase class (that contains your queue task info)
	/// use the helper AddQueue() from your CreateQueues() implementation
	/// 
	/// </summary>
	/// <typeparam name="TQueueObject"></typeparam>
	public abstract class MultiFolderEventQueue<TQueueObject> : IBackgroundServiceRunner
		 where TQueueObject : MultiFolderQueueTaskBase
	{
		private readonly SemaphoreSlim _Signal = new SemaphoreSlim(0, 1);
		private SemaphoreSlim _maxQueueTaskSemaphore;
		protected readonly ILogger _logger;
		protected CancellationTokenSource _queueCancellationToken { get; set; }
		protected ConcurrentDictionary<string, TQueueObject> _queueTasks { get; set; }
		protected string _rootQueuePath { get; set; }
		public int retryErrorIntervalSecs { get; set; } = 30;
		public int maxNumConcurrentQueueTasks { get; set; } = 8;
		
		public bool initialized { get; private set; } = false;

		private FolderEventQueue _queueRootEvent { get; set; }
		public MultiFolderEventQueue(ILogger logger)
		{
			_logger = logger;
			_queueTasks = new ConcurrentDictionary<string, TQueueObject>();
		}

		protected void AddQueue(string id, TQueueObject queue)
		{
			_queueTasks[id.Trim().ToLower()] = queue;
		}

		protected virtual void ReInitialize()
		{
			try
			{
				StopQueues();
				InitQueues();
				StartQueues();
			}
			catch(Exception ex)
			{
				_logger.LogCritical(ex, "ReInitialize error {m}", ex.Message);
			}
		}

		public virtual Task DoRun(CancellationToken stoppingToken)
		{
			// no need to actually run in here!
			_logger.LogInformation("Queue Started");
			return Task.CompletedTask;
		}

		protected void OnNotifyQueueFile(string filename)
		{
			try
			{
				_logger.LogDebug("Queue notify: {f}", filename);
				// get just the last directory name
				var dirname = Path.GetFileName(Path.GetDirectoryName(filename));
				TQueueObject queue;
				if (_queueTasks.TryGetValue(dirname.ToLower(), out queue))
				{
					queue.NotifyQueue();
				}
				else
				{
					_logger.LogWarning("Queue notify in unknown queue folder: {d}, file: {f}", dirname, filename);
				}
			}
			catch(Exception e)
			{
				_logger.LogCritical(e, "Exception in queue notify, {m}", e.Message);
			}
		}

		// create objects BASED on MultiFolderEventQueueTask
		// and add them to the _Queues dictionary
		// key must be the sub folder name
		protected abstract void CreateQueues();

		protected virtual void InitQueues()
		{
			if (_rootQueuePath == null)
				throw new InvalidOperationException("_rootQueuePath MUST be set before calling InitQueues!");

			foreach (var s in _queueTasks.Values)
			{
				s.OnQueueAction -= OnPreQueueAction;
			}
			_queueTasks.Clear();
			_maxQueueTaskSemaphore = new SemaphoreSlim(maxNumConcurrentQueueTasks, maxNumConcurrentQueueTasks);
			CreateQueues();
			foreach (var queue in _queueTasks.Values)
			{
				queue.QueueFolder = Path.Combine(_rootQueuePath, queue.QueueId);
				Directory.CreateDirectory(queue.QueueFolder);
				queue.OnQueueAction += OnPreQueueAction;
				queue.retryInterval = TimeSpan.FromSeconds(retryErrorIntervalSecs);
			}
		}

		protected virtual void StopQueues()
		{
			_queueCancellationToken?.Cancel();
			if (_queueRootEvent != null)
			{
				_queueRootEvent.OnFileAdded -= OnNotifyQueueFile;
				_queueRootEvent.Dispose();
				_queueRootEvent = null;
			}
			var tasks = _queueTasks.Values.Where(q => q.RunningTask != null).Select(q => q.RunningTask).ToArray();
			Task.WaitAll(tasks, 30000);
		}

		protected virtual void StartQueues()
		{
			if (_queueRootEvent != null)
				throw new InvalidOperationException("_queueRootEvent MUST be NULL -- call StopQueues BEFORE StartQueues");

			_queueCancellationToken = new CancellationTokenSource();
			foreach (var queue in _queueTasks.Values)
			{
				queue.RunningTask = queue.Start(_queueCancellationToken.Token);
			}
			_queueRootEvent = new FolderEventQueue(_rootQueuePath, "*.qwj", true);
			_queueRootEvent.OnFileAdded += OnNotifyQueueFile;
		}

		/// <summary>
		/// Each queue(MultiFolderQueueTaskBase) will callback here - so make sure it's thread safe
		/// _maxQueueTaskSemaphore will allow only X number of queue tasks to run at a time
		/// Then OnQueueAction is called -- which must be handled by the subclass.
		/// This way this base class can run under any host and not worry about anything specific to a queue task
		/// </summary>
		/// <param name="queueTask"></param>
		/// <param name="filename"></param>
		/// <param name="stoppingToken"></param>
		/// <returns>true if task ran ok</returns>

		protected virtual async Task<bool> OnPreQueueAction(MultiFolderQueueTaskBase queueTask, string filename, CancellationToken stoppingToken)
		{
			try
			{
				await _maxQueueTaskSemaphore.WaitAsync().ConfigureAwait(false);
				_logger.LogInformation("Running queue action id: {id}, on file: {file}", queueTask.QueueId, filename);
				return await OnQueueAction((TQueueObject)queueTask, filename, stoppingToken);
			}
			catch (Exception e)
			{
				_logger.LogError("Error {m}, posting to {s}, file: {f}", e.Message, queueTask.QueueId, filename);
			}
			finally
			{
				_maxQueueTaskSemaphore.Release();
			}
			return true; // an exception will cause a queue delete!
		}


		/// <summary>
		/// override THIS function as access to it is controlled by the semaphore
		/// </summary>
		/// <param name="queueTask"></param>
		/// <param name="filename"></param>
		/// <param name="stoppingToken"></param>
		/// <returns></returns>
		protected virtual Task<bool> OnQueueAction(TQueueObject queueTask, string filename, CancellationToken stoppingToken)
		{
			return Task.FromResult(true);
		}

		public void OnStart(CancellationToken cancellationToken)
		{
			_logger.LogDebug("Queue Initializing");
			try
			{
				InitQueues();
				StartQueues();
				initialized = true;
			}
			catch (Exception e)
			{
				_logger.LogCritical(e, "Queue initialize exception, {m}", e.Message);
			}
			return ;
		}

		public void OnStop(CancellationToken cancellationToken)
		{
			StopQueues();
			_logger.LogInformation("Queue Terminated");
		}
	}

}
