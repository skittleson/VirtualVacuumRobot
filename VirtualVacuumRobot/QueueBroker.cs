using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;

namespace VirtualVacuumRobot {

    public class QueueBroker : IDisposable {
        private const string QUEUE_NAME = "VirtualVacuumBotQueue";
        private readonly AmazonSQSClient _client;
        private bool _running;
        private Thread _waitingForMessagesThread;

        private string _queueUrl { get; set; }
        public Action<IList<string>> OnEvent { get; set; }
        public string QueueUrlSnsTopicSubscribeArn { get; private set; }

        public QueueBroker() {
            _client = new AmazonSQSClient();
        }

        public void CreateQueue(int id) {
            try {
                var findResponse = _client.GetQueueUrlAsync(new GetQueueUrlRequest(QUEUE_NAME + id)).Result;
                _queueUrl = findResponse.QueueUrl;
            } catch (Exception ex) {
                var createdRequest = new CreateQueueRequest(QUEUE_NAME + id) {
                    Attributes = new Dictionary<string, string> {
                        { QueueAttributeName.MessageRetentionPeriod, "300" },
                        { QueueAttributeName.ReceiveMessageWaitTimeSeconds, "10" }
                    }
                };
                var createdResponse = _client.CreateQueueAsync(createdRequest).Result;
                if ((int)createdResponse.HttpStatusCode == 200) {
                    _queueUrl = createdResponse.QueueUrl;
                } else {
                    throw new Exception("Unable to create queue");
                }
            }
        }

        public void DeleteQueue() {
            _client.DeleteQueueAsync(_queueUrl).Wait();
        }

        public void Subscribe(IAmazonSimpleNotificationService snsClient, string topicArn) {
            QueueUrlSnsTopicSubscribeArn = snsClient.SubscribeQueueAsync(topicArn, _client, _queueUrl).Result;
        }

        public void StartListening() {
            _running = true;
            _waitingForMessagesThread = new Thread(new ThreadStart(ReceiveMessages));
            _waitingForMessagesThread.Start();
        }

        public void StopListening() {
            _running = false;
        }

        public void ReceiveMessages() {
            while (_running) {
                var response = _client.ReceiveMessageAsync(_queueUrl).Result;
                var messages = response.Messages.Select(x => {
                    var message = "";
                    if (x.Body.StartsWith('{')) {
                        dynamic jsonObj = JsonConvert.DeserializeObject<ExpandoObject>(x.Body);
                        message = jsonObj.Message;
                    } else {
                        message = x.Body;
                    }
                    _client.DeleteMessageAsync(new DeleteMessageRequest(_queueUrl, x.ReceiptHandle)).Wait();
                    return new QueueMessageCacheModel(x.MessageId, message);
                }).ToList();
                OnEvent?.Invoke(messages.Select(x => x.Message).ToList());
            }
        }

        public void Dispose() {
            _client?.Dispose();
        }
    }

    public class QueueMessageCacheModel {
        public string MessageId { get; }
        public string Message { get; }
        public DateTime Received { get; }

        public QueueMessageCacheModel(string messageId, string message) {
            Message = message;
            MessageId = messageId;
            Received = DateTime.UtcNow;
        }
    }
}