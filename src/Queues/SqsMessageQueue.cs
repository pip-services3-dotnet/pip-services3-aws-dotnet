using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using PipServices3.Aws.Connect;
using PipServices3.Commons.Config;
using PipServices3.Commons.Convert;
using PipServices3.Commons.Errors;
using PipServices3.Components.Auth;
using PipServices3.Components.Connect;
using PipServices3.Messaging.Queues;

namespace PipServices3.Aws.Queues
{
    public class SqsMessageQueue : MessageQueue
    {
        private long DefaultVisibilityTimeout = 60000;
        private long DefaultCheckInterval = 10000;

        private AmazonSQSClient _client;
        private string _queue;
        private string _deadQueue;
        private bool _fifoQueue = false;
        private bool _contentBasedDupication = false;
        private bool _contentBasedDupicationDlq = false;
        private CancellationTokenSource _cancel = new CancellationTokenSource();

        public SqsMessageQueue(string name = null)
        {
            Name = name;
            Capabilities = new MessagingCapabilities(true, true, true, true, true, false, true, true, true);
            Interval = DefaultCheckInterval;
        }

        public SqsMessageQueue(string name, ConfigParams config)
            : this(name)
        {
            if (config != null) Configure(config);
        }

        public SqsMessageQueue(string name, AmazonSQSClient client, string queue)
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

        private async Task<IDictionary<string, string>> GetQueueAttributesAsync(string correlationId, List<string> attributes, string queueUrl)
        {
            var attributeMap = new Dictionary<string, string>();

            try
            {
                var response = await _client.GetQueueAttributesAsync(new GetQueueAttributesRequest(queueUrl, attributes));

                foreach (var attibute in response.Attributes)
                    attributeMap.Add(attibute.Key, attibute.Value);
            }
            catch (InvalidAttributeNameException)
            {
                // Ignore invalid name exception
            }

            return attributeMap;
        }

        private SendMessageRequest CreateSendMessageRequest(MessageEnvelope message, bool contentBasedDupication, string queueUrl)
        {
            var content = JsonConverter.ToJson(message);

            var request = new SendMessageRequest()
            {
                QueueUrl = queueUrl,
                MessageBody = content
            };

            if (_fifoQueue)
            {
                // A message group id is required for a fifo queue message
                request.MessageGroupId = message.MessageType;

                // A message deduplication id is required if content based duplication is turned off for the fifo queue.
                if (!contentBasedDupication) request.MessageDeduplicationId = message.MessageId;
            }

            return request;
        }

        private void CheckOpened(string correlationId)
        {
            if (_queue == null)
                throw new InvalidStateException(correlationId, "NOT_OPENED", "The queue is not opened");
        }

        public override bool IsOpen()
        {
            return _queue != null;
        }

        public override async Task OpenAsync(string correlationId, List<ConnectionParams> connections, CredentialParams credential)
        {
            var connection = connections?.FirstOrDefault();
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            var awsConnection = new AwsConnectionParams(connection, credential);

            // Assign service name
            awsConnection.Service = "sqs";

            // Assign queue name
            var queueName = awsConnection.Resource ?? awsConnection.Get("queue") ?? Name;
            awsConnection.Resource = queueName;
            var deadQueueName = awsConnection.Get("dead_queue");

            // Determine if a fifo queue is being used
            _fifoQueue = queueName.EndsWith(".fifo", StringComparison.OrdinalIgnoreCase) ? true : false;

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
                    var queueRequest = new CreateQueueRequest(queueName);

                    if (_fifoQueue)
                        queueRequest.Attributes.Add("FifoQueue", "true");

                    await _client.CreateQueueAsync(queueRequest);
                }
                catch (QueueNameExistsException)
                {
                    // Ignore exception.
                }

                try
                {
                    // Create dead queue if it doesn't exist
                    if (!string.IsNullOrEmpty(deadQueueName))
                    {
                        var deadQueueRequest = new CreateQueueRequest(deadQueueName);

                        if (_fifoQueue)
                            deadQueueRequest.Attributes.Add("FifoQueue", "true");

                        await _client.CreateQueueAsync(deadQueueRequest);
                    }
                }
                catch (QueueNameExistsException)
                {
                    // Ignore exception.
                }

                var response = await _client.GetQueueUrlAsync(queueName);
                _queue = response.QueueUrl;

                if (_fifoQueue)
                {
                    var temp = await GetQueueAttributesAsync(correlationId, new List<string>() { "ContentBasedDeduplication" }, _queue);
                    _contentBasedDupication = BooleanConverter.ToBooleanWithDefault(temp["ContentBasedDeduplication"], _contentBasedDupication);
                }

                if (!string.IsNullOrEmpty(deadQueueName))
                {
                    response = await _client.GetQueueUrlAsync(deadQueueName);
                    _deadQueue = response.QueueUrl;

                    if (_fifoQueue)
                    {
                        var temp = await GetQueueAttributesAsync(correlationId, new List<string>() { "ContentBasedDeduplication" }, _deadQueue);
                        _contentBasedDupicationDlq = BooleanConverter.ToBooleanWithDefault(temp["ContentBasedDeduplication"], _contentBasedDupicationDlq);
                    }
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


        public override async Task<long> ReadMessageCountAsync()
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

        private MessageEnvelope ToMessage(Message envelope)
        {
            if (envelope == null) return null;

            MessageEnvelope message = null;

            try
            {
                message = JsonConverter.FromJson<MessageEnvelope>(envelope.Body);
            }
            catch
            {
                // Handle broken messages gracefully
                _logger.Warn(null, "Cannot deserialize message: " + envelope.Body);
            }

            // If message is broken or null
            if (message == null)
            {
                message = new MessageEnvelope
                {
                    Message = envelope.Body
                };
            }

            message.SentTime = DateTime.UtcNow;
            message.MessageId = envelope.MessageId;
            message.Reference = envelope;

            return message;
        }

        public override async Task SendAsync(string correlationId, MessageEnvelope message)
        {
            CheckOpened(correlationId);

            // Set the message sent time
            message.SentTime = DateTime.UtcNow;

            await _client.SendMessageAsync(CreateSendMessageRequest(message, _contentBasedDupication, _queue), _cancel.Token);

            _counters.IncrementOne("queue." + Name + ".sent_messages");
            _logger.Debug(message.CorrelationId, "Sent message {0} via {1}", message, this);

            await Task.Delay(0);
        }

        public override async Task<MessageEnvelope> PeekAsync(string correlationId)
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

            var envelope = response.Messages.Count > 0 ? response.Messages[0] : null;
            if (envelope == null) return null;

            var message = ToMessage(envelope);
            if (message != null)
            {
                _logger.Trace(message.CorrelationId, "Peeked message {0} on {1}", message, this);
            }

            return message;
        }

        public override async Task<List<MessageEnvelope>> PeekBatchAsync(string correlationId, int messageCount)
        {
            CheckOpened(correlationId);

            var request = new ReceiveMessageRequest()
            {
                QueueUrl = _queue,
                WaitTimeSeconds = 0,
                VisibilityTimeout = 0,
                MaxNumberOfMessages = messageCount
            };
            var response = await _client.ReceiveMessageAsync(request, _cancel.Token);

            var envelopes = response.Messages;
            var messages = new List<MessageEnvelope>();

            foreach (var envelope in envelopes)
            {
                var message = ToMessage(envelope);
                if (message != null)
                    messages.Add(message);
            }

            _logger.Trace(correlationId, "Peeked {0} messages on {1}", messages.Count, this);

            return messages;
        }

        public override async Task<MessageEnvelope> ReceiveAsync(string correlationId, long waitTimeout)
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

            var envelope = response.Messages.Count > 0 ? response.Messages[0] : null;
            var message = ToMessage(envelope);

            if (message != null)
            {
                _counters.IncrementOne("queue." + Name + ".received_messages");
                _logger.Debug(message.CorrelationId, "Received message {0} via {1}", message, this);
            }

            return message;
        }

        public override async Task RenewLockAsync(MessageEnvelope message, long lockTimeout)
        {
            CheckOpened(message.CorrelationId);

            // Extend the message visibility
            var envelope = (Message) message.Reference;
            if (envelope != null)
            {
                var request = new ChangeMessageVisibilityRequest()
                {
                    QueueUrl = _queue,
                    ReceiptHandle = envelope.ReceiptHandle,
                    VisibilityTimeout = (int)(lockTimeout / 1000)
                };
                await _client.ChangeMessageVisibilityAsync(request, _cancel.Token);

                _logger.Trace(message.CorrelationId, "Renewed lock for message {0} at {1}", message, this);
            }
        }

        public override async Task AbandonAsync(MessageEnvelope message)
        {
            CheckOpened(message.CorrelationId);

            // Make the message immediately visible
            var envelope = (Message) message.Reference;
            if (envelope != null)
            {
                var request = new ChangeMessageVisibilityRequest()
                {
                    QueueUrl = _queue,
                    ReceiptHandle = envelope.ReceiptHandle,
                    VisibilityTimeout = 0
                };
                await _client.ChangeMessageVisibilityAsync(request, _cancel.Token);

                message.Reference = null;
                _logger.Trace(message.CorrelationId, "Abandoned message {0} at {1}", message, this);
            }
        }

        public override async Task CompleteAsync(MessageEnvelope message)
        {
            CheckOpened(message.CorrelationId);

            var envelope = (Message)message.Reference;
            if (envelope != null)
            {
                await _client.DeleteMessageAsync(_queue, envelope.ReceiptHandle, _cancel.Token);

                message.Reference = null;
                _logger.Trace(message.CorrelationId, "Completed message {0} at {1}", message, this);
            }
            await Task.Delay(0);
        }

        public override async Task MoveToDeadLetterAsync(MessageEnvelope message)
        {
            CheckOpened(message.CorrelationId);
            var envelope = (Message)message.Reference;
            if (envelope != null)
            {
                // Resend message to dead queue if it is defined
                if (_deadQueue != null)
                {
                    await _client.SendMessageAsync(CreateSendMessageRequest(message, _contentBasedDupicationDlq, _deadQueue), _cancel.Token);
                }
                else
                {
                    _logger.Warn(message.CorrelationId, "No dead letter queue is defined for {0}. The message is discarded.", this);
                }

                // Remove the message from the queue
                await _client.DeleteMessageAsync(_queue, envelope.ReceiptHandle, _cancel.Token);
                message.Reference = null;

                _counters.IncrementOne("queue." + Name + ".dead_messages");
                _logger.Trace(message.CorrelationId, "Moved to dead message {0} at {1}", message, this);
            }
        }

        public override async Task ListenAsync(string correlationId, IMessageReceiver receiver)
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
                var envelope = response.Messages.Count > 0 ? response.Messages[0] : null;

                if (envelope != null && !_cancel.IsCancellationRequested)
                {
                    var message = ToMessage(envelope);

                    _counters.IncrementOne("queue." + Name + ".received_messages");
                    _logger.Debug(message.CorrelationId, "Received message {0} via {1}", message, this);

                    try
                    {
                        await receiver.ReceiveMessageAsync(message, this);
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
                    var messages = await PeekBatchAsync(correlationId, 10);

                    foreach (var message in messages)
                    {
                        await CompleteAsync(message);
                    }

                    if (messages.Count < 9) break;
                }
            }

            _logger.Trace(null, "Cleared queue {0}", this);
        }
    }
}
