using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using PipServices.Aws.Connect;
using PipServices.Commons.Config;
using PipServices.Commons.Convert;
using PipServices.Commons.Errors;
using PipServices.Components.Auth;
using PipServices.Components.Connect;
using PipServices.Messaging.Queues;

namespace PipServices.Aws.Queues
{
    public class AmazonSqsMessageQueue : MessageQueue
    {
        private long DefaultVisibilityTimeout = 60000;
        private long DefaultCheckInterval = 10000;

        private AmazonSQSClient _client;
        private string _queue;
        private string _deadQueue;
        private CancellationTokenSource _cancel = new CancellationTokenSource();

        public AmazonSqsMessageQueue(string name = null)
        {
            Name = name;
            Capabilities = new MessagingCapabilities(true, true, true, true, true, false, true, true, true);
            Interval = DefaultCheckInterval;
        }

        public AmazonSqsMessageQueue(string name, ConfigParams config)
            : this(name)
        {
            if (config != null) Configure(config);
        }

        public AmazonSqsMessageQueue(string name, AmazonSQSClient client, string queue)
            : this(name)
        {
            _client = client;
            _queue = queue;
        }

        public long Interval { get; set; }

        public override void Configure(ConfigParams config)
        {
            base.Configure(config);

            Interval = config.GetAsLongWithDefault("interval", Interval);
        }

        private void CheckOpened(string correlationId)
        {
            if (_queue == null)
                throw new InvalidStateException(correlationId, "NOT_OPENED", "The queue is not opened");
        }

        public override bool IsOpened()
        {
            return _queue != null;
        }

        public async override Task OpenAsync(string correlationId, ConnectionParams connection, CredentialParams credential)
        {
            var awsConnection = new AwsConnectionParams(connection, credential);

            // Assign service name
            awsConnection.Service = "sqs";

            // Assign queue name
            var queueName = awsConnection.Resource ?? awsConnection.Get("queue") ?? Name;
            awsConnection.Resource = queueName;
            var deadQueueName = awsConnection.Get("dead_queue");

            // Validate connection params
            var err = awsConnection.Validate(correlationId);
            if (err != null) throw err;

            _logger.Info(null, "Connecting queue {0} to {1}", Name, awsConnection.Arn);

            var region = RegionEndpoint.GetBySystemName(awsConnection.Region);
            var config = new AmazonSQSConfig()
            {
                RegionEndpoint = region,
                UseHttp = true
            };
            _client = new AmazonSQSClient(awsConnection.AccessId, awsConnection.AccessKey, config);

            try
            {
                try
                {
                    // Create queue if it doesn't exist
                    await _client.CreateQueueAsync(queueName);
                }
                catch (QueueNameExistsException)
                {
                    // Ignore exception.
                }

                try
                {
                    // Create dead queue if it doesn't exist
                    if (!string.IsNullOrEmpty(deadQueueName))
                        await _client.CreateQueueAsync(deadQueueName);
                }
                catch (QueueNameExistsException)
                {
                    // Ignore exception.
                }

                var response = await _client.GetQueueUrlAsync(queueName);
                _queue = response.QueueUrl;

                if (!string.IsNullOrEmpty(deadQueueName))
                {
                    response = await _client.GetQueueUrlAsync(deadQueueName);
                    _deadQueue = response.QueueUrl;
                }
                else
                {
                    _deadQueue = null;
                }
            }
            catch (Exception ex)
            {
                throw new ConnectionException(correlationId, "CANNOT_ACCESS_QUEUE", "Failed to access SQS queue", ex)
                    .WithDetails("queue", _queue);
            }
        }

        public override async Task CloseAsync(string correlationId)
        {
            _cancel.Cancel();

            _logger.Trace(correlationId, "Closed queue {0}", this);

            await Task.Delay(0);
        }

        public override long? MessageCount
        {
            get
            {
                CheckOpened(null);

                var request = new GetQueueAttributesRequest()
                {
                    QueueUrl = _queue,
                     AttributeNames = new List<string>(new string[] { QueueAttributeName.ApproximateNumberOfMessages })
                };
                var response = _client.GetQueueAttributesAsync(request, _cancel.Token).Result;

                return response.ApproximateNumberOfMessages;
            }
        }

        private MessageEnvelop ToMessage(Message envelop)
        {
            if (envelop == null) return null;

            MessageEnvelop message = null;

            try
            {
                message = JsonConverter.FromJson<MessageEnvelop>(envelop.Body);
            }
            catch
            {
                // Handle broken messages gracefully
                _logger.Warn(null, "Cannot deserialize message: " + envelop.Body);
            }

            // If message is broken or null
            if (message == null)
            {
                message = new MessageEnvelop
                {
                    Message = envelop.Body
                };
            }

            message.SentTimeUtc = DateTime.UtcNow;
            message.MessageId = envelop.MessageId;
            message.Reference = envelop;

            return message;
        }

        public override async Task SendAsync(string correlationId, MessageEnvelop message)
        {
            CheckOpened(correlationId);
            var content = JsonConverter.ToJson(message);

            var request = new SendMessageRequest()
            {
                QueueUrl = _queue,
                //MessageDeduplicationId = message.MessageId,
                //MessageGroupId = message.MessageType,
                MessageBody = content
            };
            await _client.SendMessageAsync(request, _cancel.Token);

            _counters.IncrementOne("queue." + Name + ".sent_messages");
            _logger.Debug(message.CorrelationId, "Sent message {0} via {1}", message, this);

            await Task.Delay(0);
        }

        public override async Task<MessageEnvelop> PeekAsync(string correlationId)
        {
            CheckOpened(correlationId);

            // Read the message and exit if received
            var request = new ReceiveMessageRequest()
            {
                QueueUrl = _queue,
                WaitTimeSeconds = 0,
                VisibilityTimeout = 0,
                MaxNumberOfMessages = 1
            };
            var response = await _client.ReceiveMessageAsync(request, _cancel.Token);

            var envelop = response.Messages.Count > 0 ? response.Messages[0] : null;
            if (envelop == null) return null;

            var message = ToMessage(envelop);
            if (message != null)
            {
                _logger.Trace(message.CorrelationId, "Peeked message {0} on {1}", message, this);
            }

            return message;
        }

        public override async Task<List<MessageEnvelop>> PeekBatchAsync(string correlationId, int messageCount)
        {
            CheckOpened(correlationId);

            var request = new ReceiveMessageRequest()
            {
                QueueUrl = _queue,
                WaitTimeSeconds = 0,
                VisibilityTimeout = 0,
                MaxNumberOfMessages = 1
            };
            var response = await _client.ReceiveMessageAsync(request, _cancel.Token);

            var envelops = response.Messages;
            var messages = new List<MessageEnvelop>();

            foreach (var envelop in envelops)
            {
                var message = ToMessage(envelop);
                if (message != null)
                    messages.Add(message);
            }

            _logger.Trace(correlationId, "Peeked {0} messages on {1}", messages.Count, this);

            return messages;
        }

        public override async Task<MessageEnvelop> ReceiveAsync(string correlationId, long waitTimeout)
        {
            CheckOpened(correlationId);

            // Read the message and exit if received
            var request = new ReceiveMessageRequest()
            {
                QueueUrl = _queue,
                WaitTimeSeconds = (int)(waitTimeout / 1000),
                VisibilityTimeout = (int)(DefaultVisibilityTimeout / 1000),
                MaxNumberOfMessages = 1
            };
            var response = await _client.ReceiveMessageAsync(request, _cancel.Token);

            var envelop = response.Messages.Count > 0 ? response.Messages[0] : null;
            var message = ToMessage(envelop);

            if (message != null)
            {
                _counters.IncrementOne("queue." + Name + ".received_messages");
                _logger.Debug(message.CorrelationId, "Received message {0} via {1}", message, this);
            }

            return message;
        }

        public override async Task RenewLockAsync(MessageEnvelop message, long lockTimeout)
        {
            CheckOpened(message.CorrelationId);

            // Extend the message visibility
            var envelop = (Message) message.Reference;
            if (envelop != null)
            {
                var request = new ChangeMessageVisibilityRequest()
                {
                    QueueUrl = _queue,
                    ReceiptHandle = envelop.ReceiptHandle,
                    VisibilityTimeout = (int)(lockTimeout / 1000)
                };
                await _client.ChangeMessageVisibilityAsync(request, _cancel.Token);

                _logger.Trace(message.CorrelationId, "Renewed lock for message {0} at {1}", message, this);
            }
        }

        public override async Task AbandonAsync(MessageEnvelop message)
        {
            CheckOpened(message.CorrelationId);

            // Make the message immediately visible
            var envelop = (Message) message.Reference;
            if (envelop != null)
            {
                var request = new ChangeMessageVisibilityRequest()
                {
                    QueueUrl = _queue,
                    ReceiptHandle = envelop.ReceiptHandle,
                    VisibilityTimeout = 0
                };
                await _client.ChangeMessageVisibilityAsync(request, _cancel.Token);

                message.Reference = null;
                _logger.Trace(message.CorrelationId, "Abandoned message {0} at {1}", message, this);
            }
        }

        public override async Task CompleteAsync(MessageEnvelop message)
        {
            CheckOpened(message.CorrelationId);

            var envelop = (Message)message.Reference;
            if (envelop != null)
            {
                await _client.DeleteMessageAsync(_queue, envelop.ReceiptHandle, _cancel.Token);

                message.Reference = null;
                _logger.Trace(message.CorrelationId, "Completed message {0} at {1}", message, this);
            }
            await Task.Delay(0);
        }

        public override async Task MoveToDeadLetterAsync(MessageEnvelop message)
        {
            CheckOpened(message.CorrelationId);
            var envelop = (Message)message.Reference;
            if (envelop != null)
            {
                // Resend message to dead queue if it is defined
                if (_deadQueue != null)
                {
                    var content = JsonConverter.ToJson(message);
                    await _client.SendMessageAsync(_deadQueue, content, _cancel.Token);
                }
                else
                {
                    _logger.Warn(message.CorrelationId, "No dead letter queue is defined for {0}. The message is discarded.", this);
                }

                // Remove the message from the queue
                await _client.DeleteMessageAsync(_queue, envelop.ReceiptHandle, _cancel.Token);
                message.Reference = null;

                _counters.IncrementOne("queue." + Name + ".dead_messages");
                _logger.Trace(message.CorrelationId, "Moved to dead message {0} at {1}", message, this);
            }
        }

        public override async Task ListenAsync(string correlationId, Func<MessageEnvelop, IMessageQueue, Task> callback)
        {
            CheckOpened(correlationId);
            _logger.Debug(correlationId, "Started listening messages at {0}", this);

            // Create new cancelation token
            _cancel = new CancellationTokenSource();

            while (!_cancel.IsCancellationRequested)
            {
                var request = new ReceiveMessageRequest()
                {
                    QueueUrl = _queue,
                    MaxNumberOfMessages = 1
                };
                var response = await _client.ReceiveMessageAsync(_queue, _cancel.Token);
                var envelop = response.Messages.Count > 0 ? response.Messages[0] : null;

                if (envelop != null && !_cancel.IsCancellationRequested)
                {
                    var message = ToMessage(envelop);

                    _counters.IncrementOne("queue." + Name + ".received_messages");
                    _logger.Debug(message.CorrelationId, "Received message {0} via {1}", message, this);

                    try
                    {
                        await callback(message, this);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(correlationId, ex, "Failed to process the message");
                        //throw ex;
                    }
                }
                else
                {
                    // If no messages received then wait
                    await Task.Delay(TimeSpan.FromMilliseconds(Interval));
                }
            }
        }

        public override void EndListen(string correlationId)
        {
            _cancel.Cancel();
        }

        public override async Task ClearAsync(string correlationId)
        {
            CheckOpened(correlationId);

            try
            {
                var request = new PurgeQueueRequest()
                {
                    QueueUrl = _queue
                };
                await _client.PurgeQueueAsync(request, _cancel.Token);
            }
            catch (PurgeQueueInProgressException)
            {
                while (true)
                {
                    var messages = await PeekBatchAsync(correlationId, 100);

                    foreach (var message in messages)
                    {
                        await CompleteAsync(message);
                    }

                    if (messages.Count < 90) break;
                }
            }

            _logger.Trace(null, "Cleared queue {0}", this);
        }
    }
}
