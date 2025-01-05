using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using QuickWire.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QuickWire.HostedServices
{
	public class MongoMessageQueueSenderConfig<T>
	{
		public string Database { get; set; }
		public string Collection { get; set; }
		public int MaxSizeBytes { get; set; }
		public string SenderId { get; set; }
	}

	public class MongoMessageQueueReceiverConfig<T>
	{
		public string Database { get; set; }
		public string Collection { get; set; }
		public int MaxSizeBytes { get; set; }
		public string BadMsgCollection { get; set; }
	}

	/// <summary>
	/// The problem I have is adding multiple message queue singletons.
	/// each queue singleton also requires its own config singleton
	/// This is possible as long as the message type is different.
	/// I could have one singleton and allow new queues to added dynamically
	/// (or just have multiple queue configs in one config object)
	/// </summary>
	public static class SPConfigServiceCollectionExtensions
	{
		public static IServiceCollection AddMongoMessageQueueReceiver<T>(
			 this IServiceCollection services, Action<MongoMessageQueueReceiverConfig<T>> setconf)
		{
			var config = new MongoMessageQueueReceiverConfig<T>();
			setconf(config);
			services.AddSingleton<MongoMessageQueueReceiverConfig<T>>(config);
			services.AddSingleton<IMessageQueueReceiver<T>, MongoMessageQueueReceiver<T>>();
			return services;
		}
		public static IServiceCollection AddMongoMessageQueueSender<T>(
			 this IServiceCollection services, Action<MongoMessageQueueSenderConfig<T>> setconf)
		{
			var config = new MongoMessageQueueSenderConfig<T>();
			setconf(config);
			services.AddSingleton<MongoMessageQueueSenderConfig<T>>(config);
			services.AddSingleton<IMessageQueueSender<T>, MongoMessageQueueSender<T>>();
			return services;
		}
	}

	public interface IMessageQueueSender<T>
	{
		Task SendAsync(T msg);
	}

	public interface IMessageQueueReceiver<T>
	{
		//Action<IMessage<T>> Subscribe { get; set; }
		//IAsyncEnumerable<IMessage<T>> ReceiveAsync(CancellationToken token = default);
		//Task<IEnumerable<IMessage<T>>> ReceiveAsync(CancellationToken token = default);
		Task<IMessage<T>> ReceiveAsync(CancellationToken token = default);
		Task StartQueueAsync(CancellationToken token = default);

		Task AcknowledgeAsync(IMessage<T> msg, bool failed = false);
	}

	public interface IMessage<T>
	{
		T Payload { get; }
	}

	public class MongoMessage<T> : IMessage<T>
	{
		public enum STATUS { NewQueued, Running, Processed, Error }

		[BsonId]
		public string Id { get; set; }
		public string Status { get; set; }
		public string SenderId { get; set; }
		public DateTime SendTime { get; set; }
		public BsonDocument BsonPayload { get; set; }

		[BsonIgnore]
		public T Payload
		{
			get
			{
				return BsonSerializer.Deserialize<T>(BsonPayload);
			}
		}

		public MongoMessage(T payload, string senderId)
		{
			BsonPayload = payload.ToBsonDocument();
			Id = Guid.NewGuid().ToString();
			SenderId = senderId;
			SendTime = DateTime.UtcNow;
			Status = STATUS.NewQueued.ToString();
		}
	}


	public class MongoMessageQueueBase
	{
		protected readonly ILogger _logger;
		protected readonly IMongoDatabase _db;
		private bool _hasInitialized = false;

		public MongoMessageQueueBase(ILogger logger, IMongoDatabase db)
		{
			_logger = logger;
			_db = db;
		}

		protected async Task Init(string collection_name, int max_size)
		{
			try
			{
				if (!_hasInitialized)
				{
					if (!await CollectionExistsAsync(collection_name))
					{
						var options = new CreateCollectionOptions()
						{
							Capped = true,
							MaxSize = max_size
						};
						_db.CreateCollection(collection_name, options);
					}
					_hasInitialized = true;
				}
			}
			catch (Exception ex)
			{
				_logger.LogCritical("error creating mongo collection, {m}, collection={c}", ex.Message, collection_name);
			}
		}

		protected async Task<bool> CollectionExistsAsync(string collectionName)
		{
			var filter = new BsonDocument("name", collectionName);
			//filter by collection name
			var collections = await _db.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
			//check for existence
			return await collections.AnyAsync();
		}

	}

	public class MongoMessageQueueSender<T> : MongoMessageQueueBase, IMessageQueueSender<T>
	{
		private readonly MongoMessageQueueSenderConfig<T> _config;
		private IMongoCollection<MongoMessage<T>> _Queue => _db.GetCollection<MongoMessage<T>>(_config.Collection);

		public MongoMessageQueueSender(IMongoClient client, MongoMessageQueueSenderConfig<T> config
			, ILogger<MongoMessageQueueSender<T>> logger)
			: base(logger, client.GetDatabase(config.Database))
		{
			_config = config;
			Init(_config.Collection, _config.MaxSizeBytes).Wait();
		}

		// sender doesn't ack anything but we need to implement it
		public Task AcknowledgeAsync(MongoMessage<T> msg)
		{
			return Task.CompletedTask;
		}
		public async Task SendAsync(T msg)
		{
			var env = new MongoMessage<T>(msg, _config.SenderId);

			await _Queue.InsertOneAsync(env);
		}
	}

	public class MongoMessageQueueReceiver<T> : MongoMessageQueueBase, IMessageQueueReceiver<T>
	{
		private readonly MongoMessageQueueReceiverConfig<T> _config;

		private IMongoCollection<MongoMessage<T>> _Queue => _db.GetCollection<MongoMessage<T>>(_config.Collection);

		private BufferBlock<IMessage<T>> _bufferBlock = new BufferBlock<IMessage<T>>();
		public MongoMessageQueueReceiver(IMongoClient client, MongoMessageQueueReceiverConfig<T> config
			, ILogger<MongoMessageQueueReceiver<T>> logger)
			: base(logger, client.GetDatabase(config.Database))
		{
			_config = config;
			Init(_config.Collection, _config.MaxSizeBytes).Wait();
		}


		// I guess either mark the message as read or delete it
		// which end deletes the message?
		// multiple subscribers?
		public async Task AcknowledgeAsync(IMessage<T> imsg, bool failed = false)
		{
			var msg = (MongoMessage<T>)imsg;

			var filter = Builders<MongoMessage<T>>.Filter.Eq(x => x.Id, msg.Id);
			var update = Builders<MongoMessage<T>>.Update.Set(x => x.Status
			, failed ? MongoMessage<T>.STATUS.Error.ToString() : MongoMessage<T>.STATUS.Processed.ToString());
			var res = await _Queue.UpdateOneAsync(filter, update);
			if (res?.IsAcknowledged == false)
			{
				_logger.LogWarning("queue update didn't ack, id={id}", msg.Id);
			}
		}

		public async Task<IMessage<T>> ReceiveAsync(CancellationToken token = default)
		{
			_logger.LogInformation("Starting MongoDB async queue");
			return await _bufferBlock.ReceiveAsync(token);
		}

		// I can't figure out how to implement reading IAsyncEnumerable
		// meaning I can't await IAsyncEnumerablealong with a normal Task
		// so just do a simple Task<IEnumerable<>>
		// I could use BufferBlock here as well I think
		public async Task StartQueueAsync(CancellationToken token = default)
		{
			var options = new FindOptions<MongoMessage<T>>
			{
				// Our cursor is a tailable cursor and informs the server to await
				CursorType = CursorType.TailableAwait,
				//NoCursorTimeout = true,

			};

			var filter = Builders<MongoMessage<T>>.Filter.Eq(x => x.Status, MongoMessage<T>.STATUS.NewQueued.ToString());

			// so findasync will/should block
			using (var cursor = await _Queue.FindAsync(filter, options, token))
			{
				//cursor.
				await cursor.ForEachAsync(doc =>
				{
					_logger.LogInformation("queue doc: {id}", doc.Id);
					_bufferBlock.Post(doc);
				}, token);

			}
			return;
		}


		/// <summary>
		/// I can't await a task using this, it awaits a foreach
		/// so I can't start the task and just wait for it, or at least I can't figure it out
		/// await foreach(var r in ReceiveAsyncEnumerable()){}
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public async IAsyncEnumerable<IMessage<T>> ReceiveAsyncEnumerable(
			[EnumeratorCancellation] CancellationToken token = default)
		{
			var options = new FindOptions<MongoMessage<T>>
			{
				// Our cursor is a tailable cursor and informs the server to await
				CursorType = CursorType.TailableAwait
			};

			var filter = Builders<MongoMessage<T>>.Filter.Eq(x => x.Status, MongoMessage<T>.STATUS.NewQueued.ToString());

			// so findasync will/should block
			using (var cursor = await _Queue.FindAsync(filter, options, token))
			{
				// i think I should be using await cursor.ForEachAsync()
				while (await cursor.MoveNextAsync(token))
				{
					foreach (var doc in cursor.Current)
					{
						token.ThrowIfCancellationRequested();
						_logger.LogInformation("queue doc: {id}", doc.Id);
						yield return doc;
					}
				}
			}
		}
	}
}
